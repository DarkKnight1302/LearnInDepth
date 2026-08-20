using LearnInDepth.ApiModels;
using LearnInDepth.Models;
using LearnInDepth.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewHorizonLib.Attributes;
using NewHorizonLib.Services.Interfaces;

namespace LearnInDepth.Controllers
{
    [Route("api/learn")]
    [ApiController]
    [Authorize]
    public class LearningPlanController : ControllerBase
    {
        private const int MaxTopicLength = 200;
        private const int MaxSolutionLength = 50000;

        private readonly ILearningPlanService learningPlanService;
        private readonly IQuizService quizService;
        private readonly IAssignmentService assignmentService;
        private readonly IUserProgressService progressService;
        private readonly ITokenService tokenService;
        private readonly ILogger<LearningPlanController> logger;

        public LearningPlanController(
            ILearningPlanService learningPlanService,
            IQuizService quizService,
            IAssignmentService assignmentService,
            IUserProgressService progressService,
            ITokenService tokenService,
            ILogger<LearningPlanController> logger)
        {
            this.learningPlanService = learningPlanService;
            this.quizService = quizService;
            this.assignmentService = assignmentService;
            this.progressService = progressService;
            this.tokenService = tokenService;
            this.logger = logger;
        }

        [HttpPost("topics")]
        [RateLimit(10, 60)]
        public async Task<IActionResult> SubmitTopic(SubmitTopicRequest request)
        {
            string userId = ValidateAuth();
            if (userId == null) return Unauthorized();

            if (request == null || string.IsNullOrWhiteSpace(request.Topic))
            {
                return BadRequest(new { success = false, message = "Topic is required" });
            }
            if (request.Topic.Trim().Length > MaxTopicLength)
            {
                return BadRequest(new { success = false, message = $"Topic must be {MaxTopicLength} characters or fewer" });
            }

            TopicSubmissionResult result = await learningPlanService.SubmitTopicAsync(request.Topic, userId);
            LearningPlan plan = result.Plan;

            var response = new TopicSubmissionResponse
            {
                Topic = plan.Topic,
                Slug = plan.id,
                Status = plan.Status.ToString(),
                Message = result.Outcome switch
                {
                    TopicSubmissionOutcome.Ready => "A learning plan for this topic already exists and is ready.",
                    TopicSubmissionOutcome.Generating => "A learning plan for this topic is already being generated.",
                    _ => "Learning plan generation submitted. Poll the status endpoint to track progress."
                }
            };

            if (result.Outcome == TopicSubmissionOutcome.Ready)
            {
                return Ok(response);
            }

            return Accepted($"/api/learn/topics/{plan.id}/status", response);
        }

        [HttpGet("topics")]
        [RateLimit(60, 5)]
        public async Task<IActionResult> ListTopics()
        {
            string userId = ValidateAuth();
            if (userId == null) return Unauthorized();

            List<LearningPlan> plans = await learningPlanService.ListTopicsAsync();
            List<UserProgress> progressList = await progressService.ListByUserAsync(userId);
            var progressByPlan = progressList.ToDictionary(p => p.LearningPlanId, p => p);

            var items = plans.Select(plan =>
            {
                int? progressPercent = null;
                if (progressByPlan.TryGetValue(plan.id, out UserProgress progress) && plan.Chapters.Count > 0)
                {
                    int completed = progress.Chapters.Values.Count(c => c.QuizPassed && !string.IsNullOrEmpty(c.AssignmentVerdict));
                    progressPercent = (int)Math.Round(100.0 * completed / plan.Chapters.Count);
                }

                return new TopicListItemDto
                {
                    Slug = plan.id,
                    Topic = plan.Topic,
                    Status = plan.Status.ToString(),
                    TotalChapters = plan.Chapters.Count,
                    CreatedAtUtc = plan.CreatedAtUtc,
                    UserProgressPercent = progressPercent
                };
            }).ToList();

            return Ok(items);
        }

        [HttpGet("topics/{slug}/status")]
        [RateLimit(120, 5)]
        public async Task<IActionResult> GetStatus(string slug)
        {
            string userId = ValidateAuth();
            if (userId == null) return Unauthorized();

            LearningPlan plan = await learningPlanService.GetPlanAsync(slug);
            if (plan == null)
            {
                return NotFound(new { success = false, message = $"No learning plan found for '{slug}'" });
            }

            return Ok(BuildStatusResponse(plan));
        }

