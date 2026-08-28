using LearnInDepth.Clients;
using LearnInDepth.Models;
using LearnInDepth.Repositories;
using LearnInDepth.Services.Prompts;
using System.Collections.Concurrent;

namespace LearnInDepth.Services.Generation
{
    public class GenerationOrchestrator : IGenerationOrchestrator
    {
        private readonly ILearningPlanRepository planRepository;
        private readonly IOpenCodeCompletionClient llmClient;
        private readonly IChapterGenerator chapterGenerator;
        private readonly ILogger<GenerationOrchestrator> logger;
        private readonly string planModel;
        private readonly int planMaxTokens;
        private readonly int maxSweepPasses;

        // Serializes generation runs per plan so duplicate work items cannot race.
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> PlanGates = new ConcurrentDictionary<string, SemaphoreSlim>();

        public GenerationOrchestrator(
            ILearningPlanRepository planRepository,
            IOpenCodeCompletionClient llmClient,
            IChapterGenerator chapterGenerator,
            IConfiguration configuration,
            ILogger<GenerationOrchestrator> logger)
        {
            this.planRepository = planRepository;
            this.llmClient = llmClient;
            this.chapterGenerator = chapterGenerator;
            this.logger = logger;
            this.planModel = configuration["OpenCode:PlanModel"] ?? "kimi-k3";
            this.planMaxTokens = configuration.GetValue<int?>("OpenCode:PlanMaxTokens") ?? 4096;
            this.maxSweepPasses = configuration.GetValue<int?>("OpenCode:MaxSweepPasses") ?? 2;
        }

