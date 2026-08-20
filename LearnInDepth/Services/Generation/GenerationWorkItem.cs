namespace LearnInDepth.Services.Generation
{
    public class GenerationWorkItem
    {
        public string PlanId { get; set; } = string.Empty;

        /// <summary>
        /// Null = generate whole plan (plan outline + all chapters).
        /// Set = regenerate a single chapter's missing/failed artifacts.
        /// </summary>
        public int? ChapterOrder { get; set; }
    }
}
