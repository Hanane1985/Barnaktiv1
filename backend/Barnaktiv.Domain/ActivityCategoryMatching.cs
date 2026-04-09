namespace Barnaktiv.Domain;

public static class ActivityCategoryMatching
{
    public static bool MatchesSelection(string categoryValue, string selectedCategory)
    {
        if (string.IsNullOrWhiteSpace(categoryValue))
        {
            return false;
        }

        var normalizedSelected = NormalizeToken(selectedCategory);

        return categoryValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeToken)
            .Any(category => category == normalizedSelected);
    }

    public static string NormalizeToken(string category)
    {
        var normalized = category
            .Trim()
            .ToLowerInvariant()
            .Replace(" / ", "/")
            .Replace("/ ", "/")
            .Replace(" /", "/");

        return normalized switch
        {
            "bad" => "bad/simning",
            _ => normalized,
        };
    }
}
