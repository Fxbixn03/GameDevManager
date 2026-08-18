namespace GameDevManager.Data.Services;

/// <summary>Der letzte Lauf eines Hintergrunddienstes, wie ihn die Betriebs-Kennzahlen ausgeben.</summary>
/// <param name="Service">Stabiler Schlüssel des Dienstes — er wird zum Prometheus-Label.</param>
/// <param name="LastRunUtc">Wann der letzte Lauf endete.</param>
/// <param name="LastDurationSeconds">Wie lange er gebraucht hat.</param>
/// <param name="LastRunFailed">Ob der letzte Lauf mit einem Fehler endete.</param>
/// <param name="ErrorCount">Fehlgeschlagene Läufe seit dem Start des Prozesses.</param>
public sealed record BackgroundRunInfo(
    string Service,
    DateTime LastRunUtc,
    double LastDurationSeconds,
    bool LastRunFailed,
    int ErrorCount);

/// <summary>
/// Merkt sich Dauer und Ergebnis der Hintergrundläufe (Wartung, Zeitplan-Stände,
/// Webhook-Zustellung), damit <c>/api/v1/metrics</c> sie ausgeben kann.
/// <para>
/// Reiner Arbeitsspeicher und keine Tabelle — dasselbe Muster wie <see cref="EditingPresence"/>:
/// Die Zahlen beschreiben den laufenden Prozess, nach einem Neustart sind sie zu Recht leer.
/// </para>
/// </summary>
public sealed class BackgroundRunTracker
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, (DateTime LastRunUtc, double Seconds, bool Failed, int Errors)> _runs = [];

    /// <summary>Die Schlüssel der bekannten Dienste — an einer Stelle, nicht dreimal im Web-Projekt.</summary>
    public const string ChangeLogMaintenance = "changelog_maintenance";
    public const string ScheduledSnapshots = "scheduled_snapshots";
    public const string WebhookDispatcher = "webhook_dispatcher";

    public void Record(string service, TimeSpan duration, bool success)
    {
        lock (_lock)
        {
            var errors = _runs.TryGetValue(service, out var previous) ? previous.Errors : 0;

            _runs[service] = (DateTime.UtcNow, duration.TotalSeconds, !success, success ? errors : errors + 1);
        }
    }

    /// <summary>Alle bekannten Läufe, stabil nach Schlüssel sortiert.</summary>
    public IReadOnlyList<BackgroundRunInfo> GetAll()
    {
        lock (_lock)
        {
            return
            [
                .. _runs
                    .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => new BackgroundRunInfo(
                        entry.Key,
                        entry.Value.LastRunUtc,
                        entry.Value.Seconds,
                        entry.Value.Failed,
                        entry.Value.Errors))
            ];
        }
    }
}
