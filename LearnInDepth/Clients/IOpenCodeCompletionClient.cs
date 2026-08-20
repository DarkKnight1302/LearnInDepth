namespace LearnInDepth.Clients
{
    public interface IOpenCodeCompletionClient
    {
        /// <summary>
        /// Sends a prompt and parses the response as JSON into T.
        /// Falls back through configured fallback models on retryable failures.
        /// </summary>
        Task<CompletionResult<T>> SendPromptJsonAsync<T>(
            string model,
            string systemPrompt,
            string userPrompt,
            double temperature,
            int maxTokens,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a prompt and returns the raw text response (used for HTML content generation).
        /// Falls back through configured fallback models on retryable failures.
        /// </summary>
        Task<CompletionResult> SendPromptTextAsync(
            string model,
            string systemPrompt,
            string userPrompt,
            double temperature,
            int maxTokens,
            CancellationToken cancellationToken = default);
    }
}
