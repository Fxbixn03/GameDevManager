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
public record ModuleDefinition(string Id, string Name, string Icon, string Description, bool Implemented = false)
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
        new(ModuleKeys.Items, "Items", Icons.Material.Filled.Category, "Items und Item-Arten mit eigenen Feldern definieren", Implemented: true),
        new(ModuleKeys.Crafting, "Crafting", Icons.Material.Filled.Construction, "Rezepte und Crafting-Trees auf Basis der Items", Implemented: true),
        new(ModuleKeys.Currencies, "Währungen", Icons.Material.Filled.Paid, "Spielwährungen in beliebigen Variationen", Implemented: true),
        new(ModuleKeys.Npcs, "NPCs", Icons.Material.Filled.People, "NPCs und Mobs, Händler, Spawns und Bedingungen", Implemented: true),
        new(ModuleKeys.Factions, "Fraktionen", Icons.Material.Filled.Flag, "Fraktionen, Rollen und Ränge für NPCs", Implemented: true),
        new(ModuleKeys.Diplomacy, "Diplomatie", Icons.Material.Filled.Handshake, "Allianzen und Feindschaften als Graph", Implemented: true),
        new(ModuleKeys.Maps, "Karten", Icons.Material.Filled.Map, "Welt- und Detailkarten mit Markern und Gebieten", Implemented: true),
        new(ModuleKeys.Dialogs, "Dialoge", Icons.Material.Filled.Chat, "Dialoge, Sprechblasen und Antwortmöglichkeiten", Implemented: true),
        new(ModuleKeys.Story, "Story", Icons.Material.Filled.AutoStories, "Storyline im Zeitstreifen mit Verknüpfungen", Implemented: true),
        new(ModuleKeys.Quests, "Quests", Icons.Material.Filled.Assignment, "Haupt-/Nebenmissionen und Events mit Bedingungen", Implemented: true),
        new(ModuleKeys.Assets, "Assets", Icons.Material.Filled.PhotoLibrary, "Sprite-Bibliothek über alle Entitäten", Implemented: true),
        new(ModuleKeys.Player, "Spieler", Icons.Material.Filled.Person, "Spielerfigur und Skilltrees", Implemented: true),
        new(ModuleKeys.Classes, "Klassen", Icons.Material.Filled.School, "Klassen für Spieler und NPCs", Implemented: true),
        new(ModuleKeys.Loot, "Loot-Tables", Icons.Material.Filled.Casino, "Drop-Wahrscheinlichkeiten und Mengen", Implemented: true),
        new(ModuleKeys.Effects, "Effekte", Icons.Material.Filled.AutoAwesome, "Effekte und deren Wirkung, z. B. Verbrennung", Implemented: true),
        new(ModuleKeys.Achievements, "Achievements", Icons.Material.Filled.EmojiEvents, "Erfolge, die der Spieler erreichen kann"),
        new(ModuleKeys.Collectibles, "Sammelobjekte", Icons.Material.Filled.Collections, "Statuen, Notizen und andere Sammelobjekte"),
        new(ModuleKeys.Events, "Events", Icons.Material.Filled.Event, "Zufalls-Events mit Spawns, Loot und Orten", Implemented: true),
        new(ModuleKeys.Tags, "Tags", Icons.Material.Filled.Label, "Tags/Labels, modulübergreifend einsetzbar"),
        new(ModuleKeys.Audio, "SFX/Audio", Icons.Material.Filled.MusicNote, "Sounds und Audio (noch offen)"),
        new(ModuleKeys.Cutscenes, "Cutscenes", Icons.Material.Filled.Movie, "Cutscenes (noch offen)"),
        new(ModuleKeys.Statistics, "Statistik", Icons.Material.Filled.BarChart, "Kennzahlen und Health Checks über alle Module")
    ];

    public static ModuleDefinition? Find(string id) =>
        All.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Wirft, wenn der Schlüssel unbekannt ist — für Seiten, die zu einem festen Modul gehören.</summary>
    public static ModuleDefinition Get(string id) =>
        Find(id) ?? throw new ArgumentOutOfRangeException(nameof(id), id, "Unbekanntes Modul.");
}
