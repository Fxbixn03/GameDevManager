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

    /// <summary>
    /// Der Verbindungs-Graph: NPCs und Fraktionen als Netz aus Beziehungen und
    /// Mitgliedschaften. Ein Werkzeug-Modul ohne eigene Entitäten — es zeigt, was das
    /// NPC- und das Fraktions-Modul ohnehin schon tragen.
    /// </summary>
    public const string Connections = "connections";

    /// <summary>
    /// Kanban-Boards der Projektverwaltung. Werkzeug-Daten wie das Änderungsprotokoll:
    /// Sie beschreiben die Arbeit am Spiel, nicht das Spiel — und stehen nicht im Export.
    /// </summary>
    public const string Todo = "todo";

    /// <summary>Whiteboards zum gemeinsamen Skizzieren. Werkzeug-Daten wie die Kanban-Boards.</summary>
    public const string Whiteboard = "whiteboard";

    /// <summary>
    /// Die Lokalisierung der Spielinhalte: Sprachen des Projekts und die Übersetzungen zu
    /// Namen, Beschreibungen und Textfeldern. Ein Modul ohne eigene Entitäten — es übersetzt
    /// die der anderen.
    /// </summary>
    public const string Localization = "localization";

    /// <summary>
    /// Die Presets der Game Engines: Baupläne dafür, wie ein Eintrag eines Moduls als Objekt
    /// in Unity, Unreal oder Godot aussieht. Kein Spielinhalt, sondern eine Vorschrift für
    /// den Export.
    /// </summary>
    public const string EnginePresets = "enginepresets";

    /// <summary>
    /// Die Massenbearbeitung. Ein Werkzeug-Modul ohne eigene Entitäten: Es ändert die Inhalte
    /// der anderen Module — Art, Tags und einzelne Feldwerte für viele Einträge auf einmal.
    /// <para>
    /// Bewusst eine eigene Seite statt einer Mehrfachauswahl in jeder der gut zwanzig
    /// Modul-Listen: Die sind je Modul eigen gebaut (Kachelraster, Tabelle, Zeitstreifen), und
    /// dieselbe Auswahl zwanzigmal nachzubauen hieße, sie zwanzigmal zu pflegen. Über die
    /// <c>IModuleEntitySource</c> deckt eine Seite alle Module ab — auch die künftigen.
    /// </para>
    /// </summary>
    public const string BulkEdit = "bulkedit";

    /// <summary>
    /// Gefilterte Listenansichten über den Bestand eines Moduls, benennbar und wiederfindbar.
    /// Ein Werkzeug-Modul: Es zeigt, was die Inhaltsmodule ohnehin tragen — die gespeicherte
    /// Ansicht selbst gehört zum Benutzer, nicht zum Spiel.
    /// </summary>
    public const string Views = "views";
}
