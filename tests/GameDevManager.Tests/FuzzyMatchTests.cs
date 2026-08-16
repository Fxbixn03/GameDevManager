using GameDevManager.Data.Services;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Der unscharfe Vergleich der Kommandopalette. Die Zusicherung ist schmal, aber sie trägt die
/// halbe Bedienung: Die Buchstaben müssen der Reihe nach vorkommen, nicht zusammenhängend.
/// </summary>
public class FuzzyMatchTests
{
    [Theory]
    [InlineData("ei schwert", "Eisenschwert")]
    [InlineData("eisen", "Eisenschwert")]
    [InlineData("swrt", "Eisenschwert")]
    [InlineData("EISEN", "eisenschwert")]
    [InlineData("", "Eisenschwert")]
    public void Passende_Eingaben_treffen(string query, string candidate) =>
        Assert.True(FuzzyMatch.Matches(query, candidate));

    [Theory]
    [InlineData("schwerte", "Eisenschwert")]
    [InlineData("tresw", "Eisenschwert")]
    [InlineData("axt", "Eisenschwert")]
    public void Unpassende_Eingaben_treffen_nicht(string query, string candidate) =>
        Assert.False(FuzzyMatch.Matches(query, candidate));

    [Fact]
    public void Ein_leeres_Ziel_trifft_nur_die_leere_Eingabe()
    {
        Assert.True(FuzzyMatch.Matches(string.Empty, string.Empty));
        Assert.False(FuzzyMatch.Matches("a", string.Empty));
        Assert.False(FuzzyMatch.Matches("a", null));
    }

    [Fact]
    public void Die_Reihenfolge_zaehlt()
    {
        // „ne it“ findet „Neu: Items“ …
        Assert.True(FuzzyMatch.Matches("ne it", "Neu: Items"));

        // … „it ne“ dagegen nicht, obwohl dieselben Buchstaben vorkommen.
        Assert.False(FuzzyMatch.Matches("it ne", "Neu: Items"));
    }
}
