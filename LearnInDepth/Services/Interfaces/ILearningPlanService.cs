using LearnInDepth.ApiModels;
using LearnInDepth.Models;

namespace LearnInDepth.Services.Interfaces
{
    public interface ILearningPlanService
    {
        Task<TopicSubmissionResult> SubmitTopicAsync(string topic, string userId);
        Task<LearningPlan> GetPlanAsync(string slug);
        Task<TopicStatusResponse> GetRealTimeStatusAsync(LearningPlan plan);
        Task<ChapterContent> GetChapterContentAsync(string slug, int order);
        Task<ChapterQuiz> GetChapterQuizAsync(string slug, int order);
        Task<ChapterAssignment> GetChapterAssignmentAsync(string slug, int order);
        Task<List<LearningPlan>> ListTopicsAsync();
        Task<bool> RetryChapterAsync(string slug, int order);
        Task<int> RunQueuedGenerationsAsync(CancellationToken cancellationToken);
        string NormalizeTopic(string topic);
    }
}