        [HttpGet("topics/{slug}/plan")]
        [RateLimit(60, 5)]
        public async Task<IActionResult> GetPlan(string slug)
        {
            string userId = ValidateAuth();
            if (userId == null) return Unauthorized();

            LearningPlan plan = await learningPlanService.GetPlanAsync(slug);
            if (plan == null)
            {
                return NotFound(new { success = false, message = $"No learning plan found for '{slug}'" });
            }

            var response = new LearningPlanResponse
            {
                Slug = plan.id,
                Topic = plan.Topic,
                Status = plan.Status.ToString(),
                CreatedAtUtc = plan.CreatedAtUtc,
                Chapters = plan.Chapters.OrderBy(c => c.Order).Select(c => new ChapterOutlineDto
                {
                    Order = c.Order,
                    Title = c.Title,
                    Description = c.Description,
                    KeyConcepts = c.KeyConcepts,
                    InterviewFocus = c.InterviewFocus,
                    ContentStatus = c.ContentStatus.ToString(),
                    QuizStatus = c.QuizStatus.ToString(),
                    AssignmentStatus = c.AssignmentStatus.ToString()
                }).ToList()
            };
            return Ok(response);
        }

        [HttpGet("topics/{slug}/chapters/{order:int}/content")]
        [RateLimit(60, 5)]
        public async Task<IActionResult> GetChapterContent(string slug, int order)
        {
            string userId = ValidateAuth();
            if (userId == null) return Unauthorized();

            LearningPlan plan = await learningPlanService.GetPlanAsync(slug);
            if (plan == null) return NotFound(new { success = false, message = $"No learning plan found for '{slug}'" });

            ChapterContent content = await learningPlanService.GetChapterContentAsync(slug, order);
            if (content == null)
            {
                return NotFound(new { success = false, message = "Chapter content is not ready yet. Check the status endpoint." });
            }

            await progressService.MarkContentViewedAsync(userId, plan, order);
            return Ok(new ChapterContentResponse { Order = content.Order, Title = content.Title, HtmlContent = content.HtmlContent });
        }

        [HttpGet("topics/{slug}/chapters/{order:int}/quiz")]
        [RateLimit(60, 5)]
        public async Task<IActionResult> GetChapterQuiz(string slug, int order)
        {
            string userId = ValidateAuth();
            if (userId == null) return Unauthorized();

            LearningPlan plan = await learningPlanService.GetPlanAsync(slug);
            if (plan == null) return NotFound(new { success = false, message = $"No learning plan found for '{slug}'" });

            ChapterQuiz quiz = await learningPlanService.GetChapterQuizAsync(slug, order);
            if (quiz == null)
            {
                return NotFound(new { success = false, message = "Chapter quiz is not ready yet. Check the status endpoint." });
            }

            // Strip correct answers - grading happens server-side.
            var response = new QuizResponse
            {
                Order = quiz.Order,
                Title = quiz.Title,
                Questions = quiz.Questions.Select(q => new QuizQuestionPublicDto
                {
                    QuestionNumber = q.QuestionNumber,
                    Question = q.Question,
                    Options = q.Options,
                    Difficulty = q.Difficulty,
                    InterviewStyle = q.InterviewStyle
                }).ToList()
            };
            return Ok(response);
        }

        [HttpPost("topics/{slug}/chapters/{order:int}/quiz/submit")]
        [RateLimit(30, 5)]
        public async Task<IActionResult> SubmitQuiz(string slug, int order, QuizSubmissionRequest request)
        {
            string userId = ValidateAuth();
            if (userId == null) return Unauthorized();

            LearningPlan plan = await learningPlanService.GetPlanAsync(slug);
            if (plan == null) return NotFound(new { success = false, message = $"No learning plan found for '{slug}'" });
            if (request == null || request.Answers == null || request.Answers.Count == 0)
            {
                return BadRequest(new { success = false, message = "Answers are required" });
            }

            QuizResultResponse result = await quizService.SubmitQuizAsync(plan, order, userId, request.Answers);
            if (result == null)
            {
                return NotFound(new { success = false, message = "Chapter quiz is not ready yet. Check the status endpoint." });
            }
            return Ok(result);
        }

        [HttpGet("topics/{slug}/chapters/{order:int}/assignment")]
        [RateLimit(60, 5)]
        public async Task<IActionResult> GetChapterAssignment(string slug, int order)
        {
            string userId = ValidateAuth();
            if (userId == null) return Unauthorized();

            LearningPlan plan = await learningPlanService.GetPlanAsync(slug);
            if (plan == null) return NotFound(new { success = false, message = $"No learning plan found for '{slug}'" });

            ChapterAssignment assignment = await learningPlanService.GetChapterAssignmentAsync(slug, order);
            if (assignment == null)
            {
                return NotFound(new { success = false, message = "Chapter assignment is not ready yet. Check the status endpoint." });
            }

            // Evaluation rubric stays server-side for grading.
            return Ok(new AssignmentResponse
            {
                Order = assignment.Order,
                Title = assignment.Title,
                ProblemStatement = assignment.ProblemStatement,
                Tasks = assignment.Tasks,
                Hints = assignment.Hints,
                ExpectedOutcome = assignment.ExpectedOutcome
            });
        }

