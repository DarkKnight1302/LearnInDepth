using System.Threading.Channels;

namespace LearnInDepth.Services.Generation
{
    public interface IGenerationChannel
    {
        ValueTask EnqueueAsync(GenerationWorkItem workItem, CancellationToken cancellationToken = default);
        IAsyncEnumerable<GenerationWorkItem> ReadAllAsync(CancellationToken cancellationToken);
    }

    public class GenerationChannel : IGenerationChannel
    {
        private readonly Channel<GenerationWorkItem> channel = Channel.CreateUnbounded<GenerationWorkItem>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        public ValueTask EnqueueAsync(GenerationWorkItem workItem, CancellationToken cancellationToken = default) =>
            channel.Writer.WriteAsync(workItem, cancellationToken);

        public IAsyncEnumerable<GenerationWorkItem> ReadAllAsync(CancellationToken cancellationToken) =>
            channel.Reader.ReadAllAsync(cancellationToken);
    }
}
