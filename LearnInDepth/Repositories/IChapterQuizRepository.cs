using LearnInDepth.Models;

namespace LearnInDepth.Repositories
{
    public interface IChapterQuizRepository
    {
        Task<ChapterQuiz> GetAsync(string learningPlanId, int order);
        Task UpsertAsync(ChapterQuiz quiz);
    }
}
