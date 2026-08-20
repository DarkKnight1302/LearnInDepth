using LearnInDepth.Clients;
using LearnInDepth.Models;
using LearnInDepth.Repositories;
using LearnInDepth.Services.Prompts;
using System.Text.RegularExpressions;

namespace LearnInDepth.Services.Generation
{
    public class ChapterGenerator : IChapterGenerator
    {
        private enum ArtifactKind { Content, Quiz, Assignment }

        private readonly IOpenCodeCompletionClient llmClient;
        private readonly ILearningPlanRepository planRepository;
        private readonly IChapterContentRepository contentRepository;
        private readonly IChapterQuizRepository quizRepository;
        private readonly IChapterAssignmentRepository assignmentRepository;
        private readonly ILogger<ChapterGenerator> logger;
        private readonly string contentModel;
        private readonly string quizModel;
        private readonly string assignmentModel;
        private readonly int contentMaxTokens;
        private readonly int artifactMaxTokens;

        public ChapterGenerator(
            IOpenCodeCompletionClient llmClient,
            ILearningPlanRepository planRepository,
            IChapterContentRepository contentRepository,
            IChapterQuizRepository quizRepository,
            IChapterAssignmentRepository assignmentRepository,
            IConfiguration configuration,
            ILogger<ChapterGenerator> logger)
        {
            this.llmClient = llmClient;
            this.planRepository = planRepository;
            this.contentRepository = contentRepository;
            this.quizRepository = quizRepository;
            this.assignmentRepository = assignmentRepository;
            this.logger = logger;
            this.contentModel = configuration["OpenCode:ContentModel"] ?? "kimi-k3";
            this.quizModel = configuration["OpenCode:QuizModel"] ?? "deepseek-v4-flash";
            this.assignmentModel = configuration["OpenCode:AssignmentModel"] ?? "deepseek-v4-flash";
            this.contentMaxTokens = configuration.GetValue<int?>("OpenCode:ContentMaxTokens") ?? 16000;
            this.artifactMaxTokens = configuration.GetValue<int?>("OpenCode:ArtifactMaxTokens") ?? 4096;
        }

        public async Task GenerateChapterAsync(
            LearningPlan plan,
            ChapterOutline chapter,
            SemaphoreSlim planLock,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Generating chapter {Order} '{Title}' for plan {PlanId}", chapter.Order, chapter.Title, plan.id);

            string contentExcerpt = await GenerateContentAsync(plan, chapter, planLock, cancellationToken).ConfigureAwait(false);

            // Quiz and assignment run in parallel, grounded in the chapter outline + content excerpt.
            Task quizTask = GenerateQuizAsync(plan, chapter, contentExcerpt, planLock, cancellationToken);
            Task assignmentTask = GenerateAssignmentAsync(plan, chapter, contentExcerpt, planLock, cancellationToken);
            await Task.WhenAll(quizTask, assignmentTask).ConfigureAwait(false);
        }

        private async Task<string> GenerateContentAsync(
            LearningPlan plan, ChapterOutline chapter, SemaphoreSlim planLock, CancellationToken cancellationToken)
        {
            ChapterContent existing = await contentRepository.GetAsync(plan.id, chapter.Order).ConfigureAwait(false);
            if (existing != null && !string.IsNullOrWhiteSpace(existing.HtmlContent))
            {
                await SetArtifactStatusAsync(plan, chapter, ArtifactKind.Content, ArtifactStatus.Ready, string.Empty, planLock).ConfigureAwait(false);
                return BuildExcerpt(existing.HtmlContent);
            }

            await SetArtifactStatusAsync(plan, chapter, ArtifactKind.Content, ArtifactStatus.Generating, string.Empty, planLock).ConfigureAwait(false);

            try
            {
                CompletionResult result = await llmClient.SendPromptTextAsync(
                    contentModel,
                    ChapterContentPromptBuilder.SystemPrompt,
                    ChapterContentPromptBuilder.BuildUserPrompt(plan.Topic, plan, chapter),
                    temperature: 0.7,
                    maxTokens: contentMaxTokens,
                    cancellationToken).ConfigureAwait(false);

                if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Text))
                {
                    throw new InvalidOperationException($"Content generation failed: {result.ErrorMessage}");
                }

                string html = SanitizeHtml(result.Text);
                var content = new ChapterContent
                {
                    id = ChapterContentRepository.BuildId(plan.id, chapter.Order),
                    LearningPlanId = plan.id,
                    Order = chapter.Order,
                    Title = chapter.Title,
                    HtmlContent = html,
                    Model = result.ModelUsed,
                    GeneratedAtUtc = DateTime.UtcNow
                };
                await contentRepository.UpsertAsync(content).ConfigureAwait(false);

