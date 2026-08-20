using LearnInDepth.Models;
using LearnInDepth.Repositories;
using LearnInDepth.Services.Generation;
using Quartz;

namespace LearnInDepth.Jobs
{
    /// <summary>
    /// Periodic recovery job (every 10 minutes). If the app restarts while a chapter is being
    /// generated, the in-memory generation channel is lost and artifacts can be left stuck in
    /// Pending or Generating. This job re-enqueues work items for any chapter whose artifact has
    /// been in Pending/Generating longer than the stuck threshold (i.e. generation stopped making
    /// progress). Active generation keeps LastUpdateUtc/GenerationUpdatedAtUtc fresh, so it is
    /// never mistaken for stuck. Generation is idempotent and the per-plan gate serializes runs,
    /// so re-enqueuing is safe.
    /// </summary>
    [DisallowConcurrentExecution]
    public class GenerationRecoveryJob : IJob
    {
        private readonly ILearningPlanRepository planRepository;
        private readonly IGenerationChannel generationChannel;
        private readonly ILogger<GenerationRecoveryJob> logger;
        private readonly TimeSpan stuckThreshold;

        public GenerationRecoveryJob(
            ILearningPlanRepository planRepository,
            IGenerationChannel generationChannel,
            IConfiguration configuration,
            ILogger<GenerationRecoveryJob> logger)
        {
            this.planRepository = planRepository;
            this.generationChannel = generationChannel;
            this.logger = logger;
            int minutes = configuration.GetValue<int?>("OpenCode:StuckGenerationThresholdMinutes") ?? 15;
            this.stuckThreshold = TimeSpan.FromMinutes(minutes);
        }

        public async Task Execute(IJobExecutionContext context)
        {
            CancellationToken ct = context.CancellationToken;
            try
            {
                List<LearningPlan> plans = await planRepository.ListAllAsync().ConfigureAwait(false);
                int enqueued = 0;
                DateTime now = DateTime.UtcNow;

                foreach (LearningPlan plan in plans)
                {
                    // Plan outline never completed: stuck if the whole plan hasn't progressed recently.
                    if (plan.Status == GenerationStatus.Generating && plan.Chapters.Count == 0
                        && now - plan.GenerationUpdatedAtUtc > stuckThreshold)
                    {
                        await generationChannel.EnqueueAsync(new GenerationWorkItem { PlanId = plan.id }, ct).ConfigureAwait(false);
                        enqueued++;
                        logger.LogInformation("Recovery: plan {PlanId} has no chapters and is stale; re-enqueuing whole plan", plan.id);
                        continue;
                    }

                    foreach (ChapterOutline chapter in plan.Chapters.Where(c => IsStuck(c, now)))
                    {
                        await generationChannel.EnqueueAsync(new GenerationWorkItem { PlanId = plan.id, ChapterOrder = chapter.Order }, ct).ConfigureAwait(false);
                        enqueued++;
                        logger.LogInformation("Recovery: plan {PlanId} chapter {Order} stuck ({AgeMin}m) - content={Content}, quiz={Quiz}, assignment={Assignment}",
                            plan.id, chapter.Order, (int)(now - chapter.LastUpdateUtc).TotalMinutes,
                            chapter.ContentStatus, chapter.QuizStatus, chapter.AssignmentStatus);
                    }
                }

                logger.LogInformation("Generation recovery scan completed. Enqueued {Count} work item(s) for stuck generation.", enqueued);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // shutdown
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Generation recovery job failed");
            }
        }

        /// <summary>
        /// A chapter is stuck only if it has an unfinished artifact AND has made no progress
        /// (no status change) for longer than the threshold. This distinguishes a genuinely
        /// abandoned artifact (e.g. after a crash) from one that is actively being generated.
        /// </summary>
        private bool IsStuck(ChapterOutline c, DateTime now) =>
            HasUnfinishedArtifact(c) && now - c.LastUpdateUtc > stuckThreshold;

        private static bool HasUnfinishedArtifact(ChapterOutline c) =>
            c.ContentStatus == ArtifactStatus.Pending || c.ContentStatus == ArtifactStatus.Generating ||
            c.QuizStatus == ArtifactStatus.Pending || c.QuizStatus == ArtifactStatus.Generating ||
            c.AssignmentStatus == ArtifactStatus.Pending || c.AssignmentStatus == ArtifactStatus.Generating;
    }
}
