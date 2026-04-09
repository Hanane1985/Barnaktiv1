using System.Net;
using System.Text.RegularExpressions;
using Barnaktiv.Application.Activities.Queries;
using Barnaktiv.Application.DTOs.Activities;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Domain;
using Barnaktiv.Domain.Entities;

namespace Barnaktiv.Application.Services;

public sealed class ActivityService(IActivityRepository repository) : IActivityService
{
    public async Task<IReadOnlyList<ActivityDto>> GetAllAsync(
        ActivityQueryDto query,
        CancellationToken cancellationToken)
    {
        var persistenceQuery = ActivityQueryMapper.ToPersistenceQuery(query);
        var activities = await repository.GetAllAsync(persistenceQuery, cancellationToken);

        var categoryFilter = ActivityQueryMapper.CategoryFilterOrNull(query);
        if (categoryFilter is not null)
        {
            activities = activities
                .Where(activity => ActivityCategoryMatching.MatchesSelection(activity.Category, categoryFilter))
                .ToList();
        }

        return activities.Select(Map).ToList();
    }

    private static ActivityDto Map(Activity activity) =>
        new(
            activity.Id,
            SanitizeText(activity.Title),
            SanitizeText(activity.Description),
            SanitizeText(activity.Organizer),
            SanitizeText(activity.Location),
            SanitizeText(activity.City),
            activity.AgeFrom,
            activity.AgeTo,
            SanitizeText(activity.Sport),
            SanitizeText(activity.Category),
            activity.ListingType.ToString(),
            activity.Date,
            activity.Price,
            SanitizeText(activity.WebsiteUrl),
            SanitizeText(activity.SignupUrl),
            SanitizeText(activity.ImageUrl),
            SanitizeText(activity.Source),
            activity.RegistrationStatus.ToString(),
            activity.RegistrationOpenAt,
            activity.RegistrationCloseAt,
            activity.CreatedAt);

    private static string SanitizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value;

        for (var index = 0; index < 2; index++)
        {
            var decoded = WebUtility.HtmlDecode(normalized);

            if (string.Equals(decoded, normalized, StringComparison.Ordinal))
            {
                break;
            }

            normalized = decoded;
        }

        normalized = normalized
            .Replace("\u00A0", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

        return Regex.Replace(normalized, "\\s+", " ", RegexOptions.CultureInvariant).Trim();
    }
}
