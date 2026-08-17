namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein Benutzer des Tools. Das Konzept verlangt für den Changelog, „welcher angemeldete
/// Benutzer welche Änderungen getan hat“ — dafür braucht es zuerst angemeldete Benutzer.
/// <para>
/// Benutzer hängen wie die Projekte selbst an keinem Projekt: Eine Installation wird von einem
/// Team betrieben, das an mehreren Projekten arbeitet. Sich je Projekt neu anzulegen wäre eine
/// Verwaltung, die niemand will.
/// </para>
/// <para>
/// Das Passwort steht nur als Hash da (PBKDF2, siehe <c>PasswordHasher</c>), und es gibt kein
/// vorbelegtes Konto: Der erste Start führt in die Ersteinrichtung, in der man das erste Konto
/// selbst anlegt. Ein ausgeliefertes Standardpasswort wäre auf jeder Installation dasselbe.
/// </para>
/// </summary>
public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Anmeldename, installationsweit eindeutig. Verglichen wird kleingeschrieben.</summary>
    public required string UserName { get; set; }

    /// <summary>Angezeigter Name — er steht später an jedem Eintrag des Änderungsprotokolls.</summary>
    public required string DisplayName { get; set; }

    /// <summary>Format und Verfahren stehen im Hash selbst — siehe <c>PasswordHasher</c>.</summary>
    public required string PasswordHash { get; set; }

    /// <summary>
    /// Darf Benutzer anlegen, umbenennen und entfernen. Der erste Benutzer bekommt das Recht
    /// bei der Ersteinrichtung; ohne wenigstens einen Verwalter käme man nicht mehr an die
    /// Benutzerverwaltung heran.
    /// </summary>
    public bool IsAdministrator { get; set; }

    /// <summary>
    /// Gesperrt: Die Anmeldung wird abgewiesen, der Benutzer bleibt aber stehen. Ein gelöschter
    /// Benutzer verlöre den Bezug seiner Protokolleinträge — die tragen zwar seinen Namen als
    /// Momentaufnahme, aber nicht mehr, wer er war.
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    /// Darf Inhalte anlegen, ändern und löschen. Ohne das Recht ist der Benutzer ein Leser:
    /// Er sieht alles, was seine Module hergeben, aber jedes Speichern wird abgewiesen.
    /// Verwalter haben immer alle Rechte — die Spalten hier zählen für sie nicht.
    /// </summary>
    public bool CanWrite { get; set; } = true;

    /// <summary>Darf den Export nutzen — Download und Exportstände.</summary>
    public bool CanExport { get; set; } = true;

    /// <summary>Darf den Import nutzen. Ein Import schreibt — er braucht zusätzlich <see cref="CanWrite"/>.</summary>
    public bool CanImport { get; set; } = true;

    /// <summary>
    /// Die Modul-Schlüssel, die dieser Benutzer sehen darf, kommagetrennt — <c>null</c> heißt
    /// alle. Eine Textspalte statt einer Zuordnungstabelle: Die Liste wird nie abgefragt,
    /// nur als Ganzes gelesen, und eine Tabelle verlangte vier Migrationen mehr.
    /// </summary>
    public string? AllowedModuleKeys { get; set; }

    /// <summary>
    /// Die Rolle, aus der die Rechte kommen, solange <see cref="OverridesRole"/> nicht gesetzt
    /// ist. Echter Fremdschlüssel mit SetNull — eine gelöschte Rolle nimmt das Konto nicht mit;
    /// ihre Rechte werden vorher auf die Konten gestempelt, damit niemand still mehr bekommt.
    /// </summary>
    public Guid? RoleId { get; set; }

    public UserRole? Role { get; set; }

    /// <summary>
    /// Das Konto weicht von seiner Rolle ab: Es gelten die eigenen Spalten statt der Rolle.
    /// Ein Schalter statt vier einzelner Nullwerte — abgewichen wird ganz oder gar nicht,
    /// sonst wäre in der Maske nicht mehr zu sehen, woher ein Recht kommt.
    /// </summary>
    public bool OverridesRole { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAtUtc { get; set; }

    /// <summary>
    /// Bis wohin der Benutzer den Aktivitäts-Feed gelesen hat. <c>null</c> heißt „noch nie“ —
    /// dann gilt alles seit seiner ersten Anmeldung als neu, nicht der gesamte Bestand.
    /// <para>
    /// Eine Spalte am Konto und keine eigene Tabelle: Es ist genau ein Zeitpunkt je Benutzer,
    /// und der Feed selbst hat keine Daten — er liest das Änderungsprotokoll, das es ohnehin
    /// gibt.
    /// </para>
    /// </summary>
    public DateTime? FeedReadAtUtc { get; set; }
}
