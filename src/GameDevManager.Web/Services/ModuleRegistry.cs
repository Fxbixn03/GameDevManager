using GameDevManager.Domain;
using MudBlazor;

namespace GameDevManager.Web.Services;

/// <summary>
/// Arbeitsfeld, zu dem ein Modul gehört. Der Inhaltsbestand des Dashboards gruppiert danach:
/// 23 gleichrangige Einträge sind eine Wand, sechs Gruppen mit drei bis fünf Modulen liest man.
/// <para>
/// Bewusst eine zweite Ordnung neben der Reihenfolge in <see cref="ModuleRegistry.All"/> — die
/// ist die Reihenfolge der Umsetzung und trägt die Modulleiste der Topbar; diese hier ist die
/// Reihenfolge der Arbeit.
/// </para>
/// </summary>
public enum ModuleGroup
{
    /// <summary>Karten, Fraktionen, Diplomatie — der Schauplatz.</summary>
    World,

    /// <summary>Items und alles, was ihren Wert und ihre Herkunft beschreibt.</summary>
    Content,

    /// <summary>NPCs, Spielerfiguren und was sie können.</summary>
    Characters,

    /// <summary>Was erzählt wird und wodurch es ausgelöst wird.</summary>
    Narrative,

    /// <summary>Was der Spieler sammelt und freischaltet.</summary>
    Progress,

    /// <summary>Dateien und Ordnung — Assets, Audio, Tags.</summary>
    Production,

    /// <summary>Auswertende Module ohne eigenen Inhalt; sie stehen nicht im Inhaltsbestand.</summary>
    Tools
}

/// <summary>
/// Ein fachliches Modul des GameDevManagers (Items, NPCs, Karten, …).
/// </summary>
/// <param name="Id">Modul-Schlüssel aus <see cref="ModuleKeys"/> — zugleich Routensegment.</param>
/// <param name="Group">Arbeitsfeld für den Inhaltsbestand des Dashboards.</param>
/// <param name="Implemented">
/// Das Modul hat eine eigene Oberfläche. Ist es <c>false</c>, landet der Aufruf auf der
/// Platzhalterseite.
/// </param>
/// <remarks>
/// Name und Beschreibung stehen bewusst nicht hier, sondern in <see cref="ModuleLabels"/>:
/// Die Registry ist statisch und kennt zur Feldinitialisierung noch keinen Localizer.
/// </remarks>
public record ModuleDefinition(string Id, string Icon, ModuleGroup Group, bool Implemented = false)
{
    public string Route => $"/modules/{Id}";
}

