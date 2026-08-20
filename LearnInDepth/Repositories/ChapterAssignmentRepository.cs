using LearnInDepth.Models;
using Microsoft.Azure.Cosmos;
using NewHorizonLib.Services;

namespace LearnInDepth.Repositories
{
    public class ChapterAssignmentRepository : IChapterAssignmentRepository
    {
        private readonly ICosmosDbService cosmosDbService;
        private const string ContainerName = "ChapterAssignments";

        public ChapterAssignmentRepository(ICosmosDbService cosmosDbService)
        {
            this.cosmosDbService = cosmosDbService;
        }

        private Container GetContainer() => cosmosDbService.GetContainer(ContainerName);

        public async Task<ChapterAssignment> GetAsync(string learningPlanId, int order)
        {
            try
            {
                ItemResponse<ChapterAssignment> response = await GetContainer()
                    .ReadItemAsync<ChapterAssignment>(ChapterContentRepository.BuildId(learningPlanId, order), new PartitionKey(learningPlanId))
                    .ConfigureAwait(false);
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task UpsertAsync(ChapterAssignment assignment)
        {
            await GetContainer()
                .UpsertItemAsync(assignment, new PartitionKey(assignment.LearningPlanId))
                .ConfigureAwait(false);
        }
    }
}
