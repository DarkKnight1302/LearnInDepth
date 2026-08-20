using LearnInDepth.Models;

namespace LearnInDepth.ApiModels
{
    public class SubmitTopicRequest
    {
        public string Topic { get; set; } = string.Empty;
    }

    public enum TopicSubmissionOutcome
    {
        Ready,
        Generating,
        Accepted
    }

    public class TopicSubmissionResult
    {
        public TopicSubmissionOutcome Outcome { get; set; }
        public LearningPlan Plan { get; set; }
    }

    public class TopicSubmissionResponse
    {
        public string Topic { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class ChapterStatusDto
    {
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ContentStatus { get; set; } = string.Empty;
        public string QuizStatus { get; set; } = string.Empty;
        public string AssignmentStatus { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

    public class TopicStatusResponse
    {
        public string Slug { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int TotalChapters { get; set; }
        public int ReadyChapters { get; set; }
        public int FailedChapters { get; set; }
        public int PercentComplete { get; set; }
        public string Error { get; set; } = string.Empty;
        public List<ChapterStatusDto> Chapters { get; set; } = new List<ChapterStatusDto>();
    }

    public class ChapterOutlineDto
    {
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> KeyConcepts { get; set; } = new List<string>();
        public List<string> InterviewFocus { get; set; } = new List<string>();
        public string ContentStatus { get; set; } = string.Empty;
        public string QuizStatus { get; set; } = string.Empty;
        public string AssignmentStatus { get; set; } = string.Empty;
    }

    public class LearningPlanResponse
    {
        public string Slug { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public List<ChapterOutlineDto> Chapters { get; set; } = new List<ChapterOutlineDto>();
    }

    public class ChapterContentResponse
    {
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public string HtmlContent { get; set; } = string.Empty;
    }

    public class QuizQuestionPublicDto
    {
        public int QuestionNumber { get; set; }
        public string Question { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new List<string>();
        public string Difficulty { get; set; } = string.Empty;
        public bool InterviewStyle { get; set; }
    }

    public class QuizResponse
    {
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public List<QuizQuestionPublicDto> Questions { get; set; } = new List<QuizQuestionPublicDto>();
    }

    public class QuizSubmissionRequest
    {
        /// <summary>Map of questionNumber -> selected option index (0-based).</summary>
        public Dictionary<int, int> Answers { get; set; } = new Dictionary<int, int>();
    }

    public class QuestionResultDto
    {
        public int QuestionNumber { get; set; }
        public bool WasAnswered { get; set; }
        public int? SelectedOptionIndex { get; set; }
        public int CorrectOptionIndex { get; set; }
        public bool IsCorrect { get; set; }
        public string Explanation { get; set; } = string.Empty;
    }

    public class QuizResultResponse
    {
        public int Order { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectCount { get; set; }
        public int ScorePercent { get; set; }
        public bool Passed { get; set; }
        public List<QuestionResultDto> Results { get; set; } = new List<QuestionResultDto>();
    }

    public class AssignmentResponse
    {
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ProblemStatement { get; set; } = string.Empty;
        public List<string> Tasks { get; set; } = new List<string>();
        public List<string> Hints { get; set; } = new List<string>();
        public string ExpectedOutcome { get; set; } = string.Empty;
    }

    public class AssignmentSubmissionRequest
    {
        public string Solution { get; set; } = string.Empty;
    }

    public class AssignmentFeedbackResponse
    {
        public string Verdict { get; set; } = string.Empty;
        public int Score { get; set; }
        public List<string> WhatWentWell { get; set; } = new List<string>();
        public List<string> Corrections { get; set; } = new List<string>();
        public string InterviewTips { get; set; } = string.Empty;
        public DateTime SubmittedAtUtc { get; set; }
    }

    public class TopicListItemDto
    {
        public string Slug { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int TotalChapters { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public int? UserProgressPercent { get; set; }
    }

    public class UserProgressResponse
    {
        public string Topic { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public int TotalChapters { get; set; }
        public int CompletedChapters { get; set; }
        public int ProgressPercent { get; set; }
        public Dictionary<int, ChapterProgressInfo> Chapters { get; set; } = new Dictionary<int, ChapterProgressInfo>();
    }
}
