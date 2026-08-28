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

            bool hasFailedArtifact =
                chapter.ContentStatus == ArtifactStatus.Failed || chapter.ContentStatus == ArtifactStatus.Pending ||
                chapter.QuizStatus == ArtifactStatus.Failed || chapter.QuizStatus == ArtifactStatus.Pending ||
                chapter.AssignmentStatus == ArtifactStatus.Failed || chapter.AssignmentStatus == ArtifactStatus.Pending;

            if (!hasFailedArtifact)
            {
                return false;
            }

            // Reflect the in-flight state immediately so the status page shows Generating
            // instead of the stale Ready/Failed until the worker picks up the item.
            if (chapter.ContentStatus == ArtifactStatus.Failed || chapter.ContentStatus == ArtifactStatus.Pending)
                chapter.ContentStatus = ArtifactStatus.Generating;
            if (chapter.QuizStatus == ArtifactStatus.Failed || chapter.QuizStatus == ArtifactStatus.Pending)
                chapter.QuizStatus = ArtifactStatus.Generating;
            if (chapter.AssignmentStatus == ArtifactStatus.Failed || chapter.AssignmentStatus == ArtifactStatus.Pending)
                chapter.AssignmentStatus = ArtifactStatus.Generating;

            chapter.Error = string.Empty;
            plan.Status = GenerationStatus.Generating;
            await planRepository.UpsertAsync(plan).ConfigureAwait(false);
            await generationChannel.EnqueueAsync(new GenerationWorkItem { PlanId = slug, ChapterOrder = order }).ConfigureAwait(false);
            logger.LogInformation("Enqueued retry for plan {PlanId} chapter {Order}", slug, order);
            return true;
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
