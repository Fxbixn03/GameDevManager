namespace GameDevManager.Data.Services;

/// <summary>
/// Wird aus dem Konfigurationsabschnitt „RecycleBin“ gebunden: wie lange der Papierkorb
/// zurückreicht. Konfiguration und keine Tabelle — dieselbe Überlegung wie bei der
/// Passwortrichtlinie, der Aufbewahrung der Exportstände und der des Änderungsprotokolls.
/// </summary>
public class RecycleBinOptions
{
    public const string SectionName = "RecycleBin";

    /// <summary>
    /// Höchstalter eines Eintrags in Tagen; ältere fallen weg. <c>0</c> (oder weniger) hebt die
    /// Grenze auf. Vorgabe sind 30 Tage: Ein Fehlklick fällt binnen Stunden auf, nicht binnen
    /// Monaten — und anders als ein Exportstand hilft ein alter Papierkorb-Eintrag nicht beim
    /// Zurückgehen, er steht nur im Weg.
    /// </summary>
    public int MaxAgeDays { get; set; } = 30;

    /// <summary>
    /// Wie viele Einträge je Projekt stehen bleiben, jüngste zuerst. <c>0</c> (oder weniger)
    /// hebt die Grenze auf — die Vorgabe, weil das Höchstalter die Frage schon beantwortet.
    /// </summary>
    public int MaxPerProject { get; set; }

    /// <summary>Ob überhaupt aufbewahrt wird. Aus heißt: gelöscht ist gelöscht, wie vorher.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Ob überhaupt aufgeräumt wird — beide Grenzen aus heißt: alles bleibt stehen.</summary>
    public bool HasRetentionRule => MaxAgeDays > 0 || MaxPerProject > 0;
}
