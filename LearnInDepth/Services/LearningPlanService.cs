using LearnInDepth.ApiModels;
using LearnInDepth.Models;
using LearnInDepth.Repositories;
using LearnInDepth.Services.Generation;
using LearnInDepth.Services.Interfaces;
using Microsoft.Azure.Cosmos;
using System.Text.RegularExpressions;

namespace LearnInDepth.Services
{
    public class LearningPlanService : ILearningPlanService
    {
        private readonly ILearningPlanRepository planRepository;
        private readonly IChapterContentRepository contentRepository;
        private readonly IChapterQuizRepository quizRepository;
        private readonly IChapterAssignmentRepository assignmentRepository;
        private readonly IGenerationChannel generationChannel;
        private readonly IGenerationOrchestrator generationOrchestrator;
        private readonly ILogger<LearningPlanService> logger;

        public LearningPlanService(
            ILearningPlanRepository planRepository,
            IChapterContentRepository contentRepository,
            IChapterQuizRepository quizRepository,
            IChapterAssignmentRepository assignmentRepository,
            IGenerationChannel generationChannel,
            IGenerationOrchestrator generationOrchestrator,
            ILogger<LearningPlanService> logger)
        {
            this.planRepository = planRepository;
            this.contentRepository = contentRepository;
            this.quizRepository = quizRepository;
            this.assignmentRepository = assignmentRepository;
            this.generationChannel = generationChannel;
            this.generationOrchestrator = generationOrchestrator;
            this.logger = logger;
        }

        /// <summary>
        /// Explicitly drains the generation queue and processes every queued work item. Nothing runs
        /// automatically - generation only happens when this is invoked.
        /// </summary>
        public async Task<int> RunQueuedGenerationsAsync(CancellationToken cancellationToken)
        {
            int processed = 0;
            foreach (GenerationWorkItem workItem in generationChannel.TryDrainAll())
            {
                logger.LogInformation("Processing queued generation work item for plan {PlanId}, chapter {Chapter}",
                    workItem.PlanId, workItem.ChapterOrder?.ToString() ?? "(all)");
                await generationOrchestrator.GenerateAsync(workItem, cancellationToken).ConfigureAwait(false);
                processed++;
            }
            return processed;
        }

        public async Task<TopicSubmissionResult> SubmitTopicAsync(string topic, string userId)
        {
            string slug = NormalizeTopic(topic);
            LearningPlan existing = await planRepository.GetByIdAsync(slug).ConfigureAwait(false);

            if (existing != null && existing.Status == GenerationStatus.Ready)
            {
                return new TopicSubmissionResult { Outcome = TopicSubmissionOutcome.Ready, Plan = existing };
            }

            if (existing != null && existing.Status == GenerationStatus.Generating)
            {
                return new TopicSubmissionResult { Outcome = TopicSubmissionOutcome.Generating, Plan = existing };
            }

            if (existing != null && existing.Status == GenerationStatus.Failed)
            {
                // Regenerate: reset and re-enqueue. Chapter artifacts that already exist are skipped (idempotent).
                existing.Status = GenerationStatus.Generating;
                existing.Error = string.Empty;
                existing.CompletedAtUtc = null;
                await planRepository.UpsertAsync(existing).ConfigureAwait(false);
                await generationChannel.EnqueueAsync(new GenerationWorkItem { PlanId = slug }).ConfigureAwait(false);
                logger.LogInformation("Re-enqueued failed plan {PlanId} for regeneration", slug);
                return new TopicSubmissionResult { Outcome = TopicSubmissionOutcome.Accepted, Plan = existing };
            }

            var plan = new LearningPlan
            {
                id = slug,
                Topic = topic.Trim(),
                Status = GenerationStatus.Generating,
                CreatedBy = userId,
                CreatedAtUtc = DateTime.UtcNow
            };

            try
            {
                await planRepository.CreateAsync(plan).ConfigureAwait(false);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // Another request created the same topic first.
                LearningPlan raced = await planRepository.GetByIdAsync(slug).ConfigureAwait(false);
                return new TopicSubmissionResult { Outcome = TopicSubmissionOutcome.Generating, Plan = raced };
            }

            await generationChannel.EnqueueAsync(new GenerationWorkItem { PlanId = slug }).ConfigureAwait(false);
            logger.LogInformation("Accepted new topic '{Topic}' ({PlanId}) for generation", topic, slug);
            return new TopicSubmissionResult { Outcome = TopicSubmissionOutcome.Accepted, Plan = plan };
        }

        public async Task<LearningPlan> GetPlanAsync(string slug)
        {
            return await planRepository.GetByIdAsync(NormalizeTopic(slug)).ConfigureAwait(false);
        }

