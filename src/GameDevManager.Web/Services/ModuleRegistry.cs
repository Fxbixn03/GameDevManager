using MudBlazor;

namespace GameDevManager.Web.Services;

/// <summary>
/// Ein fachliches Modul des GameDevManagers (Items, NPCs, Karten, …).
/// </summary>
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
        new("items", "Items", Icons.Material.Filled.Category, "Items und Item-Arten mit eigenen Feldern definieren"),
        new("crafting", "Crafting", Icons.Material.Filled.Construction, "Rezepte und Crafting-Trees auf Basis der Items"),
        new("currencies", "Währungen", Icons.Material.Filled.Paid, "Spielwährungen in beliebigen Variationen"),
        new("npcs", "NPCs", Icons.Material.Filled.People, "NPCs und Mobs, Händler, Spawns und Bedingungen"),
        new("factions", "Fraktionen", Icons.Material.Filled.Flag, "Fraktionen, Rollen und Ränge für NPCs"),
        new("diplomacy", "Diplomatie", Icons.Material.Filled.Handshake, "Allianzen und Feindschaften als Graph"),
        new("maps", "Karten", Icons.Material.Filled.Map, "Welt- und Detailkarten mit Markern und Gebieten"),
        new("dialogs", "Dialoge", Icons.Material.Filled.Chat, "Dialoge, Sprechblasen und Antwortmöglichkeiten"),
        new("story", "Story", Icons.Material.Filled.AutoStories, "Storyline im Zeitstreifen mit Verknüpfungen"),
        new("quests", "Quests", Icons.Material.Filled.Assignment, "Haupt-/Nebenmissionen und Events mit Bedingungen"),
        new("assets", "Assets", Icons.Material.Filled.PhotoLibrary, "Sprite-Bibliothek über alle Entitäten"),
        new("player", "Spieler", Icons.Material.Filled.Person, "Spielerfigur und Skilltrees"),
        new("classes", "Klassen", Icons.Material.Filled.School, "Klassen für Spieler und NPCs"),
        new("loot", "Loot-Tables", Icons.Material.Filled.Casino, "Drop-Wahrscheinlichkeiten und Mengen"),
        new("effects", "Effekte", Icons.Material.Filled.AutoAwesome, "Effekte und deren Wirkung, z. B. Verbrennung"),
        new("achievements", "Achievements", Icons.Material.Filled.EmojiEvents, "Erfolge, die der Spieler erreichen kann"),
        new("collectibles", "Sammelobjekte", Icons.Material.Filled.Collections, "Statuen, Notizen und andere Sammelobjekte"),
        new("events", "Events", Icons.Material.Filled.Event, "Zufalls-Events mit Spawns, Loot und Orten"),
        new("tags", "Tags", Icons.Material.Filled.Label, "Tags/Labels, modulübergreifend einsetzbar"),
        new("audio", "SFX/Audio", Icons.Material.Filled.MusicNote, "Sounds und Audio (noch offen)"),
        new("cutscenes", "Cutscenes", Icons.Material.Filled.Movie, "Cutscenes (noch offen)"),
        new("statistics", "Statistik", Icons.Material.Filled.BarChart, "Kennzahlen und Health Checks über alle Module")
    ];

    public static ModuleDefinition? Find(string id) =>
        All.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
}
