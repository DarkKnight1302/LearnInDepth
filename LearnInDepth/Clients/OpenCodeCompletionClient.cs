using NewHorizonLib.Services;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LearnInDepth.Clients
{
    /// <summary>
    /// Client for the OpenCode Go LLM gateway (OpenAI-compatible chat completions).
    /// Non-streaming. Never throws for HTTP/JSON failures - returns CompletionResult with IsSuccess=false.
    /// Retries 429s honoring Retry-After, then walks the configured fallback model chain.
    /// </summary>
    public class OpenCodeCompletionClient : IOpenCodeCompletionClient
    {
        private const string BaseUrl = "https://opencode.ai/zen/go/v1/chat/completions";
        private const string ApiKeySecretName = "opencodeGoApiKey";
        private const int MaxAttemptsPerModel = 3;

        private readonly HttpClient httpClient;
        private readonly ILogger<OpenCodeCompletionClient> logger;
        private readonly IReadOnlyList<string> fallbackModels;
        private readonly SemaphoreSlim concurrencyGate;

        private static readonly JsonSerializerOptions RequestSerializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private static readonly JsonSerializerOptions ResponseSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public OpenCodeCompletionClient(
            ISecretService secretService,
            IConfiguration configuration,
            ILogger<OpenCodeCompletionClient> logger)
        {
            string apiKey = secretService.GetSecretValue(ApiKeySecretName);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException($"Missing required secret: {ApiKeySecretName}");
            }

            this.logger = logger;
            this.fallbackModels = configuration.GetSection("OpenCode:FallbackModels").Get<string[]>() ?? Array.Empty<string>();
            int maxConcurrent = configuration.GetValue<int?>("OpenCode:MaxConcurrentLlmCalls") ?? 6;
            this.concurrencyGate = new SemaphoreSlim(maxConcurrent, maxConcurrent);

            this.httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public async Task<CompletionResult<T>> SendPromptJsonAsync<T>(
            string model,
            string systemPrompt,
            string userPrompt,
            double temperature,
            int maxTokens,
            CancellationToken cancellationToken = default)
        {
            CompletionResult textResult = await SendWithFallbackAsync(
                model, systemPrompt, userPrompt, temperature, maxTokens,
                useJsonResponseFormat: true,
                cancellationToken).ConfigureAwait(false);

            if (!textResult.IsSuccess)
            {
                return CompletionResult<T>.Failure(textResult.ErrorMessage, model: textResult.ModelUsed);
            }

            return TryParseJson<T>(textResult.Text, textResult.ModelUsed);
        }

        public async Task<CompletionResult> SendPromptTextAsync(
            string model,
            string systemPrompt,
            string userPrompt,
            double temperature,
            int maxTokens,
            CancellationToken cancellationToken = default)
        {
            CompletionResult result = await SendWithFallbackAsync(
                model, systemPrompt, userPrompt, temperature, maxTokens,
                useJsonResponseFormat: false,
                cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return result;
            }

            return CompletionResult.Ok(StripCodeFences(result.Text), result.ModelUsed);
        }

        private async Task<CompletionResult> SendWithFallbackAsync(
            string primaryModel,
            string systemPrompt,
            string userPrompt,
            double temperature,
            int maxTokens,
            bool useJsonResponseFormat,
            CancellationToken cancellationToken)
        {
            var modelsToTry = new List<string> { primaryModel };
            foreach (string fallback in fallbackModels)
            {
                if (!string.Equals(fallback, primaryModel, StringComparison.OrdinalIgnoreCase))
                {
                    modelsToTry.Add(fallback);
                }
            }

            string lastError = string.Empty;
            foreach (string model in modelsToTry)
            {
                for (int attempt = 1; attempt <= MaxAttemptsPerModel; attempt++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return CompletionResult.Fail("Cancelled", model);
                    }

                    CompletionResult result = await ExecuteSingleAttemptAsync(
                        model, systemPrompt, userPrompt, temperature, maxTokens,
                        useJsonResponseFormat, attempt, cancellationToken).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        return result;
                    }

                    lastError = result.ErrorMessage;
                    logger.LogWarning("LLM attempt {Attempt}/{MaxAttempts} on model {Model} failed: {Error}",
                        attempt, MaxAttemptsPerModel, model, result.ErrorMessage);
                }
            }

            return CompletionResult.Fail($"All models exhausted. Last error: {lastError}");
        }

        private async Task<CompletionResult> ExecuteSingleAttemptAsync(
            string model,
            string systemPrompt,
            string userPrompt,
            double? temperature,
            int? maxTokens,
            bool useJsonResponseFormat,
            int attempt,
            CancellationToken cancellationToken)
        {
            await concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var request = new ChatCompletionRequest
                {
                    Model = model,
                    Messages = BuildMessages(systemPrompt, userPrompt),
                    Temperature = temperature,
                    MaxTokens = maxTokens,
                    Stream = false,
                    ResponseFormat = useJsonResponseFormat ? new ResponseFormat() : null
                };

                string requestJson = JsonSerializer.Serialize(request, RequestSerializerOptions);
                using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                using HttpResponseMessage response = await httpClient.PostAsync(BaseUrl, content, cancellationToken).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    TimeSpan delay = ResolveRetryDelay(response, responseBody, attempt);
                    logger.LogWarning("LLM 429 on {Model}. Waiting {Delay}s before retry.", model, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    return CompletionResult.Fail($"429 rate limited: {Truncate(responseBody)}", model);
                }

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    // Provider-specific parameter rejections: drop the offending parameter and retry in-place
                    // (each recovery happens at most once per attempt).
                    if (temperature.HasValue && responseBody.Contains("temperature", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogWarning("Model {Model} rejected temperature {Temperature}; retrying with provider default.", model, temperature);
                        return await ExecuteSingleAttemptAsync(model, systemPrompt, userPrompt, null, maxTokens,
                            useJsonResponseFormat, attempt, cancellationToken).ConfigureAwait(false);
                    }
                    if (maxTokens.HasValue && responseBody.Contains("max_tokens", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogWarning("Model {Model} rejected max_tokens {MaxTokens}; retrying with provider default.", model, maxTokens);
                        return await ExecuteSingleAttemptAsync(model, systemPrompt, userPrompt, temperature, null,
                            useJsonResponseFormat, attempt, cancellationToken).ConfigureAwait(false);
                    }
                    if (useJsonResponseFormat && responseBody.Contains("response_format", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogWarning("Model {Model} rejected response_format; retrying without it.", model);
                        return await ExecuteSingleAttemptAsync(model, systemPrompt, userPrompt, temperature, maxTokens,
                            false, attempt, cancellationToken).ConfigureAwait(false);
                    }
                }

                if (!response.IsSuccessStatusCode)
                {
                    if ((int)response.StatusCode >= 500)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken).ConfigureAwait(false);
                    }
                    return CompletionResult.Fail($"HTTP {(int)response.StatusCode}: {Truncate(responseBody)}", model);
                }

                ChatCompletionResponse parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(responseBody, ResponseSerializerOptions);
                string text = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
                if (string.IsNullOrWhiteSpace(text))
                {
                    return CompletionResult.Fail($"Empty completion content. Raw: {Truncate(responseBody)}", model);
                }

                string finishReason = parsed?.Choices?.FirstOrDefault()?.FinishReason ?? string.Empty;
                if (string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Model {Model} truncated the response (finish_reason=length). Consider raising max tokens.", model);
                    return CompletionResult.Fail($"Response truncated (finish_reason=length) at {maxTokens?.ToString() ?? "provider-default"} max tokens", model);
                }

                logger.LogInformation("LLM call succeeded. Model={Model}, PromptTokens={PromptTokens}, CompletionTokens={CompletionTokens}",
                    model, parsed?.Usage?.PromptTokens ?? 0, parsed?.Usage?.CompletionTokens ?? 0);
                return CompletionResult.Ok(text, model);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return CompletionResult.Fail("Cancelled", model);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "LLM call error on model {Model}", model);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), CancellationToken.None).ConfigureAwait(false);
                return CompletionResult.Fail($"{ex.GetType().Name}: {ex.Message}", model);
            }
            finally
            {
                concurrencyGate.Release();
            }
        }

        private static List<ChatMessage> BuildMessages(string systemPrompt, string userPrompt)
        {
            var messages = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new ChatMessage { Role = "system", Content = systemPrompt });
            }
            messages.Add(new ChatMessage { Role = "user", Content = userPrompt });
            return messages;
        }

        private static TimeSpan ResolveRetryDelay(HttpResponseMessage response, string body, int attempt)
        {
            if (response.Headers.RetryAfter?.Delta.HasValue == true)
            {
                return response.Headers.RetryAfter.Delta.Value;
            }

            Match match = Regex.Match(body ?? string.Empty, @"try again in (\d+(?:\.\d+)?)s", RegexOptions.IgnoreCase);
            if (match.Success && double.TryParse(match.Groups[1].Value, out double seconds))
            {
                return TimeSpan.FromSeconds(Math.Min(seconds, 60));
            }

            return TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt) * 5, 60));
        }

        private CompletionResult<T> TryParseJson<T>(string text, string model)
        {
            string candidate = StripCodeFences(text);

            try
            {
                T direct = JsonSerializer.Deserialize<T>(candidate, ResponseSerializerOptions);
                if (direct != null)
                {
                    return CompletionResult<T>.Success(direct, text, model);
                }
            }
            catch (JsonException)
            {
                // fall through to extraction
            }

            string extracted = ExtractBalancedJson(candidate);
            if (!string.IsNullOrEmpty(extracted))
            {
                try
                {
                    T extractedResult = JsonSerializer.Deserialize<T>(extracted, ResponseSerializerOptions);
                    if (extractedResult != null)
                    {
                        return CompletionResult<T>.Success(extractedResult, text, model);
                    }
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Extracted JSON did not match target type {Type}", typeof(T).Name);
                }
            }

            return CompletionResult<T>.Failure("Response did not contain valid JSON matching the expected schema.", text, model);
        }

        internal static string StripCodeFences(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("```"))
            {
                int firstNewline = trimmed.IndexOf('\n');
                if (firstNewline > 0)
                {
                    trimmed = trimmed[(firstNewline + 1)..];
                }
                int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (lastFence >= 0)
                {
                    trimmed = trimmed[..lastFence];
                }
            }
            return trimmed.Trim();
        }

        internal static string ExtractBalancedJson(string text)
        {
            int start = text.IndexOf('{');
            if (start < 0)
            {
                return string.Empty;
            }

            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (c == '\\' && inString)
                {
                    escaped = true;
                    continue;
                }
                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }
                if (inString)
                {
                    continue;
                }
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text[start..(i + 1)];
                    }
                }
            }
            return string.Empty;
        }

        private static string Truncate(string value, int maxLength = 400) =>
            string.IsNullOrEmpty(value) ? string.Empty : value.Length <= maxLength ? value : value[..maxLength];
    }
}
