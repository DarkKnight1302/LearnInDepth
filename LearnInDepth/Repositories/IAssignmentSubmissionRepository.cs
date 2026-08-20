using LearnInDepth.Models;

namespace LearnInDepth.Repositories
{
    public interface IAssignmentSubmissionRepository
    {
        Task CreateAsync(AssignmentSubmission submission);
        Task<List<AssignmentSubmission>> ListByChapterAsync(string userId, string learningPlanId, int chapterOrder);
    }
}
