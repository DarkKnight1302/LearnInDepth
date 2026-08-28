using System.Collections.Concurrent;
using System.Threading.Channels;

namespace LearnInDepth.Services.Generation
{
    /// <summary>
    /// Snapshot of the queued generation work for a single plan. Lets status consumers report the
    /// real state of a chapter (queued = will be generated) without draining the channel.
    /// </summary>
    public class GenerationQueueStatus
    {
        public bool WholePlanQueued { get; internal set; }
        public HashSet<int> ChapterOrdersQueued { get; } = new HashSet<int>();
        public bool HasWork => WholePlanQueued || ChapterOrdersQueued.Count > 0;
        public bool HasWorkForChapter(int order) => WholePlanQueued || ChapterOrdersQueued.Contains(order);
    }

    /// <summary>
    /// In-memory queue for generation work items. There is no background consumer: items stay queued
    /// until an explicit drain (e.g. the generation/run endpoint) pulls and processes them.
    /// </summary>
    public interface IGenerationChannel
    {
        ValueTask EnqueueAsync(GenerationWorkItem workItem, CancellationToken cancellationToken = default);
        List<GenerationWorkItem> TryDrainAll();
        GenerationQueueStatus GetQueueStatus(string planId);
        int Count { get; }
    }

    public class GenerationChannel : IGenerationChannel
    {
        private const int WholePlanKey = 0;

        private readonly Channel<GenerationWorkItem> channel = Channel.CreateUnbounded<GenerationWorkItem>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

        // Mirrors queued work per plan so the queue can be inspected without consuming it.
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, byte>> queuedWork = new();

        public ValueTask EnqueueAsync(GenerationWorkItem workItem, CancellationToken cancellationToken = default)
        {
            int key = workItem.ChapterOrder ?? WholePlanKey;
            queuedWork.GetOrAdd(workItem.PlanId, _ => new ConcurrentDictionary<int, byte>()).TryAdd(key, 0);
            return channel.Writer.WriteAsync(workItem, cancellationToken);
        }

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
            // Everything was drained, so no plan has pending work anymore.
            queuedWork.Clear();
            return items;
        }

        public GenerationQueueStatus GetQueueStatus(string planId)
        {
            var status = new GenerationQueueStatus();
            if (queuedWork.TryGetValue(planId, out ConcurrentDictionary<int, byte>? orders))
            {
                status.WholePlanQueued = orders.ContainsKey(WholePlanKey);
                foreach (int order in orders.Keys)
                {
                    if (order != WholePlanKey)
                    {
                        status.ChapterOrdersQueued.Add(order);
                    }
                }
            }
            return status;
        }

        public int Count => channel.Reader.Count;
    }
}