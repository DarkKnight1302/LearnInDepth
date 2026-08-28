using LearnInDepth.Models;
using LearnInDepth.Repositories;

namespace LearnInDepth.Services.Generation
{
    /// <summary>
    /// Processes generation work items from the channel one plan at a time.
    /// On startup, re-enqueues plans stuck in Generating (crash recovery). Chapters are generated
    /// strictly sequentially and artifact statuses are persisted in Cosmos, so a whole-plan work item
    /// resumes from the first chapter with unfinished artifacts - the chapter that was in flight when
    /// the app stopped. Generation is idempotent and skips artifacts that already exist.
    /// </summary>
    public class GenerationBackgroundService : BackgroundService
    {
        private readonly IGenerationChannel channel;
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<GenerationBackgroundService> logger;

        public GenerationBackgroundService(
            IGenerationChannel channel,
            IServiceProvider serviceProvider,
            ILogger<GenerationBackgroundService> logger)
        {
            this.channel = channel;
            this.serviceProvider = serviceProvider;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RecoverStalePlansAsync(stoppingToken).ConfigureAwait(false);

            await foreach (GenerationWorkItem workItem in channel.ReadAllAsync(stoppingToken))
            {
                try
                {
                    // Repositories/orchestrator are singletons; resolve from the root provider.
                    IGenerationOrchestrator orchestrator = serviceProvider.GetRequiredService<IGenerationOrchestrator>();
                    logger.LogInformation("Picked up generation work item for plan {PlanId}, chapter {Chapter}",
                        workItem.PlanId, workItem.ChapterOrder?.ToString() ?? "(all)");
                    await orchestrator.GenerateAsync(workItem, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled error processing generation work item for plan {PlanId}", workItem.PlanId);
                }
            }
        }

        private async Task RecoverStalePlansAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Wait briefly so the startup bootstrapper (container creation) finishes first.
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);

                ILearningPlanRepository planRepository = serviceProvider.GetRequiredService<ILearningPlanRepository>();
                List<LearningPlan> plans = await planRepository.ListAllAsync().ConfigureAwait(false);
                foreach (LearningPlan plan in plans)
                {
                    if (plan.Status != GenerationStatus.Generating)
                    {
                        continue;
                    }

                    ChapterOutline resume = plan.Chapters.Where(HasUnfinishedArtifact).OrderBy(c => c.Order).FirstOrDefault();
                    if (resume != null)
                    {
                        // Chapters generate in order and statuses are persisted, so the first chapter
                        // with unfinished artifacts is the one that was in flight at restart. A whole-plan
                        // work item resumes from it, skipping chapters that are already ready.
                        logger.LogInformation("Resuming stale plan {PlanId} generation from chapter {Order}", plan.id, resume.Order);
                    }
                    else
                    {
                        logger.LogInformation("Re-enqueueing stale generating plan {PlanId}", plan.id);
                    }

                    await channel.EnqueueAsync(new GenerationWorkItem { PlanId = plan.id }, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // shutting down
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Stale plan recovery scan failed");
            }
        }

        private static bool HasUnfinishedArtifact(ChapterOutline c) =>
            c.ContentStatus != ArtifactStatus.Ready ||
            c.QuizStatus != ArtifactStatus.Ready ||
            c.AssignmentStatus != ArtifactStatus.Ready;
    }
}
