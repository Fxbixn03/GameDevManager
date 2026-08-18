using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Übersetzungsvorschläge: die Versiegelung der geschützten Spannen, die
/// Sprachkürzel-Abbildung mit Rückfall und die stille Vorgabe ohne Anbieter.
/// </summary>
public class TranslationSuggesterTests
{
    // -------------------------------------------------------------------- Versiegelung

    [Fact]
    public void Erwaehnungen_und_Platzhalter_werden_versiegelt_und_unversehrt_zurueckgebaut()
    {
        var mention = ContentMentions.Format(ModuleKeys.Items, Guid.NewGuid(), "Eisenschwert");
        var text = $"Nimm {mention} und {{0}} Gold < 5 & mehr.";

        var xml = TranslationText.ToXml(text);

        // Die geschützten Spannen stecken in <x>, alles andere ist escaped.
        Assert.Contains($"<x>{mention.Replace("|", "|")}</x>", xml.Replace("&amp;", "&"));
        Assert.Contains("<x>{0}</x>", xml);
        Assert.Contains("&lt; 5 &amp; mehr", xml);
        Assert.DoesNotContain("< 5", xml.Replace("<x>", "").Replace("</x>", ""));

        // Rückweg: Der Text kommt Zeichen für Zeichen zurück.
        Assert.Equal(text, TranslationText.FromXml(xml));
    }

    [Fact]
    public void Markdown_bleibt_Text_und_wird_nicht_versiegelt()
    {
        var xml = TranslationText.ToXml("Das ist **wichtig**.");

        Assert.DoesNotContain("<x>", xml);
        Assert.Equal("Das ist **wichtig**.", TranslationText.FromXml(xml));
    }

    // ------------------------------------------------------------- Sprachkürzel-Abbildung

    [Theory]
    [InlineData("de", false, "DE")]
    [InlineData("de-AT", false, "DE")]        // Rückfall auf die Hauptsprache
    [InlineData("en-GB", true, "EN-GB")]      // bekannte Zielvariante bleibt
    [InlineData("en-GB", false, "EN")]        // als Quelle zählt nur die Hauptsprache
    [InlineData("en", true, "EN-US")]         // DeepL verlangt fürs Ziel eine Variante
    [InlineData("pt", true, "PT-PT")]
    [InlineData("pt-BR", true, "PT-BR")]
    [InlineData("fr-CA", true, "FR")]         // unbekannte Variante fällt zurück
    public void Sprachkuerzel_werden_mit_Rueckfall_abgebildet(string code, bool isTarget, string expected) =>
        Assert.Equal(expected, DeepLLanguageMap.Map(code, isTarget));

    // ------------------------------------------------------------------------- Vorgabe

    [Fact]
    public async Task Ohne_Anbieter_ist_die_Vorgabe_still_und_nicht_konfiguriert()
    {
        using var test = new TestDatabase();

        var suggester = test.GetService<ITranslationSuggester>();

        Assert.IsType<NullTranslationSuggester>(suggester);
        Assert.False(suggester.IsConfigured);
        Assert.Equal(string.Empty, await suggester.SuggestAsync("Hallo", "de", "en"));
    }
}
