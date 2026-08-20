using LearnInDepth.Models;

namespace LearnInDepth.Services.Interfaces
{
    public interface IUserProgressService
    {
        Task MarkContentViewedAsync(string userId, LearningPlan plan, int order);
        Task RecordQuizResultAsync(string userId, LearningPlan plan, int order, int scorePercent, bool passed);
        Task RecordAssignmentResultAsync(string userId, LearningPlan plan, int order, string verdict, int score);
        Task<UserProgress> GetProgressAsync(string userId, LearningPlan plan);
        Task<List<UserProgress>> ListByUserAsync(string userId);
    }
}
