using LearnInDepth.Models;
using LearnInDepth.Repositories;
using LearnInDepth.Services.Interfaces;

namespace LearnInDepth.Services
{
    public class UserProgressService : IUserProgressService
    {
        private readonly IUserProgressRepository progressRepository;

        public UserProgressService(IUserProgressRepository progressRepository)
        {
            this.progressRepository = progressRepository;
        }

        public async Task MarkContentViewedAsync(string userId, LearningPlan plan, int order)
        {
            await UpdateChapterAsync(userId, plan, order, info => info.ContentViewed = true).ConfigureAwait(false);
        }

        public async Task RecordQuizResultAsync(string userId, LearningPlan plan, int order, int scorePercent, bool passed)
        {
            await UpdateChapterAsync(userId, plan, order, info =>
            {
                info.QuizScorePercent = Math.Max(info.QuizScorePercent ?? 0, scorePercent);
                info.QuizPassed = info.QuizPassed || passed;
            }).ConfigureAwait(false);
        }

        public async Task RecordAssignmentResultAsync(string userId, LearningPlan plan, int order, string verdict, int score)
        {
            await UpdateChapterAsync(userId, plan, order, info =>
            {
                info.AssignmentVerdict = verdict;
                info.AssignmentScore = Math.Max(info.AssignmentScore ?? 0, score);
            }).ConfigureAwait(false);
        }

        public async Task<UserProgress> GetProgressAsync(string userId, LearningPlan plan)
        {
            UserProgress progress = await progressRepository.GetAsync(userId, plan.id).ConfigureAwait(false);
            return progress ?? CreateFresh(userId, plan);
        }

        public async Task<List<UserProgress>> ListByUserAsync(string userId)
        {
            return await progressRepository.ListByUserAsync(userId).ConfigureAwait(false);
        }

        private async Task UpdateChapterAsync(string userId, LearningPlan plan, int order, Action<ChapterProgressInfo> update)
        {
            UserProgress progress = await progressRepository.GetAsync(userId, plan.id).ConfigureAwait(false)
                ?? CreateFresh(userId, plan);

            if (!progress.Chapters.TryGetValue(order, out ChapterProgressInfo info))
            {
                info = new ChapterProgressInfo();
                progress.Chapters[order] = info;
            }

            update(info);
            info.UpdatedAtUtc = DateTime.UtcNow;
            progress.LastAccessedAtUtc = DateTime.UtcNow;
            await progressRepository.UpsertAsync(progress).ConfigureAwait(false);
        }

        private static UserProgress CreateFresh(string userId, LearningPlan plan) => new UserProgress
        {
            id = UserProgressRepository.BuildId(userId, plan.id),
            UserId = userId,
            LearningPlanId = plan.id,
            Topic = plan.Topic,
            LastAccessedAtUtc = DateTime.UtcNow
        };
    }
}