        [HttpPost("topics/{slug}/chapters/{order:int}/assignment/submit")]
        [RateLimit(10, 10)]
        public async Task<IActionResult> SubmitAssignment(string slug, int order, AssignmentSubmissionRequest request)
        {
            string userId = ValidateAuth();
            if (userId == null) return Unauthorized();

            LearningPlan plan = await learningPlanService.GetPlanAsync(slug);
            if (plan == null) return NotFound(new { success = false, message = $"No learning plan found for '{slug}'" });
            if (request == null || string.IsNullOrWhiteSpace(request.Solution))
            {
                return BadRequest(new { success = false, message = "Solution is required" });
            }
            if (request.Solution.Length > MaxSolutionLength)
            {
                return BadRequest(new { success = false, message = $"Solution must be {MaxSolutionLength} characters or fewer" });
            }

            AssignmentFeedbackResponse feedback;
            try
            {
                feedback = await assignmentService.SubmitSolutionAsync(plan, order, userId, request.Solution);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, "Assignment verification failed for {Slug} chapter {Order}", slug, order);
                return StatusCode(502, new { success = false, message = "Assignment verification failed. Please try again." });
            }

            if (feedback == null)
            {
                return NotFound(new { success = false, message = "Chapter assignment is not ready yet. Check the status endpoint." });
            }
            return Ok(feedback);
        }

        [HttpPost("topics/{slug}/chapters/{order:int}/retry")]
        [RateLimit(10, 60)]
        public async Task<IActionResult> RetryChapter(string slug, int order)
        {
            string userId = ValidateAuth();
            if (userId == null) return Unauthorized();

            bool queued = await learningPlanService.RetryChapterAsync(slug, order);
            if (!queued)
            {
                return BadRequest(new { success = false, message = "Chapter not found or nothing to retry (all artifacts are ready)." });
            }
            return Accepted($"/api/learn/topics/{learningPlanService.NormalizeTopic(slug)}/status",
                new { success = true, message = "Chapter regeneration submitted." });
        }

        [HttpGet("topics/{slug}/progress")]
        [RateLimit(60, 5)]
        public async Task<IActionResult> GetProgress(string slug)
        {
            string userId = ValidateAuth();
            if (userId == null) return Unauthorized();

            LearningPlan plan = await learningPlanService.GetPlanAsync(slug);
            if (plan == null) return NotFound(new { success = false, message = $"No learning plan found for '{slug}'" });

            UserProgress progress = await progressService.GetProgressAsync(userId, plan);
            int completed = progress.Chapters.Values.Count(c => c.QuizPassed && !string.IsNullOrEmpty(c.AssignmentVerdict));
            int total = plan.Chapters.Count;

            return Ok(new UserProgressResponse
            {
                Topic = plan.Topic,
                Slug = plan.id,
                TotalChapters = total,
                CompletedChapters = completed,
                ProgressPercent = total == 0 ? 0 : (int)Math.Round(100.0 * completed / total),
                Chapters = progress.Chapters
            });
        }

        private string ValidateAuth()
        {
            string userId = HttpContext.Request.Headers["x-uid"].ToString();
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            bool isValid = tokenService.IsValidAuth(userId, HttpContext, GlobalConstant.Issuer);
            return isValid ? userId : null;
        }

        private static TopicStatusResponse BuildStatusResponse(LearningPlan plan)
        {
            int totalArtifacts = plan.Chapters.Count * 3;
            int readyArtifacts = plan.Chapters.Sum(c =>
                (c.ContentStatus == ArtifactStatus.Ready ? 1 : 0) +
                (c.QuizStatus == ArtifactStatus.Ready ? 1 : 0) +
                (c.AssignmentStatus == ArtifactStatus.Ready ? 1 : 0));

            return new TopicStatusResponse
            {
                Slug = plan.id,
                Topic = plan.Topic,
                Status = plan.Status.ToString(),
                TotalChapters = plan.Chapters.Count,
                ReadyChapters = plan.Chapters.Count(c =>
                    c.ContentStatus == ArtifactStatus.Ready && c.QuizStatus == ArtifactStatus.Ready && c.AssignmentStatus == ArtifactStatus.Ready),
                FailedChapters = plan.Chapters.Count(c =>
                    c.ContentStatus == ArtifactStatus.Failed || c.QuizStatus == ArtifactStatus.Failed || c.AssignmentStatus == ArtifactStatus.Failed),
                PercentComplete = totalArtifacts == 0 ? 0 : (int)Math.Round(100.0 * readyArtifacts / totalArtifacts),
                Error = plan.Error,
                Chapters = plan.Chapters.OrderBy(c => c.Order).Select(c => new ChapterStatusDto
                {
                    Order = c.Order,
                    Title = c.Title,
                    ContentStatus = c.ContentStatus.ToString(),
                    QuizStatus = c.QuizStatus.ToString(),
                    AssignmentStatus = c.AssignmentStatus.ToString(),
                    Error = c.Error
                }).ToList()
            };
        }
    }
}
