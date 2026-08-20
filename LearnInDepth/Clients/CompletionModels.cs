using System.Text.Json.Serialization;

namespace LearnInDepth.Clients
{
    public class CompletionResult<T>
    {
        public bool IsSuccess { get; set; }
        public T Data { get; set; }
        public string RawResponse { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string ModelUsed { get; set; } = string.Empty;

        public static CompletionResult<T> Success(T data, string raw, string model) =>
            new CompletionResult<T> { IsSuccess = true, Data = data, RawResponse = raw, ModelUsed = model };

        public static CompletionResult<T> Failure(string error, string raw = "", string model = "") =>
            new CompletionResult<T> { IsSuccess = false, ErrorMessage = error, RawResponse = raw, ModelUsed = model };
    }

    public class CompletionResult
    {
        public bool IsSuccess { get; set; }
        public string Text { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string ModelUsed { get; set; } = string.Empty;

        public static CompletionResult Ok(string text, string model) =>
            new CompletionResult { IsSuccess = true, Text = text, ModelUsed = model };

        public static CompletionResult Fail(string error, string model = "") =>
            new CompletionResult { IsSuccess = false, ErrorMessage = error, ModelUsed = model };
    }

    internal class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

        [JsonPropertyName("temperature")]
        public double? Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false;

        [JsonPropertyName("response_format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ResponseFormat ResponseFormat { get; set; }
    }

    internal class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    internal class ResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "json_object";
    }

    internal class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatChoice> Choices { get; set; }

        [JsonPropertyName("error")]
        public ChatError Error { get; set; }

        [JsonPropertyName("usage")]
        public ChatUsage Usage { get; set; }
    }

    internal class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string FinishReason { get; set; }
    }

    internal class ChatError
    {
        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; }
    }

    internal class ChatUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }
    }
}
