using LearnInDepth.Models;
using Microsoft.Azure.Cosmos;
using NewHorizonLib.Services;

namespace LearnInDepth.Repositories
{
    public class AssignmentSubmissionRepository : IAssignmentSubmissionRepository
    {
        private readonly ICosmosDbService cosmosDbService;
        private const string ContainerName = "AssignmentSubmissions";

        public AssignmentSubmissionRepository(ICosmosDbService cosmosDbService)
        {
            this.cosmosDbService = cosmosDbService;
        }

        private Container GetContainer() => cosmosDbService.GetContainer(ContainerName);

        public async Task CreateAsync(AssignmentSubmission submission)
        {
            await GetContainer()
                .CreateItemAsync(submission, new PartitionKey(submission.UserId))
                .ConfigureAwait(false);
        }

        public async Task<List<AssignmentSubmission>> ListByChapterAsync(string userId, string learningPlanId, int chapterOrder)
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.LearningPlanId = @planId AND c.ChapterOrder = @order ORDER BY c.SubmittedAtUtc DESC")
                .WithParameter("@planId", learningPlanId)
                .WithParameter("@order", chapterOrder);

            var results = new List<AssignmentSubmission>();
            using FeedIterator<AssignmentSubmission> iterator = GetContainer().GetItemQueryIterator<AssignmentSubmission>(
                query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });
            while (iterator.HasMoreResults)
            {
                FeedResponse<AssignmentSubmission> response = await iterator.ReadNextAsync().ConfigureAwait(false);
                results.AddRange(response);
            }
            return results;
        }
    }
}