        /// <summary>
        /// Computes the real generation status of every chapter by verifying whether the actual
        /// content/quiz/assignment documents exist, then overlaying the true in-flight state
        /// (queued work items + the plan currently being processed by the orchestrator). Persisted
        /// status fields alone can be stale - e.g. an artifact marked Ready whose document was never
        /// written, or a Failed/Pending artifact that actually exists.
        /// </summary>
        public async Task<TopicStatusResponse> GetRealTimeStatusAsync(LearningPlan plan)
        {
            GenerationQueueStatus queue = generationChannel.GetQueueStatus(plan.id);
            bool inFlight = generationOrchestrator.IsGenerating(plan.id);
            ChapterOutline[] chapters = plan.Chapters.OrderBy(c => c.Order).ToArray();

            Task<ChapterStatusDto>[] chapterTasks = chapters.Select(async c =>
            {
                Task<ChapterContent> contentTask = contentRepository.GetAsync(plan.id, c.Order);
                Task<ChapterQuiz> quizTask = quizRepository.GetAsync(plan.id, c.Order);
                Task<ChapterAssignment> assignmentTask = assignmentRepository.GetAsync(plan.id, c.Order);
                await Task.WhenAll(contentTask, quizTask, assignmentTask).ConfigureAwait(false);

                bool hasWork = queue.HasWorkForChapter(c.Order);
                string contentStatus = ResolveStatus(c.ContentStatus, IsContentReady(contentTask.Result), hasWork, inFlight);
                string quizStatus = ResolveStatus(c.QuizStatus, IsQuizReady(quizTask.Result), hasWork, inFlight);
                string assignmentStatus = ResolveStatus(c.AssignmentStatus, IsAssignmentReady(assignmentTask.Result), hasWork, inFlight);
                bool failed = contentStatus == ArtifactStatus.Failed.ToString()
                    || quizStatus == ArtifactStatus.Failed.ToString()
                    || assignmentStatus == ArtifactStatus.Failed.ToString();

                return new ChapterStatusDto
                {
                    Order = c.Order,
                    Title = c.Title,
                    ContentStatus = contentStatus,
                    QuizStatus = quizStatus,
                    AssignmentStatus = assignmentStatus,
                    Error = failed ? c.Error : string.Empty
                };
            }).ToArray();

            ChapterStatusDto[] statuses = await Task.WhenAll(chapterTasks).ConfigureAwait(false);

            int readyChapters = statuses.Count(cs =>
                cs.ContentStatus == ArtifactStatus.Ready.ToString()
                && cs.QuizStatus == ArtifactStatus.Ready.ToString()
                && cs.AssignmentStatus == ArtifactStatus.Ready.ToString());
            int failedChapters = statuses.Count(cs =>
                cs.ContentStatus == ArtifactStatus.Failed.ToString()
                || cs.QuizStatus == ArtifactStatus.Failed.ToString()
                || cs.AssignmentStatus == ArtifactStatus.Failed.ToString());

            int totalArtifacts = chapters.Length * 3;
            int readyArtifacts = statuses.Sum(cs =>
                (cs.ContentStatus == ArtifactStatus.Ready.ToString() ? 1 : 0)
                + (cs.QuizStatus == ArtifactStatus.Ready.ToString() ? 1 : 0)
                + (cs.AssignmentStatus == ArtifactStatus.Ready.ToString() ? 1 : 0));

            // A plan is only truly "Generating" when there is queued work for it or the orchestrator
            // is actively processing it. A persisted Generating status with no queued/in-flight work
            // is a stale/interrupted run, so surface it as Pending rather than claiming activity.
            bool active = queue.HasWork || inFlight;

            string planStatus = chapters.Length == 0
                ? (active ? GenerationStatus.Generating.ToString() : ArtifactStatus.Pending.ToString())
                : readyChapters == chapters.Length
                    ? GenerationStatus.Ready.ToString()
                    : statuses.Any(cs => cs.ContentStatus == ArtifactStatus.Generating.ToString()
                        || cs.QuizStatus == ArtifactStatus.Generating.ToString()
                        || cs.AssignmentStatus == ArtifactStatus.Generating.ToString())
                        ? GenerationStatus.Generating.ToString()
                        : failedChapters > 0
                            ? GenerationStatus.Failed.ToString()
                            : (active ? GenerationStatus.Generating.ToString() : ArtifactStatus.Pending.ToString());

            return new TopicStatusResponse
            {
                Slug = plan.id,
                Topic = plan.Topic,
                Status = planStatus,
                TotalChapters = chapters.Length,
                ReadyChapters = readyChapters,
                FailedChapters = failedChapters,
                PercentComplete = totalArtifacts == 0 ? 0 : (int)Math.Round(100.0 * readyArtifacts / totalArtifacts),
                Error = plan.Error,
                Chapters = statuses.ToList()
            };
        }

        private static bool IsContentReady(ChapterContent content) =>
            content != null && !string.IsNullOrWhiteSpace(content.HtmlContent);

        private static bool IsQuizReady(ChapterQuiz quiz) =>
            quiz != null && quiz.Questions.Count > 0;

        private static bool IsAssignmentReady(ChapterAssignment assignment) =>
            assignment != null && !string.IsNullOrWhiteSpace(assignment.ProblemStatement) && assignment.Tasks.Count > 0;

