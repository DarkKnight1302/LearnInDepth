using Microsoft.Azure.Cosmos;
using NewHorizonLib.Services;

namespace LearnInDepth.Services
{
    public interface ICosmosCollectionBootstrapper
    {
        Task EnsureCreatedAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Creates the LearnInDepth database and all containers if they don't exist (Nuggets pattern).
    /// </summary>
    public class CosmosCollectionBootstrapper : ICosmosCollectionBootstrapper
    {
        private readonly ISecretService secretService;
        private readonly ILogger<CosmosCollectionBootstrapper> logger;

        public CosmosCollectionBootstrapper(ISecretService secretService, ILogger<CosmosCollectionBootstrapper> logger)
        {
            this.secretService = secretService;
            this.logger = logger;
        }

        public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
        {
            string connectionString = secretService.GetSecretValue("cosmosDbConnectionString");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Missing required secret: cosmosDbConnectionString");
            }

            using var cosmosClient = new CosmosClient(connectionString);
            DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(
                GlobalConstant.DatabaseName, cancellationToken: cancellationToken).ConfigureAwait(false);
            Database database = databaseResponse.Database;

            // Partition key paths use PascalCase to match the entity property names (Cosmos SDK default serialization).
            await CreateContainerAsync(database, "Users", "/id", cancellationToken).ConfigureAwait(false);
            await CreateContainerAsync(database, "LearningPlans", "/id", cancellationToken).ConfigureAwait(false);
            await CreateContainerAsync(database, "ChapterContents", "/LearningPlanId", cancellationToken).ConfigureAwait(false);
            await CreateContainerAsync(database, "ChapterQuizzes", "/LearningPlanId", cancellationToken).ConfigureAwait(false);
            await CreateContainerAsync(database, "ChapterAssignments", "/LearningPlanId", cancellationToken).ConfigureAwait(false);
            await CreateContainerAsync(database, "AssignmentSubmissions", "/UserId", cancellationToken).ConfigureAwait(false);
            await CreateContainerAsync(database, "UserProgress", "/UserId", cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Cosmos database '{Database}' and containers verified.", GlobalConstant.DatabaseName);
        }

        private async Task CreateContainerAsync(Database database, string name, string partitionKeyPath, CancellationToken cancellationToken)
        {
            await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(name, partitionKeyPath),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
