using LearnInDepth.Models;
using Microsoft.Azure.Cosmos;
using NewHorizonLib.Services;

namespace LearnInDepth.Repositories
{
    public class LearningPlanRepository : ILearningPlanRepository
    {
        private readonly ICosmosDbService cosmosDbService;
        private const string ContainerName = "LearningPlans";

        public LearningPlanRepository(ICosmosDbService cosmosDbService)
        {
            this.cosmosDbService = cosmosDbService;
        }

        private Container GetContainer() => cosmosDbService.GetContainer(ContainerName);

        public async Task<LearningPlan> GetByIdAsync(string id)
        {
            try
            {
                ItemResponse<LearningPlan> response = await GetContainer()
                    .ReadItemAsync<LearningPlan>(id, new PartitionKey(id))
                    .ConfigureAwait(false);
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<LearningPlan> CreateAsync(LearningPlan plan)
        {
            ItemResponse<LearningPlan> response = await GetContainer()
                .CreateItemAsync(plan, new PartitionKey(plan.id))
                .ConfigureAwait(false);
            return response.Resource;
        }

        public async Task UpsertAsync(LearningPlan plan)
        {
            await GetContainer()
                .UpsertItemAsync(plan, new PartitionKey(plan.id))
                .ConfigureAwait(false);
        }

        public async Task<List<LearningPlan>> ListAllAsync()
        {
            var query = new QueryDefinition("SELECT * FROM c ORDER BY c.CreatedAtUtc DESC");
            return await ExecuteQueryAsync(query).ConfigureAwait(false);
        }

        public async Task<List<LearningPlan>> ListGeneratingAsync()
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.Status = @status")
                .WithParameter("@status", GenerationStatus.Generating.ToString());
            return await ExecuteQueryAsync(query).ConfigureAwait(false);
        }

        private async Task<List<LearningPlan>> ExecuteQueryAsync(QueryDefinition query)
        {
            var results = new List<LearningPlan>();
            using FeedIterator<LearningPlan> iterator = GetContainer().GetItemQueryIterator<LearningPlan>(query);
            while (iterator.HasMoreResults)
            {
                FeedResponse<LearningPlan> response = await iterator.ReadNextAsync().ConfigureAwait(false);
                results.AddRange(response);
            }
            return results;
        }
    }
}