        /// <summary>
        /// Resolves the real status of one artifact. Ready is authoritative (the document exists).
        /// Otherwise the artifact is Generating only while work for it is actually queued or the
        /// plan is being processed and the persisted status says Generating. A persisted Generating
        /// status with no queued/in-flight work is a crashed run, so it surfaces as Pending rather
        /// than a permanently stale "Generating".
        /// </summary>
        private static string ResolveStatus(ArtifactStatus dbStatus, bool artifactReady, bool hasWork, bool inFlight)
        {
            if (artifactReady) return ArtifactStatus.Ready.ToString();
            if (hasWork || (inFlight && dbStatus == ArtifactStatus.Generating)) return ArtifactStatus.Generating.ToString();
            if (dbStatus == ArtifactStatus.Failed) return ArtifactStatus.Failed.ToString();
            return ArtifactStatus.Pending.ToString();
        }

        public async Task<ChapterContent> GetChapterContentAsync(string slug, int order)
        {
            return await contentRepository.GetAsync(NormalizeTopic(slug), order).ConfigureAwait(false);
        }

        public async Task<ChapterQuiz> GetChapterQuizAsync(string slug, int order)
        {
            return await quizRepository.GetAsync(NormalizeTopic(slug), order).ConfigureAwait(false);
        }

        public async Task<ChapterAssignment> GetChapterAssignmentAsync(string slug, int order)
        {
            return await assignmentRepository.GetAsync(NormalizeTopic(slug), order).ConfigureAwait(false);
        }

        public async Task<List<LearningPlan>> ListTopicsAsync()
        {
            return await planRepository.ListAllAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Lightweight truth for the topic list (avoids per-artifact reads). A plan is Generating only
        /// while it has queued work or is actively being processed; otherwise report its persisted
        /// Ready/Failed, or Pending for a stale/interrupted Generating flag.
        /// </summary>
        public string GetPlanStatusLabel(LearningPlan plan)
        {
            bool active = generationChannel.GetQueueStatus(plan.id).HasWork || generationOrchestrator.IsGenerating(plan.id);
            if (active) return GenerationStatus.Generating.ToString();
            if (plan.Status == GenerationStatus.Ready) return GenerationStatus.Ready.ToString();
            if (plan.Status == GenerationStatus.Failed) return GenerationStatus.Failed.ToString();
            return ArtifactStatus.Pending.ToString();
        }

        public async Task<bool> RetryChapterAsync(string slug, int order)
        {
            slug = NormalizeTopic(slug);
            LearningPlan plan = await planRepository.GetByIdAsync(slug).ConfigureAwait(false);
            if (plan == null)
            {
                return false;
            }

            ChapterOutline chapter = plan.Chapters.FirstOrDefault(c => c.Order == order);
            if (chapter == null)
            {
                return false;
            }

            // Decide what actually needs regenerating from the real state of the artifact documents,
            // not the persisted status flags - those can be stale (e.g. DB says Ready but the
            // content/quiz/assignment document is missing, or vice versa).
            Task<ChapterContent> contentTask = contentRepository.GetAsync(plan.id, order);
            Task<ChapterQuiz> quizTask = quizRepository.GetAsync(plan.id, order);
            Task<ChapterAssignment> assignmentTask = assignmentRepository.GetAsync(plan.id, order);
            await Task.WhenAll(contentTask, quizTask, assignmentTask).ConfigureAwait(false);

            bool needsContent = NeedsGeneration(contentTask.Result, chapter.ContentStatus, IsContentReady);
            bool needsQuiz = NeedsGeneration(quizTask.Result, chapter.QuizStatus, IsQuizReady);
            bool needsAssignment = NeedsGeneration(assignmentTask.Result, chapter.AssignmentStatus, IsAssignmentReady);

            if (!needsContent && !needsQuiz && !needsAssignment)
            {
                return false;
            }

            // Reflect the in-flight state immediately so the status page shows Generating
            // instead of the stale Ready/Failed until the worker picks up the item.
            if (needsContent) chapter.ContentStatus = ArtifactStatus.Generating;
            if (needsQuiz) chapter.QuizStatus = ArtifactStatus.Generating;
            if (needsAssignment) chapter.AssignmentStatus = ArtifactStatus.Generating;

            chapter.Error = string.Empty;
            plan.Status = GenerationStatus.Generating;
            await planRepository.UpsertAsync(plan).ConfigureAwait(false);
            await generationChannel.EnqueueAsync(new GenerationWorkItem { PlanId = slug, ChapterOrder = order }).ConfigureAwait(false);
            logger.LogInformation("Enqueued retry for plan {PlanId} chapter {Order}", slug, order);
            return true;
        }

        private static bool NeedsGeneration<T>(T artifact, ArtifactStatus dbStatus, Func<T, bool> isReady)
        {
            // Regenerate when the document is missing or incomplete, or the persisted status says so.
            return !isReady(artifact) || dbStatus == ArtifactStatus.Failed || dbStatus == ArtifactStatus.Pending;
        }

        public string NormalizeTopic(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                return string.Empty;
            }

            string slug = topic.Trim().ToLowerInvariant();
            slug = Regex.Replace(slug, "[^a-z0-9]+", "-");
            slug = slug.Trim('-');
            return slug.Length <= 200 ? slug : slug[..200].Trim('-');
        }
    }
}
