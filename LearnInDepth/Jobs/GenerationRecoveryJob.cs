using LearnInDepth.Models;
using LearnInDepth.Repositories;
using LearnInDepth.Services.Generation;
using Quartz;

namespace LearnInDepth.Jobs
{
    /// <summary>
    /// Periodic recovery job (every 10 minutes). If the app restarts while a chapter is being
    /// generated, the in-memory generation channel is lost and artifacts can be left stuck in
    /// Pending or Generating. This job re-enqueues work items for any chapter with an unfinished
    /// artifact. Generation is idempotent (already-ready artifacts are skipped) and the per-plan
    /// gate serializes runs, so re-enqueuing is safe.
    /// </summary>
    [DisallowConcurrentExecution]
    public class GenerationRecoveryJob : IJob
    {
        private readonly ILearningPlanRepository planRepository;
        private readonly IGenerationChannel generationChannel;
        private readonly ILogger<GenerationRecoveryJob> logger;

        public GenerationRecoveryJob(
            ILearningPlanRepository planRepository,
            IGenerationChannel generationChannel,
            ILogger<GenerationRecoveryJob> logger)
        {
            this.planRepository = planRepository;
            this.generationChannel = generationChannel;
            this.logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            CancellationToken ct = context.CancellationToken;
            try
            {
                List<LearningPlan> plans = await planRepository.ListAllAsync().ConfigureAwait(false);
                int enqueued = 0;

                foreach (LearningPlan plan in plans)
                {
                    if (plan.Status == GenerationStatus.Generating && plan.Chapters.Count == 0)
                    {
                        // Plan outline never completed - restart the whole plan.
                        await generationChannel.EnqueueAsync(new GenerationWorkItem { PlanId = plan.id }, ct).ConfigureAwait(false);
                        enqueued++;
                        logger.LogInformation("Recovery: plan {PlanId} has no chapters; re-enqueuing whole plan", plan.id);
                        continue;
                    }

                    foreach (ChapterOutline chapter in plan.Chapters.Where(HasUnfinishedArtifact))
                    {
                        await generationChannel.EnqueueAsync(new GenerationWorkItem { PlanId = plan.id, ChapterOrder = chapter.Order }, ct).ConfigureAwait(false);
                        enqueued++;
                        logger.LogInformation("Recovery: re-enqueuing plan {PlanId} chapter {Order} (content={Content}, quiz={Quiz}, assignment={Assignment})",
                            plan.id, chapter.Order, chapter.ContentStatus, chapter.QuizStatus, chapter.AssignmentStatus);
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

        private static bool HasUnfinishedArtifact(ChapterOutline c) =>
            c.ContentStatus == ArtifactStatus.Pending || c.ContentStatus == ArtifactStatus.Generating ||
            c.QuizStatus == ArtifactStatus.Pending || c.QuizStatus == ArtifactStatus.Generating ||
            c.AssignmentStatus == ArtifactStatus.Pending || c.AssignmentStatus == ArtifactStatus.Generating;
    }
}
