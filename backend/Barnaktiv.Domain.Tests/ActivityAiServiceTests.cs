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
        var aiOptions = Options.Create(new AiOptions { MaxQuestionLength = 500 });

        var sut = new ActivityAiService(
            activityService,
            chatClient,
            aiOptions,
            NullLogger<ActivityAiService>.Instance);

        var result = await sut.AskAsync("Har du fotboll i Göteborg?", CancellationToken.None);

        Assert.Equal("Här är en aktivitet som passar.", result.Answer);
        var source = Assert.Single(result.Sources);
        Assert.Equal("Fotbollsskola", source.Title);
        Assert.Equal("https://example.com/signup", source.SignupUrl);
        Assert.Equal("Göteborg", source.City);
        Assert.Equal("date-asc", activityService.LastQuery?.Sort);
        Assert.Equal(15, activityService.LastQuery?.Take);
        Assert.Equal(0, activityService.LastQuery?.Skip);
        Assert.All(chatClient.Calls, call => Assert.True(call.JsonObjectResponse));
    }

    private sealed class StubActivityService(IReadOnlyList<ActivityDto> activities) : IActivityService
    {
        public ActivityQueryDto? LastQuery { get; private set; }

        public Task<IReadOnlyList<ActivityDto>> GetAllAsync(
            ActivityQueryDto query,
            CancellationToken cancellationToken)
        {
            LastQuery = query;
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
}
