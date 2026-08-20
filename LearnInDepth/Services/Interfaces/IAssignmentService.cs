using LearnInDepth.ApiModels;
using LearnInDepth.Models;

namespace LearnInDepth.Services.Interfaces
{
    public interface IAssignmentService
    {
        Task<AssignmentFeedbackResponse> SubmitSolutionAsync(LearningPlan plan, int order, string userId, string solution);
    }
}
