using System.Xml.Linq;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Vollständigkeit der englischen Oberfläche (F29): Zu jeder neutralen <c>.resx</c> muss eine
/// <c>.en.resx</c> mit denselben Schlüsseln danebenliegen.
/// <para>
/// Ein fehlender Schlüssel ist kein Fehler zur Laufzeit — der <c>ResourceManager</c> fällt
/// still auf die deutsche Fassung zurück. Genau deshalb braucht es diesen Test: Sonst fiele
/// eine Lücke erst auf, wenn jemand mit englischer Oberfläche vor einem deutschen Satz steht.
/// </para>
/// <para>
/// Geprüft wird über die <b>Dateien</b> und nicht über den Localizer: Der bräuchte je Seite
/// einen Typparameter, und die Web-Schicht ist vom Testprojekt aus gar nicht referenziert.
/// </para>
/// </summary>
public class TranslationCompletenessTests
{
    /// <summary>
    /// Der Weg zum Quellverzeichnis. Vom Ausgabeordner des Testprojekts aus vier Ebenen
    /// hinauf — dieselbe Rechnung wie in jedem Testprojekt, das an Quelldateien muss.
    /// </summary>
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new InvalidOperationException("Das Quellverzeichnis wurde nicht gefunden.");
        }
    }

    private static IEnumerable<string> NeutralFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.resx", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".en.resx", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Order(StringComparer.Ordinal);

    private static List<string> Keys(string path) =>
    [
        .. XDocument.Load(path).Root!
            .Elements("data")
            .Select(element => element.Attribute("name")?.Value)
            .OfType<string>()
    ];

    [Fact]
    public void Zu_jeder_deutschen_Datei_gibt_es_eine_englische()
    {
        var missing = NeutralFiles()
            .Where(path => !File.Exists(path.Replace(".resx", ".en.resx")))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToList();

        Assert.True(missing.Count == 0,
            $"Ohne englische Fassung: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Keine_englische_Datei_laesst_einen_Schluessel_aus()
    {
        var gaps = new List<string>();

        foreach (var path in NeutralFiles())
        {
            var english = path.Replace(".resx", ".en.resx");

            if (!File.Exists(english))
            {
                // Die fehlende Datei meldet der Test darüber — hier ginge es sonst zweimal
                // durch dieselbe Lücke.
                continue;
            }

            var have = Keys(english).ToHashSet(StringComparer.Ordinal);
            var missing = Keys(path).Where(key => !have.Contains(key)).ToList();

            if (missing.Count > 0)
            {
                gaps.Add($"{Path.GetRelativePath(RepositoryRoot, path)}: {string.Join(", ", missing)}");
            }
        }

        Assert.True(gaps.Count == 0, string.Join(Environment.NewLine, gaps));
    }

    [Fact]
    public void Keine_englische_Datei_traegt_einen_Schluessel_zu_viel()
    {
        var extra = new List<string>();

        foreach (var path in NeutralFiles())
        {
            var english = path.Replace(".resx", ".en.resx");

            if (!File.Exists(english))
            {
                continue;
            }

            var neutral = Keys(path).ToHashSet(StringComparer.Ordinal);
            var orphans = Keys(english).Where(key => !neutral.Contains(key)).ToList();

            // Ein Schlüssel, den es nur auf Englisch gibt, ist tot: Er wird nie gelesen, weil
            // die Anwendung ihn über die neutrale Fassung anspricht.
            if (orphans.Count > 0)
            {
                extra.Add($"{Path.GetRelativePath(RepositoryRoot, path)}: {string.Join(", ", orphans)}");
            }
        }

        Assert.True(extra.Count == 0, string.Join(Environment.NewLine, extra));
    }
}
