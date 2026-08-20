using System.Text.Json.Serialization;

namespace LearnInDepth.Services.Prompts
{
    public class PlanGenerationResponse
    {
        [JsonPropertyName("chapters")]
        public List<PlanChapterDto> Chapters { get; set; } = new List<PlanChapterDto>();
    }

    public class PlanChapterDto
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("keyConcepts")]
        public List<string> KeyConcepts { get; set; } = new List<string>();

        [JsonPropertyName("interviewFocus")]
        public List<string> InterviewFocus { get; set; } = new List<string>();
    }

    public class QuizGenerationResponse
    {
        [JsonPropertyName("questions")]
        public List<QuizQuestionDto> Questions { get; set; } = new List<QuizQuestionDto>();
    }

    public class QuizQuestionDto
    {
        [JsonPropertyName("question")]
        public string Question { get; set; } = string.Empty;

        [JsonPropertyName("options")]
        public List<string> Options { get; set; } = new List<string>();

        [JsonPropertyName("correctOptionIndex")]
        public int CorrectOptionIndex { get; set; }

        [JsonPropertyName("explanation")]
        public string Explanation { get; set; } = string.Empty;

        [JsonPropertyName("difficulty")]
        public string Difficulty { get; set; } = "Medium";

        [JsonPropertyName("interviewStyle")]
        public bool InterviewStyle { get; set; }
    }

    public class AssignmentGenerationResponse
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("problemStatement")]
        public string ProblemStatement { get; set; } = string.Empty;

        [JsonPropertyName("tasks")]
        public List<string> Tasks { get; set; } = new List<string>();

        [JsonPropertyName("hints")]
        public List<string> Hints { get; set; } = new List<string>();

        [JsonPropertyName("evaluationRubric")]
        public List<string> EvaluationRubric { get; set; } = new List<string>();

        [JsonPropertyName("expectedOutcome")]
        public string ExpectedOutcome { get; set; } = string.Empty;
    }

    public class VerificationResponse
    {
        [JsonPropertyName("verdict")]
        public string Verdict { get; set; } = string.Empty;

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("whatWentWell")]
        public List<string> WhatWentWell { get; set; } = new List<string>();

        [JsonPropertyName("corrections")]
        public List<string> Corrections { get; set; } = new List<string>();

        [JsonPropertyName("interviewTips")]
        public string InterviewTips { get; set; } = string.Empty;
    }
}