        public async Task GenerateAsync(GenerationWorkItem workItem, CancellationToken cancellationToken)
        {
            SemaphoreSlim gate = PlanGates.GetOrAdd(workItem.PlanId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                LearningPlan plan = await planRepository.GetByIdAsync(workItem.PlanId).ConfigureAwait(false);
                if (plan == null)
                {
                    logger.LogWarning("Generation requested for unknown plan {PlanId}", workItem.PlanId);
                    return;
                }

                if (workItem.ChapterOrder.HasValue)
                {
                    await GenerateSingleChapterAsync(plan, workItem.ChapterOrder.Value, cancellationToken).ConfigureAwait(false);
                    return;
                }

                await GenerateWholePlanAsync(plan, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // host shutting down - statuses remain in Cosmos for startup recovery
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Generation run failed for plan {PlanId}", workItem.PlanId);
                await TryMarkPlanFailedAsync(workItem.PlanId, ex.Message).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task GenerateWholePlanAsync(LearningPlan plan, CancellationToken cancellationToken)
        {
            if (plan.Chapters.Count == 0)
            {
                bool planGenerated = await GeneratePlanOutlineAsync(plan, cancellationToken).ConfigureAwait(false);
                if (!planGenerated)
                {
                    return; // plan marked Failed inside
                }
            }

            await GenerateChaptersSequentiallyAsync(plan, plan.Chapters, cancellationToken).ConfigureAwait(false);
            await RunConvergenceSweepsAsync(plan, cancellationToken).ConfigureAwait(false);
            await FinalizePlanStatusAsync(plan).ConfigureAwait(false);
        }

        /// <summary>
        /// Built-in retry sweep: after the initial generation pass, re-runs any chapters that still have
        /// failed or pending artifacts (bounded by MaxSweepPasses). Generation is idempotent, so already-ready
        /// artifacts are skipped. This lets transient LLM failures self-heal without manual intervention.
        /// </summary>
        private async Task RunConvergenceSweepsAsync(LearningPlan plan, CancellationToken cancellationToken)
        {
            for (int pass = 1; pass <= maxSweepPasses; pass++)
            {
                List<ChapterOutline> incomplete = plan.Chapters.Where(HasIncompleteArtifacts).ToList();
                if (incomplete.Count == 0)
                {
                    return;
                }

                logger.LogInformation("Convergence sweep pass {Pass}/{MaxPasses}: retrying {Count} chapters with failed/pending artifacts for plan {PlanId}",
                    pass, maxSweepPasses, incomplete.Count, plan.id);

                await GenerateChaptersSequentiallyAsync(plan, incomplete, cancellationToken).ConfigureAwait(false);

                // Stop early once everything is ready.
                if (plan.Chapters.All(c => !HasIncompleteArtifacts(c)))
                {
                    return;
                }
            }
        }

        private static bool HasIncompleteArtifacts(ChapterOutline chapter) =>
            chapter.ContentStatus != ArtifactStatus.Ready ||
            chapter.QuizStatus != ArtifactStatus.Ready ||
            chapter.AssignmentStatus != ArtifactStatus.Ready;

        private async Task GenerateSingleChapterAsync(LearningPlan plan, int chapterOrder, CancellationToken cancellationToken)
        {
            ChapterOutline chapter = plan.Chapters.FirstOrDefault(c => c.Order == chapterOrder);
            if (chapter == null)
            {
                logger.LogWarning("Chapter {Order} not found in plan {PlanId}", chapterOrder, plan.id);
                return;
            }

            await GenerateChaptersSequentiallyAsync(plan, new List<ChapterOutline> { chapter }, cancellationToken).ConfigureAwait(false);

            // Re-check: if nothing is failed anymore and everything exists, ensure plan shows Ready.
            if (plan.Status == GenerationStatus.Ready || plan.Chapters.All(c =>
                c.ContentStatus != ArtifactStatus.Failed && c.QuizStatus != ArtifactStatus.Failed && c.AssignmentStatus != ArtifactStatus.Failed))
            {
                plan.Status = GenerationStatus.Ready;
                plan.Error = string.Empty;
                await planRepository.UpsertAsync(plan).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Generates chapters strictly in order, one at a time: a chapter is only started after the
        /// previous one has fully completed. Chapters whose artifacts are already Ready are skipped
        /// (idempotent), so a whole-plan run resumes from the first chapter with unfinished work.
        /// </summary>
        private async Task GenerateChaptersSequentiallyAsync(
            LearningPlan plan, IReadOnlyCollection<ChapterOutline> chapters, CancellationToken cancellationToken)
        {
            var planLock = new SemaphoreSlim(1, 1);

            foreach (ChapterOutline chapter in chapters.OrderBy(c => c.Order))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!HasIncompleteArtifacts(chapter))
                {
                    continue;
                }

                try
                {
                    await chapterGenerator.GenerateChapterAsync(plan, chapter, planLock, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Chapter {Order} generation crashed for plan {PlanId}", chapter.Order, plan.id);
                    await planLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        if (chapter.ContentStatus == ArtifactStatus.Generating) chapter.ContentStatus = ArtifactStatus.Failed;
                        if (chapter.QuizStatus == ArtifactStatus.Generating) chapter.QuizStatus = ArtifactStatus.Failed;
                        if (chapter.AssignmentStatus == ArtifactStatus.Generating) chapter.AssignmentStatus = ArtifactStatus.Failed;
                        chapter.Error = ex.Message;
                        await planRepository.UpsertAsync(plan).ConfigureAwait(false);
                    }
                    finally
                    {
                        planLock.Release();
                    }
                }
            }
        }

        private async Task<bool> GeneratePlanOutlineAsync(LearningPlan plan, CancellationToken cancellationToken)
        {
            logger.LogInformation("Generating plan outline for topic '{Topic}' ({PlanId})", plan.Topic, plan.id);

            CompletionResult<PlanGenerationResponse> result = await llmClient.SendPromptJsonAsync<PlanGenerationResponse>(
                planModel,
                PlanPromptBuilder.SystemPrompt,
                PlanPromptBuilder.BuildUserPrompt(plan.Topic),
                temperature: 0.7,
                maxTokens: planMaxTokens,
                cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess || result.Data == null || result.Data.Chapters == null || result.Data.Chapters.Count == 0)
            {
                plan.Status = GenerationStatus.Failed;
                plan.Error = $"Plan generation failed: {result.ErrorMessage}";
                await planRepository.UpsertAsync(plan).ConfigureAwait(false);
                return false;
            }

            var chapters = result.Data.Chapters
                .Where(c => !string.IsNullOrWhiteSpace(c.Title) && !string.IsNullOrWhiteSpace(c.Description))
                .Take(15)
                .Select((c, index) => new ChapterOutline
                {
                    Order = index + 1,
                    Title = c.Title.Trim(),
                    Description = c.Description.Trim(),
                    KeyConcepts = c.KeyConcepts ?? new List<string>(),
                    InterviewFocus = c.InterviewFocus ?? new List<string>(),
                    ContentStatus = ArtifactStatus.Pending,
                    QuizStatus = ArtifactStatus.Pending,
                    AssignmentStatus = ArtifactStatus.Pending
                })
                .ToList();

            if (chapters.Count == 0)
            {
                plan.Status = GenerationStatus.Failed;
                plan.Error = "Plan generation produced no usable chapters.";
                await planRepository.UpsertAsync(plan).ConfigureAwait(false);
                return false;
            }

            plan.Chapters = chapters;
            plan.GenerationUpdatedAtUtc = DateTime.UtcNow;
            foreach (var ch in chapters) ch.LastUpdateUtc = DateTime.UtcNow;
            await planRepository.UpsertAsync(plan).ConfigureAwait(false);
            logger.LogInformation("Plan {PlanId} outline saved with {Count} chapters", plan.id, chapters.Count);
            return true;
        }

        private async Task FinalizePlanStatusAsync(LearningPlan plan)
        {
            bool allArtifactsFailed = plan.Chapters.All(c =>
                c.ContentStatus == ArtifactStatus.Failed && c.QuizStatus == ArtifactStatus.Failed && c.AssignmentStatus == ArtifactStatus.Failed);

            if (allArtifactsFailed)
            {
                plan.Status = GenerationStatus.Failed;
                plan.Error = "All chapter generations failed. See per-chapter errors and retry.";
            }
            else
            {
                plan.Status = GenerationStatus.Ready;
                plan.Error = string.Empty;
                plan.CompletedAtUtc = DateTime.UtcNow;
            }
            plan.GenerationUpdatedAtUtc = DateTime.UtcNow;
            await planRepository.UpsertAsync(plan).ConfigureAwait(false);
            logger.LogInformation("Plan {PlanId} finalized with status {Status}", plan.id, plan.Status);
        }

        private async Task TryMarkPlanFailedAsync(string planId, string error)
        {
            try
            {
                LearningPlan plan = await planRepository.GetByIdAsync(planId).ConfigureAwait(false);
                if (plan != null && plan.Status == GenerationStatus.Generating)
                {
                    plan.Status = GenerationStatus.Failed;
                    plan.Error = error;
                    await planRepository.UpsertAsync(plan).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to mark plan {PlanId} as Failed", planId);
            }
        }
    }
}
