namespace LearnInDepth.Models
{
    public class AssignmentSubmission
    {
        public string id { get; set; } = Guid.NewGuid().ToString("N");
        public string UserId { get; set; } = string.Empty; // partition key
        public string LearningPlanId { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public int ChapterOrder { get; set; }
        public string ChapterTitle { get; set; } = string.Empty;
        public string SolutionText { get; set; } = string.Empty;
        public string Verdict { get; set; } = string.Empty; // Pass / NeedsWork / Fail
        public int Score { get; set; } // 0-100
        public List<string> WhatWentWell { get; set; } = new List<string>();
        public List<string> Corrections { get; set; } = new List<string>();
        public string InterviewTips { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
