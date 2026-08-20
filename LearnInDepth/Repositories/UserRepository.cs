using Microsoft.Azure.Cosmos;
using NewHorizonLib.Services;
using User = LearnInDepth.Models.User;

namespace LearnInDepth.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ICosmosDbService cosmosDbService;
        private const string ContainerName = "Users";

        public UserRepository(ICosmosDbService cosmosDbService)
        {
            this.cosmosDbService = cosmosDbService;
        }

        private Container GetContainer() => cosmosDbService.GetContainer(ContainerName);

        public async Task<User> GetByIdAsync(string email)
        {
            try
            {
                ItemResponse<User> response = await GetContainer()
                    .ReadItemAsync<User>(email, new PartitionKey(email))
                    .ConfigureAwait(false);
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task UpsertAsync(User user)
        {
            await GetContainer()
                .UpsertItemAsync(user, new PartitionKey(user.id))
                .ConfigureAwait(false);
        }
    }
}
