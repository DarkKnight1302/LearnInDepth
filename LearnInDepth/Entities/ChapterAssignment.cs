namespace LearnInDepth.Models
{
    public class ChapterAssignment
    {
        public string id { get; set; } = string.Empty; // {learningPlanId}-ch{order:D2}
        public string LearningPlanId { get; set; } = string.Empty; // partition key
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ProblemStatement { get; set; } = string.Empty;
        public List<string> Tasks { get; set; } = new List<string>();
        public List<string> Hints { get; set; } = new List<string>();
        public List<string> EvaluationRubric { get; set; } = new List<string>();
        public string ExpectedOutcome { get; set; } = string.Empty;
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