/// <summary>
/// Zentrale Definition aller Module. Topbar und Dashboard speisen sich hieraus —
/// neue Module werden nur hier ergänzt.
/// </summary>
/// <remarks>
/// Die Reihenfolge dieser Liste ist die der Umsetzung; sie ist die Vorgabe der Modulleiste in
/// der Topbar (umsortierbar über die <see cref="TopbarSelection"/>) und wird bewusst nicht nach
/// <see cref="ModuleGroup"/> umsortiert — die Leiste steht auf jeder Seite, und eine wandernde
/// Icon-Reihe kostet mehr, als die Gruppierung dort einbrächte.
/// </remarks>
public static class ModuleRegistry
{
    public static readonly IReadOnlyList<ModuleDefinition> All =
    [
        new(ModuleKeys.Items, Icons.Material.Filled.Category, ModuleGroup.Content, Implemented: true),
        new(ModuleKeys.Crafting, Icons.Material.Filled.Construction, ModuleGroup.Content, Implemented: true),
        new(ModuleKeys.Currencies, Icons.Material.Filled.Paid, ModuleGroup.Content, Implemented: true),
        new(ModuleKeys.Rarities, Icons.Material.Filled.Diamond, ModuleGroup.Content, Implemented: true),
        new(ModuleKeys.Npcs, Icons.Material.Filled.People, ModuleGroup.Characters, Implemented: true),
        new(ModuleKeys.Factions, Icons.Material.Filled.Flag, ModuleGroup.World, Implemented: true),
        new(ModuleKeys.Diplomacy, Icons.Material.Filled.Handshake, ModuleGroup.World, Implemented: true),
        new(ModuleKeys.Maps, Icons.Material.Filled.Map, ModuleGroup.World, Implemented: true),
        new(ModuleKeys.Dialogs, Icons.Material.Filled.Chat, ModuleGroup.Narrative, Implemented: true),
        new(ModuleKeys.Story, Icons.Material.Filled.AutoStories, ModuleGroup.Narrative, Implemented: true),
        new(ModuleKeys.Quests, Icons.Material.Filled.Assignment, ModuleGroup.Narrative, Implemented: true),
        new(ModuleKeys.Assets, Icons.Material.Filled.PhotoLibrary, ModuleGroup.Production, Implemented: true),
        new(ModuleKeys.Player, Icons.Material.Filled.Person, ModuleGroup.Characters, Implemented: true),
        new(ModuleKeys.Classes, Icons.Material.Filled.School, ModuleGroup.Characters, Implemented: true),
        new(ModuleKeys.Loot, Icons.Material.Filled.Casino, ModuleGroup.Content, Implemented: true),
        new(ModuleKeys.Effects, Icons.Material.Filled.AutoAwesome, ModuleGroup.Characters, Implemented: true),
        new(ModuleKeys.Achievements, Icons.Material.Filled.EmojiEvents, ModuleGroup.Progress, Implemented: true),
        new(ModuleKeys.Collectibles, Icons.Material.Filled.Collections, ModuleGroup.Progress, Implemented: true),
        new(ModuleKeys.Events, Icons.Material.Filled.Event, ModuleGroup.Narrative, Implemented: true),
        new(ModuleKeys.Tags, Icons.Material.Filled.Label, ModuleGroup.Production, Implemented: true),
        new(ModuleKeys.Audio, Icons.Material.Filled.MusicNote, ModuleGroup.Production, Implemented: true),
        new(ModuleKeys.Cutscenes, Icons.Material.Filled.Movie, ModuleGroup.Narrative, Implemented: true),
        new(ModuleKeys.World, Icons.Material.Filled.WbTwilight, ModuleGroup.World, Implemented: true),
        new(ModuleKeys.Statistics, Icons.Material.Filled.BarChart, ModuleGroup.Tools, Implemented: true),
        new(ModuleKeys.TechTree, Icons.Material.Filled.AccountTree, ModuleGroup.Tools, Implemented: true),
        new(ModuleKeys.Changelog, Icons.Material.Filled.History, ModuleGroup.Tools, Implemented: true),
        new(ModuleKeys.Connections, Icons.Material.Filled.Hub, ModuleGroup.Tools, Implemented: true),
        new(ModuleKeys.Todo, Icons.Material.Filled.ViewKanban, ModuleGroup.Tools, Implemented: true),
        new(ModuleKeys.Whiteboard, Icons.Material.Filled.Draw, ModuleGroup.Tools, Implemented: true),
        new(ModuleKeys.BulkEdit, Icons.Material.Filled.Checklist, ModuleGroup.Tools, Implemented: true),
        // Werkzeug-Modul und nicht „Produktion“: Es trägt zwar eigene Daten (Sprachen und
        // Übersetzungen), aber keine ContentEntity — Massenbearbeitung, CSV und der
        // Inhaltsbestand des Dashboards fragen nach genau der und fänden hier nichts.
        new(ModuleKeys.Localization, Icons.Material.Filled.Translate, ModuleGroup.Tools, Implemented: true),
        new(ModuleKeys.EnginePresets, Icons.Material.Filled.Extension, ModuleGroup.Tools, Implemented: true)
    ];

    /// <summary>
    /// Die Arbeitsfelder des Inhaltsbestands in ihrer Reihenfolge — <see cref="ModuleGroup.Tools"/>
    /// gehört nicht dazu, diese Module tragen keinen eigenen Inhalt.
    /// </summary>
    public static readonly IReadOnlyList<ModuleGroup> ContentGroups =
    [
        ModuleGroup.World,
        ModuleGroup.Content,
        ModuleGroup.Characters,
        ModuleGroup.Narrative,
        ModuleGroup.Progress,
        ModuleGroup.Production
    ];

    public static ModuleDefinition? Find(string id) =>
        All.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Wirft, wenn der Schlüssel unbekannt ist — für Seiten, die zu einem festen Modul gehören.</summary>
    public static ModuleDefinition Get(string id) =>
        Find(id) ?? throw new ArgumentOutOfRangeException(nameof(id), id, "Unbekanntes Modul.");
}
