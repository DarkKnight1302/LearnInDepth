using LearnInDepth.Models;

namespace LearnInDepth.Repositories
{
    public interface IChapterContentRepository
    {
        Task<ChapterContent> GetAsync(string learningPlanId, int order);
        Task UpsertAsync(ChapterContent content);
    }
}
