namespace LearnInDepth.Models
{
    public class UserProgress
    {
        public string id { get; set; } = string.Empty; // {userId}|{learningPlanId}
        public string UserId { get; set; } = string.Empty; // partition key
        public string LearningPlanId { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public Dictionary<int, ChapterProgressInfo> Chapters { get; set; } = new Dictionary<int, ChapterProgressInfo>();
        public DateTime LastAccessedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class ChapterProgressInfo
    {
        public bool ContentViewed { get; set; }
        public int? QuizScorePercent { get; set; }
        public bool QuizPassed { get; set; }
        public string AssignmentVerdict { get; set; } = string.Empty;
        public int? AssignmentScore { get; set; }
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
