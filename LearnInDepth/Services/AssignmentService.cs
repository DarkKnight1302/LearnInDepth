using LearnInDepth.ApiModels;
using LearnInDepth.Clients;
using LearnInDepth.Models;
using LearnInDepth.Repositories;
using LearnInDepth.Services.Interfaces;
using LearnInDepth.Services.Prompts;

namespace LearnInDepth.Services
{
    public class AssignmentService : IAssignmentService
    {
        private const int MaxSolutionLength = 20000;

        private readonly IChapterAssignmentRepository assignmentRepository;
        private readonly IAssignmentSubmissionRepository submissionRepository;
        private readonly IOpenCodeCompletionClient llmClient;
        private readonly IUserProgressService progressService;
        private readonly ILogger<AssignmentService> logger;
        private readonly string verificationModel;
        private readonly int verificationMaxTokens;

        public AssignmentService(
            IChapterAssignmentRepository assignmentRepository,
            IAssignmentSubmissionRepository submissionRepository,
            IOpenCodeCompletionClient llmClient,
            IUserProgressService progressService,
            IConfiguration configuration,
            ILogger<AssignmentService> logger)
        {
            this.assignmentRepository = assignmentRepository;
            this.submissionRepository = submissionRepository;
            this.llmClient = llmClient;
            this.progressService = progressService;
            this.logger = logger;
            this.verificationModel = configuration["OpenCode:VerificationModel"] ?? "deepseek-v4-flash";
            this.verificationMaxTokens = configuration.GetValue<int?>("OpenCode:VerificationMaxTokens") ?? 4096;
        }

        public async Task<AssignmentFeedbackResponse> SubmitSolutionAsync(LearningPlan plan, int order, string userId, string solution)
        {
            ChapterAssignment assignment = await assignmentRepository.GetAsync(plan.id, order).ConfigureAwait(false);
            if (assignment == null)
            {
                return null;
            }

            ChapterOutline chapter = plan.Chapters.FirstOrDefault(c => c.Order == order)
                ?? new ChapterOutline { Order = order, Title = assignment.Title };

            string trimmedSolution = solution.Length > MaxSolutionLength ? solution[..MaxSolutionLength] : solution;

            CompletionResult<VerificationResponse> result = await llmClient.SendPromptJsonAsync<VerificationResponse>(
                verificationModel,
                VerificationPromptBuilder.SystemPrompt,
                VerificationPromptBuilder.BuildUserPrompt(plan.Topic, chapter, assignment, trimmedSolution),
                temperature: 0.2,
                maxTokens: verificationMaxTokens).ConfigureAwait(false);

            if (!result.IsSuccess || result.Data == null)
            {
                throw new InvalidOperationException($"Assignment verification failed: {result.ErrorMessage}");
            }

            VerificationResponse feedback = result.Data;
            string verdict = NormalizeVerdict(feedback.Verdict);
            int score = Math.Clamp(feedback.Score, 0, 100);

            var submission = new AssignmentSubmission
            {
                UserId = userId,
                LearningPlanId = plan.id,
                Topic = plan.Topic,
                ChapterOrder = order,
                ChapterTitle = assignment.Title,
                SolutionText = trimmedSolution,
                Verdict = verdict,
                Score = score,
                WhatWentWell = feedback.WhatWentWell ?? new List<string>(),
                Corrections = feedback.Corrections ?? new List<string>(),
                InterviewTips = feedback.InterviewTips ?? string.Empty,
                Model = result.ModelUsed,
                SubmittedAtUtc = DateTime.UtcNow
            };
            await submissionRepository.CreateAsync(submission).ConfigureAwait(false);
            await progressService.RecordAssignmentResultAsync(userId, plan, order, verdict, score).ConfigureAwait(false);

            logger.LogInformation("Assignment verified. User={UserId}, Plan={PlanId}, Chapter={Order}, Verdict={Verdict}, Score={Score}",
                userId, plan.id, order, verdict, score);

            return new AssignmentFeedbackResponse
            {
                Verdict = verdict,
                Score = score,
                WhatWentWell = submission.WhatWentWell,
                Corrections = submission.Corrections,
                InterviewTips = submission.InterviewTips,
                SubmittedAtUtc = submission.SubmittedAtUtc
            };
        }

        private static string NormalizeVerdict(string verdict)
        {
            if (string.IsNullOrWhiteSpace(verdict))
            {
                return "NeedsWork";
            }

            string normalized = verdict.Trim();
            if (normalized.Equals("Pass", StringComparison.OrdinalIgnoreCase)) return "Pass";
            if (normalized.Equals("Fail", StringComparison.OrdinalIgnoreCase)) return "Fail";
            return "NeedsWork";
        }
    }
}