                await SetArtifactStatusAsync(plan, chapter, ArtifactKind.Content, ArtifactStatus.Ready, string.Empty, planLock).ConfigureAwait(false);
                return BuildExcerpt(html);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Content generation failed for plan {PlanId} chapter {Order}", plan.id, chapter.Order);
                await SetArtifactStatusAsync(plan, chapter, ArtifactKind.Content, ArtifactStatus.Failed, ex.Message, planLock).ConfigureAwait(false);
                return string.Empty;
            }
        }

        private async Task GenerateQuizAsync(
            LearningPlan plan, ChapterOutline chapter, string contentExcerpt, SemaphoreSlim planLock, CancellationToken cancellationToken)
        {
            ChapterQuiz existing = await quizRepository.GetAsync(plan.id, chapter.Order).ConfigureAwait(false);
            if (existing != null && existing.Questions.Count > 0)
            {
                await SetArtifactStatusAsync(plan, chapter, ArtifactKind.Quiz, ArtifactStatus.Ready, string.Empty, planLock).ConfigureAwait(false);
                return;
            }

            await SetArtifactStatusAsync(plan, chapter, ArtifactKind.Quiz, ArtifactStatus.Generating, string.Empty, planLock).ConfigureAwait(false);

            try
            {
                CompletionResult<QuizGenerationResponse> result = await llmClient.SendPromptJsonAsync<QuizGenerationResponse>(
                    quizModel,
                    QuizPromptBuilder.SystemPrompt,
                    QuizPromptBuilder.BuildUserPrompt(plan.Topic, chapter, contentExcerpt),
                    temperature: 0.5,
                    maxTokens: artifactMaxTokens,
                    cancellationToken).ConfigureAwait(false);

                if (!result.IsSuccess || result.Data == null)
                {
                    throw new InvalidOperationException($"Quiz generation failed: {result.ErrorMessage}");
                }

                ChapterQuiz quiz = BuildQuiz(plan, chapter, result.Data);
                await quizRepository.UpsertAsync(quiz).ConfigureAwait(false);

                await SetArtifactStatusAsync(plan, chapter, ArtifactKind.Quiz, ArtifactStatus.Ready, string.Empty, planLock).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Quiz generation failed for plan {PlanId} chapter {Order}", plan.id, chapter.Order);
                await SetArtifactStatusAsync(plan, chapter, ArtifactKind.Quiz, ArtifactStatus.Failed, ex.Message, planLock).ConfigureAwait(false);
            }
        }

        private async Task GenerateAssignmentAsync(
            LearningPlan plan, ChapterOutline chapter, string contentExcerpt, SemaphoreSlim planLock, CancellationToken cancellationToken)
        {
            ChapterAssignment existing = await assignmentRepository.GetAsync(plan.id, chapter.Order).ConfigureAwait(false);
            if (existing != null && !string.IsNullOrWhiteSpace(existing.ProblemStatement))
            {
                await SetArtifactStatusAsync(plan, chapter, ArtifactKind.Assignment, ArtifactStatus.Ready, string.Empty, planLock).ConfigureAwait(false);
                return;
            }

            await SetArtifactStatusAsync(plan, chapter, ArtifactKind.Assignment, ArtifactStatus.Generating, string.Empty, planLock).ConfigureAwait(false);

            try
            {
                CompletionResult<AssignmentGenerationResponse> result = await llmClient.SendPromptJsonAsync<AssignmentGenerationResponse>(
                    assignmentModel,
                    AssignmentPromptBuilder.SystemPrompt,
                    AssignmentPromptBuilder.BuildUserPrompt(plan.Topic, chapter, contentExcerpt),
                    temperature: 0.6,
                    maxTokens: artifactMaxTokens,
                    cancellationToken).ConfigureAwait(false);

                if (!result.IsSuccess || result.Data == null)
                {
                    throw new InvalidOperationException($"Assignment generation failed: {result.ErrorMessage}");
                }

                ChapterAssignment assignment = BuildAssignment(plan, chapter, result.Data);
                await assignmentRepository.UpsertAsync(assignment).ConfigureAwait(false);

                await SetArtifactStatusAsync(plan, chapter, ArtifactKind.Assignment, ArtifactStatus.Ready, string.Empty, planLock).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Assignment generation failed for plan {PlanId} chapter {Order}", plan.id, chapter.Order);
                await SetArtifactStatusAsync(plan, chapter, ArtifactKind.Assignment, ArtifactStatus.Failed, ex.Message, planLock).ConfigureAwait(false);
            }
        }

        private async Task SetArtifactStatusAsync(
            LearningPlan plan,
            ChapterOutline chapter,
            ArtifactKind kind,
            ArtifactStatus status,
            string error,
            SemaphoreSlim planLock)
        {
            await planLock.WaitAsync().ConfigureAwait(false);
            try
            {
                switch (kind)
                {
                    case ArtifactKind.Content: chapter.ContentStatus = status; break;
                    case ArtifactKind.Quiz: chapter.QuizStatus = status; break;
                    case ArtifactKind.Assignment: chapter.AssignmentStatus = status; break;
                }
                chapter.Error = status == ArtifactStatus.Failed ? error : string.Empty;
                await planRepository.UpsertAsync(plan).ConfigureAwait(false);
            }
            finally
            {
                planLock.Release();
            }
        }

        private static ChapterQuiz BuildQuiz(LearningPlan plan, ChapterOutline chapter, QuizGenerationResponse response)
        {
            var quiz = new ChapterQuiz
            {
                id = ChapterContentRepository.BuildId(plan.id, chapter.Order),
                LearningPlanId = plan.id,
                Order = chapter.Order,
                Title = chapter.Title,
                GeneratedAtUtc = DateTime.UtcNow
            };

            int questionNumber = 1;
            foreach (QuizQuestionDto dto in response.Questions ?? new List<QuizQuestionDto>())
            {
                if (string.IsNullOrWhiteSpace(dto.Question) || dto.Options == null || dto.Options.Count < 2
                    || dto.CorrectOptionIndex < 0 || dto.CorrectOptionIndex >= dto.Options.Count)
                {
                    continue; // skip malformed questions
                }

                quiz.Questions.Add(new QuizQuestion
                {
                    QuestionNumber = questionNumber++,
                    Question = dto.Question.Trim(),
                    Options = dto.Options.Select(o => o.Trim()).ToList(),
                    CorrectOptionIndex = dto.CorrectOptionIndex,
                    Explanation = dto.Explanation ?? string.Empty,
                    Difficulty = string.IsNullOrWhiteSpace(dto.Difficulty) ? "Medium" : dto.Difficulty,
                    InterviewStyle = dto.InterviewStyle
                });
            }

            if (quiz.Questions.Count == 0)
            {
                throw new InvalidOperationException("Quiz generation produced no valid questions.");
            }
            return quiz;
        }

        private static ChapterAssignment BuildAssignment(LearningPlan plan, ChapterOutline chapter, AssignmentGenerationResponse response)
        {
            if (string.IsNullOrWhiteSpace(response.ProblemStatement) || response.Tasks == null || response.Tasks.Count == 0
                || response.EvaluationRubric == null || response.EvaluationRubric.Count == 0)
            {
                throw new InvalidOperationException("Assignment generation produced an incomplete assignment.");
            }

            return new ChapterAssignment
            {
                id = ChapterContentRepository.BuildId(plan.id, chapter.Order),
                LearningPlanId = plan.id,
                Order = chapter.Order,
                Title = string.IsNullOrWhiteSpace(response.Title) ? $"Chapter {chapter.Order} challenge" : response.Title.Trim(),
                ProblemStatement = response.ProblemStatement.Trim(),
                Tasks = response.Tasks.Select(t => t.Trim()).ToList(),
                Hints = (response.Hints ?? new List<string>()).Select(h => h.Trim()).ToList(),
                EvaluationRubric = response.EvaluationRubric.Select(r => r.Trim()).ToList(),
                ExpectedOutcome = response.ExpectedOutcome ?? string.Empty,
                GeneratedAtUtc = DateTime.UtcNow
            };
        }

        private static string SanitizeHtml(string html)
        {
            string sanitized = OpenCodeCompletionClient.StripCodeFences(html);
            // Belt and braces: drop full-document wrappers if the model emits them.
            sanitized = Regex.Replace(sanitized, "(?is)^\\s*<!DOCTYPE[^>]*>", string.Empty).Trim();
            return sanitized;
        }

        private static string BuildExcerpt(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return "(chapter content not yet available - ground the output in the chapter scope and key concepts)";
            }

            string withoutScripts = Regex.Replace(html, "(?is)<script\\b.*?</script>", " ");
            string withoutStyles = Regex.Replace(withoutScripts, "(?is)<style\\b.*?</style>", " ");
            string text = Regex.Replace(withoutStyles, "(?s)<[^>]+>", " ");
            text = Regex.Replace(text, "\\s+", " ").Trim();
            return text.Length <= 3500 ? text : text[..3500];
        }
    }
}
