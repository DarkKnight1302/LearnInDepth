using LearnInDepth.ApiModels;
using LearnInDepth.Models;
using LearnInDepth.Repositories;
using LearnInDepth.Services.Interfaces;

namespace LearnInDepth.Services
{
    public class QuizService : IQuizService
    {
        private const int PassThresholdPercent = 70;

        private readonly IChapterQuizRepository quizRepository;
        private readonly IUserProgressService progressService;

        public QuizService(IChapterQuizRepository quizRepository, IUserProgressService progressService)
        {
            this.quizRepository = quizRepository;
            this.progressService = progressService;
        }

        public async Task<QuizResultResponse> SubmitQuizAsync(LearningPlan plan, int order, string userId, Dictionary<int, int> answers)
        {
            ChapterQuiz quiz = await quizRepository.GetAsync(plan.id, order).ConfigureAwait(false);
            if (quiz == null || quiz.Questions.Count == 0)
            {
                return null;
            }

            var response = new QuizResultResponse
            {
                Order = order,
                TotalQuestions = quiz.Questions.Count
            };

            foreach (QuizQuestion question in quiz.Questions)
            {
                int selected = -1;
                bool answered = answers != null && answers.TryGetValue(question.QuestionNumber, out selected);
                bool isCorrect = answered && selected == question.CorrectOptionIndex;

                response.Results.Add(new QuestionResultDto
                {
                    QuestionNumber = question.QuestionNumber,
                    WasAnswered = answered,
                    SelectedOptionIndex = answered ? selected : (int?)null,
                    CorrectOptionIndex = question.CorrectOptionIndex,
                    IsCorrect = isCorrect,
                    Explanation = question.Explanation
                });

                if (isCorrect)
                {
                    response.CorrectCount++;
                }
            }

            response.ScorePercent = (int)Math.Round(100.0 * response.CorrectCount / response.TotalQuestions);
            response.Passed = response.ScorePercent >= PassThresholdPercent;

            await progressService.RecordQuizResultAsync(userId, plan, order, response.ScorePercent, response.Passed).ConfigureAwait(false);
            return response;
        }
    }
}
