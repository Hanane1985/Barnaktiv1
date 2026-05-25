using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Barnaktiv.Application.DTOs.Activities;
using Barnaktiv.Application.DTOs.Ai;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Barnaktiv.Application.Services;

public sealed class ActivityAiService(
    IActivityService activityService,
    IAiChatClient chatClient,
    IOptions<AiOptions> options,
    ILogger<ActivityAiService> logger) : IActivityAiService
{
    private const int ActivityContextLimit = 15;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<AskResponseDto> AskAsync(string question, CancellationToken cancellationToken)
    {
        var aiOptions = options.Value;
        var trimmedQuestion = question.Trim();

        if (trimmedQuestion.Length > aiOptions.MaxQuestionLength)
        {
            throw new ArgumentException(
                $"Question must be at most {aiOptions.MaxQuestionLength} characters.",
                nameof(question));
        }

        var query = await ParseQueryAsync(trimmedQuestion, cancellationToken);
        query.Take = ActivityContextLimit;
        query.Skip = 0;

        var activities = await activityService.GetAllAsync(query, cancellationToken);

        if (activities.Count == 0)
        {
            return new AskResponseDto(
                "Jag hittade inga aktiviteter i databasen som matchar din fråga. Prova att bredda ålder, ort eller aktivitetstyp.",
                []);
        }

        var answer = await ComposeAnswerAsync(trimmedQuestion, activities, cancellationToken);
        var sources = activities
            .Select(activity => new ActivityAiSourceDto(
                activity.Id,
                activity.Title,
                PickUrl(activity.SignupUrl, activity.WebsiteUrl),
                string.IsNullOrWhiteSpace(activity.City) ? null : activity.City,
                activity.Date))
            .ToList();

        logger.LogInformation(
            "AI ask completed with {SourceCount} sources for question length {QuestionLength}",
            sources.Count,
            trimmedQuestion.Length);

        return new AskResponseDto(answer, sources);
    }

    private async Task<ActivityQueryDto> ParseQueryAsync(
        string question,
        CancellationToken cancellationToken)
    {
        var messages = new List<AiChatMessage>
        {
            new(
                "system",
                """
                Du tolkar frågor om barnaktiviteter i Göteborg och närområdet.
                Svara ENDAST med giltig JSON utan markdown eller förklaring.
                Fält: search (string|null), city (string|null), sport (string|null),
                category (string|null), minAge (number|null), maxAge (number|null),
                price ("free"|"paid"|null).
                Om användaren nämner en ålder, sätt minAge och maxAge till ett rimligt spann.
                Om inget anges för ett fält, använd null.
                """),
            new("user", question),
        };

        var raw = await chatClient.CompleteAsync(messages, jsonObjectResponse: true, cancellationToken);
        var parsed = JsonSerializer.Deserialize<ParsedActivityAiQuery>(raw, JsonOptions)
            ?? new ParsedActivityAiQuery();

        return new ActivityQueryDto
        {
            Search = parsed.Search,
            City = parsed.City,
            Sport = parsed.Sport,
            Category = parsed.Category,
            MinAge = parsed.MinAge,
            MaxAge = parsed.MaxAge,
            Price = parsed.Price,
            Sort = "date-asc",
        };
    }

    private async Task<string> ComposeAnswerAsync(
        string question,
        IReadOnlyList<ActivityDto> activities,
        CancellationToken cancellationToken)
    {
        var context = BuildActivityContext(activities);
        var messages = new List<AiChatMessage>
        {
            new(
                "system",
                """
                Du är Barnaktiv-assistenten. Svara på svenska, vänligt och konkret.
                Använd ENDAST aktiviteterna i kontexten nedan. Hitta på inga aktiviteter.
                Om listan inte räcker, säg det och föreslå hur användaren kan omformulera.
                Svara med JSON: { "answer": "din text här" } utan markdown.
                """),
            new(
                "user",
                $"""
                Fråga: {question}

                Aktiviteter:
                {context}
                """),
        };

        var raw = await chatClient.CompleteAsync(messages, jsonObjectResponse: true, cancellationToken);
        var parsed = JsonSerializer.Deserialize<AnswerPayload>(raw, JsonOptions);

        if (!string.IsNullOrWhiteSpace(parsed?.Answer))
        {
            return parsed.Answer.Trim();
        }

        return raw.Trim();
    }

    private static string BuildActivityContext(IReadOnlyList<ActivityDto> activities)
    {
        var builder = new StringBuilder();

        foreach (var activity in activities)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"- id={activity.Id}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"  titel: {activity.Title}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"  datum: {activity.Date:yyyy-MM-dd}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"  stad: {activity.City}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"  ålder: {activity.AgeFrom}-{activity.AgeTo}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"  sport: {activity.Sport}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"  pris: {activity.Price}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"  plats: {activity.Location}");

            if (!string.IsNullOrWhiteSpace(activity.Description))
            {
                var snippet = activity.Description.Length > 220
                    ? activity.Description[..220] + "…"
                    : activity.Description;
                builder.AppendLine(CultureInfo.InvariantCulture, $"  beskrivning: {snippet}");
            }
        }

        return builder.ToString();
    }

    private static string? PickUrl(string signupUrl, string websiteUrl)
    {
        if (!string.IsNullOrWhiteSpace(signupUrl))
        {
            return signupUrl.Trim();
        }

        if (!string.IsNullOrWhiteSpace(websiteUrl))
        {
            return websiteUrl.Trim();
        }

        return null;
    }

    private sealed class ParsedActivityAiQuery
    {
        public string? Search { get; set; }

        public string? City { get; set; }

        public string? Sport { get; set; }

        public string? Category { get; set; }

        public int? MinAge { get; set; }

        public int? MaxAge { get; set; }

        public string? Price { get; set; }
    }

    private sealed class AnswerPayload
    {
        public string? Answer { get; set; }
    }
}
