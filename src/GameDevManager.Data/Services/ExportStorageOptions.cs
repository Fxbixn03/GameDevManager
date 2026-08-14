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

    /// <summary>Der aufgelöste absolute Pfad. Wird beim Registrieren gesetzt.</summary>
    public string RootPath { get; set; } = string.Empty;
}
