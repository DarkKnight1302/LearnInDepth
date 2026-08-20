namespace LearnInDepth.Models
{
    public class ChapterQuiz
    {
        public string id { get; set; } = string.Empty; // {learningPlanId}-ch{order:D2}
        public string LearningPlanId { get; set; } = string.Empty; // partition key
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public List<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class QuizQuestion
    {
        public int QuestionNumber { get; set; }
        public string Question { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new List<string>();
        public int CorrectOptionIndex { get; set; }
        public string Explanation { get; set; } = string.Empty;
        public string Difficulty { get; set; } = "Medium"; // Easy / Medium / Hard
        public bool InterviewStyle { get; set; }
    }
}
