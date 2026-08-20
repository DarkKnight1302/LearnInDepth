using LearnInDepth.Models;

namespace LearnInDepth.Repositories
{
    public interface ILearningPlanRepository
    {
        Task<LearningPlan> GetByIdAsync(string id);
        Task<LearningPlan> CreateAsync(LearningPlan plan);
        Task UpsertAsync(LearningPlan plan);
        Task<List<LearningPlan>> ListAllAsync();
        Task<List<LearningPlan>> ListGeneratingAsync();
    }
}
