using System.Globalization;
using GameDevManager.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Sprache der Oberfläche: Die neutralen <c>.resx</c> tragen Deutsch, die
/// <c>.en.resx</c> daneben Englisch. Geprüft wird, dass die Satelliten-Dateien überhaupt
/// gefunden werden — ein Tippfehler im Dateinamen fiele sonst erst im Betrieb auf, und zwar
/// stumm: Der Localizer liefert dann einfach die deutsche Fassung.
/// </summary>
public class LanguageTests : IDisposable
{
    private readonly CultureInfo _before = CultureInfo.CurrentUICulture;
    private readonly ServiceProvider _provider;

    public LanguageTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();

        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _before;
        _provider.Dispose();

        GC.SuppressFinalize(this);
    }

    private string Text(string key, string culture)
    {
        CultureInfo.CurrentUICulture = new CultureInfo(culture);

        return _provider.GetRequiredService<IStringLocalizer<DataMessages>>()[key].Value;
    }

    [Theory]
    [InlineData("ItemNameRequired", "Das Item braucht einen Namen.", "The item needs a name.")]
    [InlineData("Stance_War", "Krieg", "War")]
    [InlineData("Csv_NotANumber", "keine Zahl", "not a number")]
    public void Dieselbe_Meldung_kommt_je_Sprache_anders(string key, string german, string english)
    {
        Assert.Equal(german, Text(key, "de"));
        Assert.Equal(english, Text(key, "en"));
    }

    [Fact]
    public void Eine_unbekannte_Sprache_faellt_auf_die_neutrale_Fassung_zurueck()
    {
        // Für Französisch gibt es keine Satelliten-Datei — dann gilt Deutsch, die neutrale
        // Fassung, statt des rohen Schlüssels.
        Assert.Equal("Das Item braucht einen Namen.", Text("ItemNameRequired", "fr"));
    }
}
