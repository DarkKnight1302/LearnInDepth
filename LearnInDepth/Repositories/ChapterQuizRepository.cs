using LearnInDepth.Models;
using Microsoft.Azure.Cosmos;
using NewHorizonLib.Services;

namespace LearnInDepth.Repositories
{
    public class ChapterQuizRepository : IChapterQuizRepository
    {
        private readonly ICosmosDbService cosmosDbService;
        private const string ContainerName = "ChapterQuizzes";

        public ChapterQuizRepository(ICosmosDbService cosmosDbService)
        {
            this.cosmosDbService = cosmosDbService;
        }

        private Container GetContainer() => cosmosDbService.GetContainer(ContainerName);

        public async Task<ChapterQuiz> GetAsync(string learningPlanId, int order)
        {
            try
            {
                ItemResponse<ChapterQuiz> response = await GetContainer()
                    .ReadItemAsync<ChapterQuiz>(ChapterContentRepository.BuildId(learningPlanId, order), new PartitionKey(learningPlanId))
                    .ConfigureAwait(false);
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task UpsertAsync(ChapterQuiz quiz)
        {
            await GetContainer()
                .UpsertItemAsync(quiz, new PartitionKey(quiz.LearningPlanId))
                .ConfigureAwait(false);
        }
    }
}
