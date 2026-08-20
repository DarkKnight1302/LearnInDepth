using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LearnInDepth.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum GenerationStatus
    {
        Generating,
        Ready,
        Failed
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ArtifactStatus
    {
        Pending,
        Generating,
        Ready,
        Failed
    }

    public class LearningPlan
    {
        public string id { get; set; } = string.Empty; // topic slug, also the partition key
        public string Topic { get; set; } = string.Empty;
        public GenerationStatus Status { get; set; } = GenerationStatus.Generating;
        public List<ChapterOutline> Chapters { get; set; } = new List<ChapterOutline>();
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAtUtc { get; set; }
        public string Error { get; set; } = string.Empty;
    }

    public class ChapterOutline
    {
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> KeyConcepts { get; set; } = new List<string>();
        public List<string> InterviewFocus { get; set; } = new List<string>();
        public ArtifactStatus ContentStatus { get; set; } = ArtifactStatus.Pending;
        public ArtifactStatus QuizStatus { get; set; } = ArtifactStatus.Pending;
        public ArtifactStatus AssignmentStatus { get; set; } = ArtifactStatus.Pending;
        public string Error { get; set; } = string.Empty;
    }
}
