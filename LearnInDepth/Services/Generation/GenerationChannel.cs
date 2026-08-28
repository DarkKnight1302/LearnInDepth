using System.Threading.Channels;

namespace LearnInDepth.Services.Generation
{
    /// <summary>
    /// In-memory queue for generation work items. There is no background consumer: items stay queued
    /// until an explicit drain (e.g. the generation/run endpoint) pulls and processes them.
    /// </summary>
    public interface IGenerationChannel
    {
        ValueTask EnqueueAsync(GenerationWorkItem workItem, CancellationToken cancellationToken = default);
        List<GenerationWorkItem> TryDrainAll();
        int Count { get; }
    }

    public class GenerationChannel : IGenerationChannel
    {
        private readonly Channel<GenerationWorkItem> channel = Channel.CreateUnbounded<GenerationWorkItem>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

        public ValueTask EnqueueAsync(GenerationWorkItem workItem, CancellationToken cancellationToken = default) =>
            channel.Writer.WriteAsync(workItem, cancellationToken);

        /// <summary>
        /// Removes and returns all work items currently queued. Items enqueued concurrently while
        /// draining remain queued for the next drain call.
        /// </summary>
        public List<GenerationWorkItem> TryDrainAll()
        {
            var items = new List<GenerationWorkItem>();
            while (channel.Reader.TryRead(out GenerationWorkItem? workItem))
            {
                items.Add(workItem);
            }
            return items;
        }

        public int Count => channel.Reader.Count;
    }
}