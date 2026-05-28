using Barnaktiv.Application.DTOs.Activities;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Application.Options;
using Barnaktiv.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Barnaktiv.Domain.Tests;

public sealed class ActivityAiServiceTests
{
    [Fact]
    public async Task AskAsync_returns_answer_and_sources_from_matched_activities()
    {
        var activities = new List<ActivityDto>
        {
            new(
                Guid.NewGuid(),
                "Fotbollsskola",
                "Testaktivitet",
                "IF Test",
                "Testplanen",
                "Göteborg",
                8,
                10,
                "Fotboll",
                "bollsport",
                "course",
                new DateTime(2026, 6, 10),
                0,
                "https://example.com/info",
                "https://example.com/signup",
                "https://example.com/image.jpg",
                "manual",
                "Open",
                null,
                null,
                DateTime.UtcNow)
        };

        var activityService = new StubActivityService(activities);
        var chatClient = new QueueAiChatClient(
            """{"search":"fotboll","city":"Göteborg"}""",
            """{"answer":"Här är en aktivitet som passar."}""");
        var embeddingClient = new StubEmbeddingClient();
        var embeddingRepository = new InMemoryEmbeddingRepository();
        var aiOptions = Options.Create(new AiOptions { MaxQuestionLength = 500 });

        var sut = new ActivityAiService(
            activityService,
            chatClient,
            embeddingClient,
            embeddingRepository,
            aiOptions,
            NullLogger<ActivityAiService>.Instance);

        var result = await sut.AskAsync("Har du fotboll i Göteborg?", CancellationToken.None);

        Assert.Equal("Här är en aktivitet som passar.", result.Answer);
        var source = Assert.Single(result.Sources);
        Assert.Equal("Fotbollsskola", source.Title);
        Assert.Equal("https://example.com/signup", source.SignupUrl);
        Assert.Equal("Göteborg", source.City);
        Assert.Contains(activityService.Queries, query => query.Take == 15 && query.Skip == 0);
        Assert.Contains(activityService.Queries, query => query.Take == 120 && query.Search is null);
        Assert.All(activityService.Queries, query => Assert.Equal("date-asc", query.Sort));
        Assert.All(chatClient.Calls, call => Assert.True(call.JsonObjectResponse));
        Assert.Equal(2, embeddingClient.CallCount);
        Assert.Single(embeddingRepository.StoredVectors);
    }

    private sealed class StubActivityService(IReadOnlyList<ActivityDto> activities) : IActivityService
    {
        public List<ActivityQueryDto> Queries { get; } = [];

        public Task<IReadOnlyList<ActivityDto>> GetAllAsync(
            ActivityQueryDto query,
            CancellationToken cancellationToken)
        {
            Queries.Add(query);
            return Task.FromResult(activities);
        }
    }

    private sealed class QueueAiChatClient(params string[] responses) : IAiChatClient
    {
        private readonly Queue<string> responsesQueue = new(responses);

        public List<(IReadOnlyList<AiChatMessage> Messages, bool JsonObjectResponse)> Calls { get; } = [];

        public Task<string> CompleteAsync(
            IReadOnlyList<AiChatMessage> messages,
            bool jsonObjectResponse,
            CancellationToken cancellationToken)
        {
            Calls.Add((messages, jsonObjectResponse));

            if (responsesQueue.Count == 0)
            {
                throw new InvalidOperationException("No queued AI response available.");
            }

            return Task.FromResult(responsesQueue.Dequeue());
        }
    }

    private sealed class StubEmbeddingClient : IAiEmbeddingClient
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<float[]>> CreateEmbeddingsAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken)
        {
            CallCount++;
            IReadOnlyList<float[]> vectors = inputs
                .Select(_ => new float[] { 1f, 0f, 0.5f })
                .ToList();
            return Task.FromResult(vectors);
        }
    }

    private sealed class InMemoryEmbeddingRepository : IActivityEmbeddingRepository
    {
        public Dictionary<Guid, StoredActivityEmbedding> StoredVectors { get; } = [];

        public Task<IReadOnlyDictionary<Guid, StoredActivityEmbedding>> GetByActivityIdsAsync(
            IReadOnlyCollection<Guid> activityIds,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<Guid, StoredActivityEmbedding> result = StoredVectors
                .Where(entry => activityIds.Contains(entry.Key))
                .ToDictionary(entry => entry.Key, entry => entry.Value);
            return Task.FromResult(result);
        }

        public Task UpsertAsync(
            IReadOnlyList<ActivityEmbeddingUpsertItem> items,
            CancellationToken cancellationToken)
        {
            foreach (var item in items)
            {
                StoredVectors[item.ActivityId] = new StoredActivityEmbedding(
                    item.ActivityId,
                    item.ContentHash,
                    item.Vector);
            }

            return Task.CompletedTask;
        }
    }
}
