using LearnInDepth.Models;
using Microsoft.Azure.Cosmos;
using NewHorizonLib.Services;

namespace LearnInDepth.Repositories
{
    public class UserProgressRepository : IUserProgressRepository
    {
        private readonly ICosmosDbService cosmosDbService;
        private const string ContainerName = "UserProgress";

        public UserProgressRepository(ICosmosDbService cosmosDbService)
        {
            this.cosmosDbService = cosmosDbService;
        }

        private Container GetContainer() => cosmosDbService.GetContainer(ContainerName);

        public static string BuildId(string userId, string learningPlanId) => $"{userId}|{learningPlanId}";

        public async Task<UserProgress> GetAsync(string userId, string learningPlanId)
        {
            try
            {
                ItemResponse<UserProgress> response = await GetContainer()
                    .ReadItemAsync<UserProgress>(BuildId(userId, learningPlanId), new PartitionKey(userId))
                    .ConfigureAwait(false);
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task UpsertAsync(UserProgress progress)
        {
            await GetContainer()
                .UpsertItemAsync(progress, new PartitionKey(progress.UserId))
                .ConfigureAwait(false);
        }

        public async Task<List<UserProgress>> ListByUserAsync(string userId)
        {
            var query = new QueryDefinition("SELECT * FROM c ORDER BY c.LastAccessedAtUtc DESC");
            var results = new List<UserProgress>();
            using FeedIterator<UserProgress> iterator = GetContainer().GetItemQueryIterator<UserProgress>(
                query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });
            while (iterator.HasMoreResults)
            {
                FeedResponse<UserProgress> response = await iterator.ReadNextAsync().ConfigureAwait(false);
                results.AddRange(response);
            }
            return results;
        }
    }
}
