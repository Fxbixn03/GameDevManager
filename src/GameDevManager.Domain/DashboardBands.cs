namespace GameDevManager.Domain;

/// <summary>
/// Die Bänder des Dashboards. Ein Band ist ein waagerechter Abschnitt mit einer eigenen Frage:
/// wie steht das Projekt, wo war ich, was ist kaputt, was ist überhaupt da.
/// <para>
/// Die Schlüssel landen als <c>CardKey</c> in der Datenbank und dürfen sich nicht mehr ändern.
/// Vorher standen dort die Modul-Schlüssel — das Dashboard war eine Kartenwand mit einer Karte
/// je Modul. Solche Zeilen sind heute unbekannt und werden schlicht übergangen; es ist
/// Werkzeug-Konfiguration und steht in keinem Export.
/// </para>
/// </summary>
public static class DashboardBands
{
    /// <summary>Projektleiste: Name, Umfang, Zustand in einer Zahl, letzter Exportstand.</summary>
    public const string Project = "project";

    /// <summary>Zuletzt bearbeitete Entitäten quer durch alle Module.</summary>
    public const string Recent = "recent";

    /// <summary>Die Health Checks als Liste mit Fundzahl.</summary>
    public const string Health = "health";

    /// <summary>Wie viel Inhalt in welchem Bearbeitungsstand steht — Entwurf bis fertig.</summary>
    public const string Status = "status";

    /// <summary>Die angehefteten Entitäten des angemeldeten Benutzers.</summary>
    public const string Pinned = "pinned";

    /// <summary>Die offenen Kanban-Karten, die dem angemeldeten Benutzer zugewiesen sind.</summary>
    public const string Tasks = "tasks";

    /// <summary>Die offenen Abnahmen, die dem angemeldeten Benutzer zugewiesen sind.</summary>
    public const string Reviews = "reviews";

    /// <summary>Wer gerade woran arbeitet — die Team-Sicht auf die Präsenz.</summary>
    public const string Presence = "presence";

    /// <summary>Offene Anmerkungen an Entitäten des Projekts.</summary>
    public const string Comments = "comments";

    /// <summary>Alle Module als Zahlen-Chips, nach Arbeitsfeld gruppiert.</summary>
    public const string Inventory = "inventory";

    /// <summary>Provider und Verbindungsstatus — Einrichtungsdiagnose, deshalb standardmäßig aus.</summary>
    public const string Database = "database";

    /// <summary>Voreingestellte Reihenfolge: von dringlich nach nachschlagend.</summary>
    public static readonly IReadOnlyList<string> All =
        [Project, Pinned, Tasks, Reviews, Presence, Comments, Recent, Health, Status, Inventory, Database];

    /// <summary>
    /// Bänder, die ohne eigene Zeile <b>nicht</b> erscheinen. Bisher nur die Datenbank: Provider
    /// und Verbindung interessieren einmal bei der Einrichtung, danach erst wieder im Fehlerfall
    /// — und der meldet sich ohnehin in der Projektleiste.
    /// </summary>
    public static bool IsHiddenByDefault(string bandKey) => bandKey == Database;
}
