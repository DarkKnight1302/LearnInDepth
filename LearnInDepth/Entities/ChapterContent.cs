namespace LearnInDepth.Models
{
    public class ChapterContent
    {
        public string id { get; set; } = string.Empty; // {learningPlanId}-ch{order:D2}
        public string LearningPlanId { get; set; } = string.Empty; // partition key
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public string HtmlContent { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
