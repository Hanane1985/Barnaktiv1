using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Application.Models.Ingestion;

namespace Barnaktiv.Infrastructure.Scrapers;

public sealed class JsonFeedActivityScraper(HttpClient httpClient) : IActivityScraper
{
    public string Kind => "json-feed-v1";

    public async Task<ScrapeResult> ScrapeAsync(
        ConfiguredIngestionSource source,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.EndpointUrl))
        {
            return new ScrapeResult([], [$"Source '{source.SourceKey}' has no endpoint URL."]);
        }

        using var response = await httpClient.GetAsync(source.EndpointUrl, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ScrapeResult(
                [],
                [$"GET {source.EndpointUrl} returned {(int)response.StatusCode} {response.ReasonPhrase}."]);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        using var document = JsonDocument.Parse(responseBody);

        if (!TryResolveItemsArray(document.RootElement, out var itemsElement))
        {
            return new ScrapeResult(
                [],
                [$"The JSON feed for '{source.SourceKey}' must be an array or expose an 'activities' array."]);
        }

        var items = new List<ScrapedActivityItem>();
        var errors = new List<string>();
        var index = 0;

        foreach (var itemElement in itemsElement.EnumerateArray())
        {
            index++;

            if (itemElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"Item {index} is not a JSON object.");
                continue;
            }

            if (TryMapItem(itemElement, out var item, out var error))
            {
                items.Add(item!);
                continue;
            }

            errors.Add($"Item {index}: {error}");
        }

        return new ScrapeResult(
            items,
            errors,
            items.Select(item => item.ExternalId).ToList(),
            errors.Count == 0);
    }

    private static bool TryResolveItemsArray(JsonElement rootElement, out JsonElement itemsElement)
    {
        if (rootElement.ValueKind == JsonValueKind.Array)
        {
            itemsElement = rootElement;
            return true;
        }

        foreach (var propertyName in new[] { "activities", "items", "data" })
        {
            if (rootElement.TryGetProperty(propertyName, out itemsElement) &&
                itemsElement.ValueKind == JsonValueKind.Array)
            {
                return true;
            }
        }

        itemsElement = default;
        return false;
    }

    private static bool TryMapItem(
        JsonElement itemElement,
        out ScrapedActivityItem? item,
        out string error)
    {
        var title = GetString(itemElement, "title");

        if (string.IsNullOrWhiteSpace(title))
        {
            item = null;
            error = "Missing required field 'title'.";
            return false;
        }

        var dateText = GetString(itemElement, "date", "startDate");

        if (!TryParseDate(dateText, out var date))
        {
            item = null;
            error = "Missing or invalid 'date'.";
            return false;
        }

        var externalId = GetString(itemElement, "externalId", "id", "slug");

        if (string.IsNullOrWhiteSpace(externalId))
        {
            externalId = BuildExternalId(itemElement, title, dateText);
        }

        var ageFrom = GetInt(itemElement, "ageFrom", "minAge");
        var ageTo = GetInt(itemElement, "ageTo", "maxAge");

        if (ageFrom > 0 && ageTo <= 0)
        {
            ageTo = ageFrom;
        }
        else if (ageTo > 0 && ageFrom <= 0)
        {
            ageFrom = ageTo;
        }

        var price = GetDecimal(itemElement, "price");

        if (GetBool(itemElement, "isFree") is true)
        {
            price = 0;
        }

        item = new ScrapedActivityItem(
            externalId,
            title.Trim(),
            GetString(itemElement, "description") ?? string.Empty,
            GetString(itemElement, "organizer") ?? string.Empty,
            GetString(itemElement, "location", "venue") ?? string.Empty,
            GetString(itemElement, "city") ?? string.Empty,
            ageFrom,
            ageTo,
            GetString(itemElement, "category") ?? string.Empty,
            date,
            price,
            GetString(itemElement, "websiteUrl", "url") ?? string.Empty,
            GetString(itemElement, "imageUrl", "image") ?? string.Empty,
            itemElement.GetRawText());

        error = string.Empty;
        return true;
    }

    private static string BuildExternalId(JsonElement itemElement, string title, string? dateText)
    {
        var seed = string.Join(
            "|",
            title.Trim(),
            dateText?.Trim() ?? string.Empty,
            GetString(itemElement, "location", "venue") ?? string.Empty,
            GetString(itemElement, "websiteUrl", "url") ?? string.Empty);

        var normalizedSeed = seed.ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedSeed));
        return Convert.ToHexString(hash);
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind == JsonValueKind.Number ||
                value.ValueKind == JsonValueKind.True ||
                value.ValueKind == JsonValueKind.False)
            {
                return value.ToString();
            }
        }

        return null;
    }

    private static int GetInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String &&
                int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }
        }

        return 0;
    }

    private static decimal GetDecimal(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String &&
                decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }
        }

        return 0;
    }

    private static bool? GetBool(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (value.ValueKind == JsonValueKind.String &&
                bool.TryParse(value.GetString(), out var boolValue))
            {
                return boolValue;
            }
        }

        return null;
    }

    private static bool TryParseDate(string? value, out DateTime date)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
            out date);
    }
}
