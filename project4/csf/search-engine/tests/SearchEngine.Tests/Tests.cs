using SearchEngine;
using Xunit;

public class EditDistanceTests
{
    [Theory]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("", "abc", 3)]
    [InlineData("same", "same", 0)]
    [InlineData("turbine", "turbin", 1)]
    public void KnownDistances(string a, string b, int expected) =>
        Assert.Equal(expected, EditDistance.Levenshtein(a, b));
}

public class IndexTests
{
    private static InvertedIndex Build()
    {
        var i = new InvertedIndex();
        i.Add("turbines.txt", "Gas turbine blades and turbine maintenance. Turbine inspection schedules.");
        i.Add("pumps.txt", "Centrifugal pump seals and pump maintenance.");
        i.Add("safety.txt", "Safety procedures for maintenance of rotating equipment.");
        return i;
    }

    [Fact]
    public void RanksMostRelevantDocumentFirst() =>
        Assert.Equal("turbines.txt", Build().Search("turbine").First().Document);

    [Fact]
    public void RareTermsOutweighCommonOnes()
    {
        // "maintenance" is in all docs (low IDF); "seals" only in pumps.txt.
        Assert.Equal("pumps.txt", Build().Search("seals maintenance").First().Document);
    }

    [Fact]
    public void FuzzyMatchCorrectsTypos()
    {
        var idx = Build();
        Assert.Equal("turbine", idx.Correct("turbin"));
        Assert.Equal("turbines.txt", idx.Search("turbyne").First().Document);
        Assert.Empty(idx.Search("turbyne", fuzzy: false));
    }

    [Fact]
    public void UnknownTermsReturnNothing() =>
        Assert.Empty(Build().Search("zzzzzzzz"));
}
