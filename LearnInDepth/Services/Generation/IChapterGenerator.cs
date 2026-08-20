using LearnInDepth.Models;

namespace LearnInDepth.Services.Generation
{
    public interface IChapterGenerator
    {
        /// <summary>
        /// Generates all missing/failed artifacts (content, quiz, assignment) for one chapter.
        /// Idempotent: artifacts already Ready in the database are skipped.
        /// Mutates chapter artifact statuses on the shared plan and persists the plan after each artifact.
        /// </summary>
        Task GenerateChapterAsync(
            LearningPlan plan,
            ChapterOutline chapter,
            SemaphoreSlim planLock,
            CancellationToken cancellationToken);
    }
}
