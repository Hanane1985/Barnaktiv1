namespace Barnaktiv.Domain.Tests;

public sealed class ActivityCategoryMatchingTests
{
    [Theory]
    [InlineData("bad", "bad/simning")]
    [InlineData("  Bad  ", "bad/simning")]
    public void NormalizeToken_maps_swimming_alias(string input, string expected)
    {
        Assert.Equal(expected, ActivityCategoryMatching.NormalizeToken(input));
    }

    [Theory]
    [InlineData("fotboll", "fotboll")]
    [InlineData("Fotboll / inomhus", "fotboll/inomhus")]
    public void NormalizeToken_trims_and_normalizes_slashes(string input, string expected)
    {
        Assert.Equal(expected, ActivityCategoryMatching.NormalizeToken(input));
    }

    [Fact]
    public void MatchesSelection_matches_comma_separated_category_against_normalized_selection()
    {
        Assert.True(ActivityCategoryMatching.MatchesSelection("fotboll, bad", "bad"));
    }

    [Fact]
    public void MatchesSelection_returns_false_for_empty_category_value()
    {
        Assert.False(ActivityCategoryMatching.MatchesSelection("", "fotboll"));
        Assert.False(ActivityCategoryMatching.MatchesSelection("   ", "fotboll"));
    }
}
