using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Namensvorlage der Serien-Anlage: {n}, {n:001}, {roemisch} und Wortlisten — samt der
/// Grenzfälle, die ein Parser aushalten muss.
/// </summary>
public class NameTemplateTests
{
    [Theory]
    [InlineData("Eisenschwert {n}", 1, "Eisenschwert 1")]
    [InlineData("Eisenschwert {n}", 12, "Eisenschwert 12")]
    [InlineData("Truhe {n:001}", 1, "Truhe 001")]
    [InlineData("Truhe {n:001}", 42, "Truhe 042")]
    [InlineData("Welle {n:010}", 1, "Welle 010")]
    [InlineData("Welle {n:010}", 3, "Welle 012")]
    [InlineData("Akt {roemisch}", 4, "Akt IV")]
    [InlineData("Akt {römisch}", 9, "Akt IX")]
    [InlineData("{liste:Eisen|Stahl|Silber}schwert", 1, "Eisenschwert")]
    [InlineData("{liste:Eisen|Stahl|Silber}schwert", 3, "Silberschwert")]
    [InlineData("{liste:Eisen|Stahl|Silber}schwert", 4, "Eisenschwert")]
    [InlineData("{liste:Eisen,Stahl}schwert", 2, "Stahlschwert")]
    [InlineData("{liste:Eisen|Stahl} {n}", 2, "Stahl 2")]
    public void Bekannte_Platzhalter_werden_ersetzt(string template, int index, string expected) =>
        Assert.Equal(expected, NameTemplate.Format(template, index));

    [Theory]
    [InlineData("Schwert {foo}", 1, "Schwert {foo}")]
    [InlineData("Schwert {n:abc}", 1, "Schwert {n:abc}")]
    [InlineData("Schwert {liste:}", 1, "Schwert {liste:}")]
    [InlineData("Schwert {", 1, "Schwert {")]
    [InlineData("Schwert }", 1, "Schwert }")]
    public void Unbekannte_Platzhalter_bleiben_unveraendert_stehen(string template, int index, string expected) =>
        Assert.Equal(expected, NameTemplate.Format(template, index));

    [Fact]
    public void Eine_leere_Vorlage_ergibt_einen_leeren_Namen()
    {
        Assert.Equal(string.Empty, NameTemplate.Format(string.Empty, 1));
        Assert.False(NameTemplate.HasPlaceholder(string.Empty));
    }

    [Fact]
    public void HasPlaceholder_erkennt_ob_die_Serie_unterscheidbare_Namen_ergibt()
    {
        Assert.True(NameTemplate.HasPlaceholder("Schwert {n}"));
        Assert.True(NameTemplate.HasPlaceholder("Akt {roemisch}"));
        Assert.True(NameTemplate.HasPlaceholder("{liste:a|b}"));

        // Ein fester Text und ein unbekannter Platzhalter ergeben immer denselben Namen.
        Assert.False(NameTemplate.HasPlaceholder("Schwert"));
        Assert.False(NameTemplate.HasPlaceholder("Schwert {foo}"));

        // Eine Ein-Wort-Liste wiederholt sich ebenfalls.
        Assert.False(NameTemplate.HasPlaceholder("{liste:Eisen}schwert"));
    }

    [Fact]
    public void Expand_liefert_die_ganze_Serie()
    {
        Assert.Equal(
            ["Truhe 001", "Truhe 002", "Truhe 003"],
            NameTemplate.Expand("Truhe {n:001}", 3));

        Assert.Empty(NameTemplate.Expand("Truhe {n}", 0));
    }

    [Theory]
    [InlineData(1, "I")]
    [InlineData(14, "XIV")]
    [InlineData(1987, "MCMLXXXVII")]
    [InlineData(3999, "MMMCMXCIX")]
    [InlineData(4000, "4000")]
    [InlineData(0, "0")]
    public void Roemische_Zahlen_stimmen_und_kapitulieren_jenseits_von_3999(int value, string expected) =>
        Assert.Equal(expected, NameTemplate.ToRoman(value));
}
