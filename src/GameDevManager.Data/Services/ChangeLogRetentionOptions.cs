namespace GameDevManager.Data.Services;

/// <summary>
/// Wird aus dem Konfigurationsabschnitt "ChangeLog" gebunden: wie lange das Änderungsprotokoll
/// zurückreicht. Nach einem Jahr Arbeit stehen dort sehr viele Zeilen, und anders als der
/// Spielinhalt wächst das Protokoll bei jedem Speichern weiter.
/// <para>
/// Konfiguration und keine Tabelle — dieselbe Überlegung wie bei der Passwortrichtlinie und
/// der Aufbewahrung der Exportstände: eine Angabe der Installation, für die vier Migrationen
/// unangemessen wären.
/// </para>
/// </summary>
public class ChangeLogRetentionOptions
{
    public const string SectionName = "ChangeLog";

    /// <summary>
    /// Höchstalter eines Eintrags in Tagen; ältere fallen weg. <c>0</c> (oder weniger) hebt die
    /// Grenze auf. Die Vorgabe ist ein Jahr — der Horizont, ab dem ein einzelner Eintrag
    /// praktisch nichts mehr beantwortet, was nicht auch der aktuelle Stand beantwortet.
    /// </summary>
    public int MaxAgeDays { get; set; } = 365;

    /// <summary>
    /// Wie viele Einträge je Projekt stehen bleiben, jüngste zuerst; darüber hinaus fällt weg,
    /// was am längsten zurückliegt. <c>0</c> (oder weniger) hebt die Grenze auf — die Vorgabe,
    /// weil das Höchstalter allein die Frage „wie weit reicht das Protokoll zurück?“ schon
    /// beantwortet. Wer statt eines Zeitraums eine feste Obergrenze will, setzt sie hier.
    /// </summary>
    public int MaxPerProject { get; set; }

    /// <summary>
    /// Wie oft der Wartungslauf prüft. Das Protokoll ist keine Warteschlange — einmal am Tag
    /// reicht, und ein häufigerer Lauf fände beim zweiten Mal ohnehin nichts mehr.
    /// </summary>
    public int SweepHours { get; set; } = 24;

    /// <summary>Ob überhaupt aufgeräumt wird — beide Grenzen aus heißt: alles bleibt stehen.</summary>
    public bool HasRetentionRule => MaxAgeDays > 0 || MaxPerProject > 0;
}
