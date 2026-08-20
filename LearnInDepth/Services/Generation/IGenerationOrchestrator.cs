namespace LearnInDepth.Services.Generation
{
    public interface IGenerationOrchestrator
    {
        Task GenerateAsync(GenerationWorkItem workItem, CancellationToken cancellationToken);
    }
}
