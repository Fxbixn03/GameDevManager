using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;

namespace GameDevManager.Data.Services;

/// <summary>
/// Schreibt ein Export-ZIP so um, dass es sich als <b>Kopie</b> in dieselbe Installation
/// einspielen lässt: Jede Entitäts-GUID wird gegen eine frische getauscht
/// (<see cref="GuidRemap"/>), Name und Beschreibung im Manifest gegen die des neuen Projekts.
/// <para>
/// Der Import erhält GUIDs sonst bewusst — ein Projekt zieht damit um, ohne dass eine einzige
/// Referenz umgeschrieben werden müsste. Genau das macht ihn zum Duplizieren untauglich: Die
/// Kopie liefe in jeden Primärschlüssel des Originals.
/// </para>
/// </summary>
internal static class ProjectDuplication
{
    /// <summary>
    /// Liest das Archiv aus <paramref name="source"/> und schreibt die umgeschriebene Fassung
    /// nach <paramref name="target"/>.
    /// <para>
    /// Die Asset-Dateien werden unverändert übernommen, auch ihr Pfad: Der ist der
    /// <c>storageKey</c> des Originals, und unter genau dem sucht der Import sie auch für die
    /// Kopie. Den neuen Schlüssel vergibt erst der Dateispeicher beim Einspielen. Der Schlüssel
    /// enthält GUIDs ohne Bindestriche und bleibt vom Austausch deshalb ohnehin unberührt.
    /// </para>
    /// </summary>
    internal static void WriteCopy(Stream source, Stream target, string name, string? description)
    {
        using var input = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);

        var manifest = ExportFormat.FindManifest(input)
            ?? throw new InvalidDataException($"Im Archiv fehlt {ExportFormat.ManifestFileName}.");

        var contentPrefix = manifest.FullName[..^ExportFormat.ManifestFileName.Length]
            + ExportFormat.ContentFolder;

        // Erst alle Inhaltsdateien lesen und die vergebenen GUIDs einsammeln — der Austausch
        // muss über alle Dateien hinweg derselbe sein, sonst zeigten Verweise ins Leere.
        var contents = new Dictionary<string, string>(StringComparer.Ordinal);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in input.Entries)
        {
            if (!IsContentFile(entry.FullName, contentPrefix))
            {
                continue;
            }

            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            var text = reader.ReadToEnd();
            contents[entry.FullName] = text;

            GuidRemap.Collect(JsonNode.Parse(text), map);
        }

        using var output = new ZipArchive(target, ZipArchiveMode.Create, leaveOpen: true);

        foreach (var entry in input.Entries)
        {
            var copied = output.CreateEntry(entry.FullName, CompressionLevel.Optimal);

            if (contents.TryGetValue(entry.FullName, out var content))
            {
                using var writer = new StreamWriter(copied.Open(), Encoding.UTF8);
                writer.Write(GuidRemap.Apply(content, map));
                continue;
            }

            if (entry == manifest)
            {
                using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                using var writer = new StreamWriter(copied.Open(), Encoding.UTF8);
                writer.Write(RenameProject(reader.ReadToEnd(), name, description));
                continue;
            }

            using var raw = entry.Open();
            using var plain = copied.Open();
            raw.CopyTo(plain);
        }
    }

    /// <summary>Eine JSON-Datei direkt unter <c>content/</c> — Unterordner gibt es dort nicht.</summary>
    private static bool IsContentFile(string fullName, string contentPrefix) =>
        fullName.StartsWith(contentPrefix, StringComparison.Ordinal)
        && fullName.EndsWith(".json", StringComparison.Ordinal)
        && !fullName[contentPrefix.Length..].Contains('/');

    /// <summary>
    /// Setzt Name und Beschreibung ins Manifest. Der Import übernimmt beides als Namen des
    /// Zielprojekts — die Kopie heißt damit von Anfang an richtig, statt kurzzeitig genauso
    /// wie das Original.
    /// </summary>
    private static string RenameProject(string manifest, string name, string? description)
    {
        if (JsonNode.Parse(manifest) is not JsonObject root)
        {
            return manifest;
        }

        var project = root["project"] as JsonObject ?? [];
        project["name"] = name;
        project["description"] = description;
        root["project"] = project;

        return root.ToJsonString(ExportFormat.JsonOptions);
    }
}
