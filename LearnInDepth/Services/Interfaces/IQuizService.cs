using LearnInDepth.ApiModels;
using LearnInDepth.Models;

namespace LearnInDepth.Services.Interfaces
{
    public interface IQuizService
    {
        Task<QuizResultResponse> SubmitQuizAsync(LearningPlan plan, int order, string userId, Dictionary<int, int> answers);
    }
}
