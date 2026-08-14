namespace GameDevManager.Domain;

/// <summary>
/// Stabile Schlüssel der fachlichen Module. Sie stehen an Arten, Feldern und Feldwerten und
/// legen fest, zu welchem Modul ein Datensatz gehört — die Feld- und Referenzinfrastruktur ist
/// modulübergreifend und braucht deshalb einen Diskriminator.
/// <para>
/// Die Werte landen in der Datenbank und dürfen sich nicht mehr ändern. Die Modul-Registry der
/// Web-Schicht baut ihre Routen aus denselben Konstanten auf.
/// </para>
/// </summary>
public static class ModuleKeys
{
    public const string Items = "items";
    public const string Crafting = "crafting";
    public const string Currencies = "currencies";
    public const string Rarities = "rarities";
    public const string Npcs = "npcs";
    public const string Factions = "factions";
    public const string Diplomacy = "diplomacy";
    public const string Maps = "maps";
    public const string Dialogs = "dialogs";
    public const string Story = "story";
    public const string Quests = "quests";
    public const string Assets = "assets";
    public const string Player = "player";
    public const string Classes = "classes";
    public const string Loot = "loot";
    public const string Effects = "effects";
    public const string Achievements = "achievements";
    public const string Collectibles = "collectibles";
    public const string Events = "events";
    public const string Tags = "tags";
    public const string Audio = "audio";
    public const string Cutscenes = "cutscenes";

    /// <summary>Tageszeiten, Wetterlagen und Biome — siehe <see cref="Entities.WorldState"/>.</summary>
    public const string World = "world";

    public const string Statistics = "statistics";

    /// <summary>
    /// Der Freischaltungs-Graph. Ein Werkzeug-Modul ohne eigene Entitäten: Es zeigt, was das
    /// Bedingungssystem ohnehin schon trägt.
    /// </summary>
    public const string TechTree = "techtree";

    /// <summary>Das Änderungsprotokoll. Ebenfalls ein Werkzeug-Modul ohne eigene Inhalte.</summary>
    public const string Changelog = "changelog";
}
