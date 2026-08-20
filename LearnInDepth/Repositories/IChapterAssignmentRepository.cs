using LearnInDepth.Models;

namespace LearnInDepth.Repositories
{
    public interface IChapterAssignmentRepository
    {
        Task<ChapterAssignment> GetAsync(string learningPlanId, int order);
        Task UpsertAsync(ChapterAssignment assignment);
    }
}
