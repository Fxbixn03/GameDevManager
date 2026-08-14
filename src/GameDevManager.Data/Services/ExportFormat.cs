using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;

namespace GameDevManager.Data.Services;

/// <summary>
/// Alles, was Export, Import und Diff über den Aufbau des Export-ZIPs gemeinsam wissen müssen:
/// die JSON-Regeln, die festen Dateipfade im Archiv und das Auffinden des Manifests. Wer hier
/// etwas ändert, ändert das Exportformat — dann <see cref="ExportService.FormatVersion"/> erhöhen.
/// </summary>
internal static class ExportFormat
{
    internal const string ManifestFileName = "project.json";
    internal const string ContentFolder = "content/";
    internal const string AssetFilesFolder = "assets/files/";

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { StripNonExportedProperties } }
    };

    /// <summary>
    /// Welche Inhaltsdatei zu welchem Modul gehört — für Beschriftungen in Import-Ergebnis und
    /// Diff-Ansicht. Dateien ohne Modul (Arten/Felder, Feldwerte, Bedingungen) stehen mit
    /// <c>null</c> drin und werden in der Oberfläche über eigene Texte benannt.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string?> ContentFileModules =
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["items.json"] = ModuleKeys.Items,
            ["crafting.json"] = ModuleKeys.Crafting,
            ["currencies.json"] = ModuleKeys.Currencies,
            ["rarities.json"] = ModuleKeys.Rarities,
            ["npcs.json"] = ModuleKeys.Npcs,
            ["factions.json"] = ModuleKeys.Factions,
            ["diplomacy.json"] = ModuleKeys.Diplomacy,
            ["maps.json"] = ModuleKeys.Maps,
            ["dialogs.json"] = ModuleKeys.Dialogs,
            ["story.json"] = ModuleKeys.Story,
            ["quests.json"] = ModuleKeys.Quests,
            ["events.json"] = ModuleKeys.Events,
            ["player.json"] = ModuleKeys.Player,
            ["classes.json"] = ModuleKeys.Classes,
            ["loot.json"] = ModuleKeys.Loot,
            ["world.json"] = ModuleKeys.World,
            ["effects.json"] = ModuleKeys.Effects,
            ["achievements.json"] = ModuleKeys.Achievements,
            ["collectibles.json"] = ModuleKeys.Collectibles,
            ["audio.json"] = ModuleKeys.Audio,
            ["cutscenes.json"] = ModuleKeys.Cutscenes,
            ["tags.json"] = ModuleKeys.Tags,
            ["assets.json"] = ModuleKeys.Assets,
            ["types-and-fields.json"] = null,
            ["field-values.json"] = null,
            ["conditions.json"] = null
        };

    /// <summary>
    /// Findet das Manifest im Archiv, egal unter welchem Engine-Präfix es liegt. Bei mehreren
    /// Kandidaten gewinnt der kürzeste Pfad — ein echtes Manifest liegt nie tiefer als das Präfix.
    /// </summary>
    internal static ZipArchiveEntry? FindManifest(ZipArchive archive) =>
        archive.Entries
            .Where(entry => entry.FullName == ManifestFileName
                || entry.FullName.EndsWith("/" + ManifestFileName, StringComparison.Ordinal))
            .OrderBy(entry => entry.FullName.Length)
            .FirstOrDefault();

    /// <summary>
    /// Entfernt aus den Domain-Entitäten, was nicht in den Export gehört: Navigationsobjekte
    /// (die GUID-Spalten bleiben — Referenzen laufen laut Konzept ausschließlich über GUIDs)
    /// und berechnete Nur-Lese-Eigenschaften wie <c>IsToolAsset</c> oder <c>ModuleKey</c>.
    /// Kind-Sammlungen bleiben eingebettet. Typen außerhalb des Entitäten-Namensraums
    /// (Manifest, Datei-Wrapper) sind nicht betroffen. Gilt für beide Richtungen — der Import
    /// liest mit denselben Regeln, mit denen der Export schreibt.
    /// </summary>
    private static void StripNonExportedProperties(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object
            || typeInfo.Type.Namespace != typeof(ContentEntity).Namespace)
        {
            return;
        }

        for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
        {
            var property = typeInfo.Properties[i];
            var isNavigation = property.PropertyType.IsClass
                && property.PropertyType.Namespace == typeof(ContentEntity).Namespace;

            var isUnloadedBackReference = IsUnloadedCollection(typeInfo.Type, property.Name);

            if (property.Set is null || isNavigation || isUnloadedBackReference)
            {
                typeInfo.Properties.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Kind-Sammlungen, die trotz ihrer Form nicht in den Export gehören. Sie sind entweder nie
    /// mitgeladen — eine immer leere Liste im Archiv sähe nach „nichts vorhanden“ aus — oder
    /// nur zusammengetragen und stünden sonst doppelt darin.
    /// </summary>
    private static bool IsUnloadedCollection(Type type, string propertyName) =>
        (type == typeof(AssetTag) && Is(propertyName, nameof(AssetTag.Assignments)))
        // Die Zuordnungen stehen an den Assets.
        || (type == typeof(ContentType) && Is(propertyName, nameof(ContentType.InheritedFields)))
        // Geerbte Felder stehen an der Eltern-Art.
        || (type == typeof(ContentType) && Is(propertyName, nameof(ContentType.Children)));
        // Die Unterarten stehen ohnehin als eigene Einträge in derselben Liste.

    private static bool Is(string propertyName, string name) =>
        propertyName.Equals(name, StringComparison.OrdinalIgnoreCase);
}
