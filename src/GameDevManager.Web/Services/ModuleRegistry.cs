using GameDevManager.Domain;
using MudBlazor;

namespace GameDevManager.Web.Services;

/// <summary>
/// Ein fachliches Modul des GameDevManagers (Items, NPCs, Karten, …).
/// </summary>
/// <param name="Id">Modul-Schlüssel aus <see cref="ModuleKeys"/> — zugleich Routensegment.</param>
/// <param name="Implemented">
/// Das Modul hat eine eigene Oberfläche. Ist es <c>false</c>, landet der Aufruf auf der
/// Platzhalterseite.
/// </param>
/// <remarks>
/// Name und Beschreibung stehen bewusst nicht hier, sondern in <see cref="ModuleLabels"/>:
/// Die Registry ist statisch und kennt zur Feldinitialisierung noch keinen Localizer.
/// </remarks>
public record ModuleDefinition(string Id, string Icon, bool Implemented = false)
{
    public string Route => $"/modules/{Id}";
}

/// <summary>
/// Zentrale Definition aller Module. Topbar und Dashboard speisen sich hieraus —
/// neue Module werden nur hier ergänzt.
/// </summary>
public static class ModuleRegistry
{
    public static readonly IReadOnlyList<ModuleDefinition> All =
    [
        new(ModuleKeys.Items, Icons.Material.Filled.Category, Implemented: true),
        new(ModuleKeys.Crafting, Icons.Material.Filled.Construction, Implemented: true),
        new(ModuleKeys.Currencies, Icons.Material.Filled.Paid, Implemented: true),
        new(ModuleKeys.Npcs, Icons.Material.Filled.People, Implemented: true),
        new(ModuleKeys.Factions, Icons.Material.Filled.Flag, Implemented: true),
        new(ModuleKeys.Diplomacy, Icons.Material.Filled.Handshake, Implemented: true),
        new(ModuleKeys.Maps, Icons.Material.Filled.Map, Implemented: true),
        new(ModuleKeys.Dialogs, Icons.Material.Filled.Chat, Implemented: true),
        new(ModuleKeys.Story, Icons.Material.Filled.AutoStories, Implemented: true),
        new(ModuleKeys.Quests, Icons.Material.Filled.Assignment, Implemented: true),
        new(ModuleKeys.Assets, Icons.Material.Filled.PhotoLibrary, Implemented: true),
        new(ModuleKeys.Player, Icons.Material.Filled.Person, Implemented: true),
        new(ModuleKeys.Classes, Icons.Material.Filled.School, Implemented: true),
        new(ModuleKeys.Loot, Icons.Material.Filled.Casino, Implemented: true),
        new(ModuleKeys.Effects, Icons.Material.Filled.AutoAwesome, Implemented: true),
        new(ModuleKeys.Achievements, Icons.Material.Filled.EmojiEvents, Implemented: true),
        new(ModuleKeys.Collectibles, Icons.Material.Filled.Collections, Implemented: true),
        new(ModuleKeys.Events, Icons.Material.Filled.Event, Implemented: true),
        new(ModuleKeys.Tags, Icons.Material.Filled.Label, Implemented: true),
        new(ModuleKeys.Audio, Icons.Material.Filled.MusicNote, Implemented: true),
        new(ModuleKeys.Cutscenes, Icons.Material.Filled.Movie, Implemented: true),
        new(ModuleKeys.Statistics, Icons.Material.Filled.BarChart, Implemented: true)
    ];

    public static ModuleDefinition? Find(string id) =>
        All.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Wirft, wenn der Schlüssel unbekannt ist — für Seiten, die zu einem festen Modul gehören.</summary>
    public static ModuleDefinition Get(string id) =>
        Find(id) ?? throw new ArgumentOutOfRangeException(nameof(id), id, "Unbekanntes Modul.");
}
