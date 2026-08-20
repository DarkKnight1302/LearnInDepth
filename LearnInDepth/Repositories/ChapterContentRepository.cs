using LearnInDepth.Models;
using Microsoft.Azure.Cosmos;
using NewHorizonLib.Services;

namespace LearnInDepth.Repositories
{
    public class ChapterContentRepository : IChapterContentRepository
    {
        private readonly ICosmosDbService cosmosDbService;
        private const string ContainerName = "ChapterContents";

        public ChapterContentRepository(ICosmosDbService cosmosDbService)
        {
            this.cosmosDbService = cosmosDbService;
        }

        private Container GetContainer() => cosmosDbService.GetContainer(ContainerName);

        public static string BuildId(string learningPlanId, int order) => $"{learningPlanId}-ch{order:D2}";

        public async Task<ChapterContent> GetAsync(string learningPlanId, int order)
        {
            try
            {
                ItemResponse<ChapterContent> response = await GetContainer()
                    .ReadItemAsync<ChapterContent>(BuildId(learningPlanId, order), new PartitionKey(learningPlanId))
                    .ConfigureAwait(false);
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task UpsertAsync(ChapterContent content)
        {
            await GetContainer()
                .UpsertItemAsync(content, new PartitionKey(content.LearningPlanId))
                .ConfigureAwait(false);
        }
    }
}
