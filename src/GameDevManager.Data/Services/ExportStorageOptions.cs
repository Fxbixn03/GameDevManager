namespace GameDevManager.Data.Services;

/// <summary>
/// Wird aus dem Konfigurationsabschnitt "Exports" gebunden. Aufbewahrte Exportstände liegen
/// wie die Assets im Dateisystem, nicht in der Datenbank — das hält das Verhalten über alle
/// vier Provider gleich und die Datenbanksicherungen klein.
/// </summary>
public class ExportStorageOptions
{
    public const string SectionName = "Exports";

    /// <summary>
    /// Wurzelverzeichnis der aufbewahrten Exportstände. Ein relativer Pfad wird beim
    /// Registrieren gegen das Anwendungsverzeichnis aufgelöst und landet in <see cref="RootPath"/>.
    /// </summary>
    public string StoragePath { get; set; } = "exports";

    /// <summary>
    /// Wie viele Stände je Projekt aufbewahrt werden; die ältesten fallen darüber hinaus weg.
    /// <c>0</c> (oder weniger) hebt die Grenze auf.
    /// <para>
    /// Die Vorgabe ist bewusst nicht „unbegrenzt“: Seit das Sicherheitsnetz vor jedem
    /// ersetzenden Import und jedem Projektlöschen einen Stand anlegt, wächst das Verzeichnis
    /// von allein — eine Grenze, die man erst einschalten muss, käme für den ersten Nutzer
    /// zu spät.
    /// </para>
    /// </summary>
    public int MaxPerProject { get; set; } = 20;

    /// <summary>
    /// Höchstalter eines Standes in Tagen; ältere fallen weg. <c>0</c> (oder weniger) hebt die
    /// Grenze auf — die Vorgabe, weil ein Alter allein nichts über die Menge sagt: Ein Projekt,
    /// an dem ein halbes Jahr niemand arbeitet, verlöre sonst seine Historie ohne Not.
    /// </summary>
    public int MaxAgeDays { get; set; }

    /// <summary>Ob überhaupt aufgeräumt wird — beide Grenzen aus heißt: alles bleibt liegen.</summary>
    public bool HasRetentionRule => MaxPerProject > 0 || MaxAgeDays > 0;

    /// <summary>
    /// Uhrzeit des täglichen Standes im Format <c>HH:mm</c> (Ortszeit des Servers). Leer heißt
    /// „kein Zeitplan“ — die Vorgabe, denn ein Tool, das ungefragt Archive schreibt, überrascht.
    /// <para>
    /// Bewusst eine <b>Uhrzeit</b> und kein Cron-Ausdruck: Die Anforderung lautet „jeden Abend“,
    /// und ein Cron-Parser wäre entweder eine Fremdbibliothek oder ein eigenes Kleinprojekt für
    /// eine Angabe, die aus Stunde und Minute besteht — dieselbe Abwägung wie beim
    /// <c>ImageDimensionReader</c>, nur andersherum entschieden.
    /// </para>
    /// </summary>
    public string? ScheduleTime { get; set; }

    /// <summary>Ob der Zeitplan an ist und die Uhrzeit lesbar war.</summary>
    public bool HasSchedule => DailyTime is not null;

    /// <summary>
    /// Die geparste Uhrzeit, oder <c>null</c> bei leerer oder unlesbarer Angabe. Eine kaputte
    /// Uhrzeit schaltet den Zeitplan ab, statt die Anwendung am Start zu hindern: Ein Tippfehler
    /// in der Konfiguration darf nicht dazu führen, dass niemand mehr an seine Projekte kommt.
    /// </summary>
    public TimeOnly? DailyTime =>
        TimeOnly.TryParse(ScheduleTime, System.Globalization.CultureInfo.InvariantCulture, out var time)
            ? time
            : null;

    /// <summary>Ob der Zeitplan die Asset-Dateien mitnimmt. Vorgabe ja — sonst ist es keine Sicherung.</summary>
    public bool ScheduleIncludesAssets { get; set; } = true;

    /// <summary>
    /// Die Wartezeit bis zur nächsten Fälligkeit. Ortszeit, weil „jeden Abend“ eine Aussage
    /// über den Arbeitstag ist und nicht über UTC. Genau auf der Uhrzeit zu starten zählt als
    /// „schon gelaufen“ und wartet einen Tag — sonst liefe der Dienst beim Start in dieser
    /// Minute sofort los.
    /// <para>
    /// Steht hier und nicht im Hintergrunddienst, damit die Rechnung ohne die Web-Schicht
    /// prüfbar ist.
    /// </para>
    /// </summary>
    public static TimeSpan UntilNext(TimeOnly time, DateTime now)
    {
        var next = now.Date + time.ToTimeSpan();

        return (next <= now ? next.AddDays(1) : next) - now;
    }

    /// <summary>Der aufgelöste absolute Pfad. Wird beim Registrieren gesetzt.</summary>
    public string RootPath { get; set; } = string.Empty;
}
