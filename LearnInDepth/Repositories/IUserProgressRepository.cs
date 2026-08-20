using LearnInDepth.Models;

namespace LearnInDepth.Repositories
{
    public interface IUserProgressRepository
    {
        Task<UserProgress> GetAsync(string userId, string learningPlanId);
        Task UpsertAsync(UserProgress progress);
        Task<List<UserProgress>> ListByUserAsync(string userId);
    }
}
