using GameDevManager.Data.Services;

namespace GameDevManager.Web.Services;

/// <summary>
/// Legt zur eingestellten Uhrzeit für jedes Projekt einen Exportstand an — die Sicherung, die
/// auch dann entsteht, wenn niemand daran gedacht hat.
/// <para>
/// Dasselbe Muster wie <see cref="ChangeLogMaintenance"/>: Hintergrunddienst, eigener Scope,
/// ein Fehler beendet ihn nicht. Das Sicherheitsnetz greift bisher nur bei zerstörenden
/// Aktionen (ersetzender Import, Projekt löschen); ein ganz normaler Arbeitstag hinterlässt
/// ohne diesen Lauf keinen Stand.
/// </para>
/// <para>
/// Der Zeitpunkt ist eine <b>Uhrzeit</b> (<see cref="ExportStorageOptions.ScheduleTime"/>) und
/// kein Cron-Ausdruck — siehe die Begründung dort. Gewartet wird bis zur nächsten Fälligkeit
/// statt in festem Abstand: „Jeden Abend um 3“ heißt um 3 und nicht 24 Stunden nach dem Start.
/// </para>
/// </summary>
public sealed class ScheduledExportSnapshots(
    IServiceScopeFactory scopes,
    ExportStorageOptions options,
    ILogger<ScheduledExportSnapshots> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.DailyTime is not { } time)
        {
            // Ohne Zeitplan gibt es nichts zu tun — dann läuft auch kein Zeitgeber mit.
            return;
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(ExportStorageOptions.UntilNext(time, DateTime.Now), stoppingToken);
                await RunAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Die Anwendung fährt herunter — kein Fehler.
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var snapshots = scope.ServiceProvider.GetRequiredService<ExportSnapshotService>();

            var created = await snapshots.CreateScheduledAsync(options.ScheduleIncludesAssets, ct);

            if (created > 0)
            {
                log.LogInformation("Geplante Exportstände angelegt: {Created}.", created);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Eine gerade nicht erreichbare Datenbank oder ein volles Verzeichnis darf den
            // Zeitplan nicht beenden — morgen steht vielleicht beides wieder.
            log.LogWarning(ex, "Die geplanten Exportstände konnten nicht angelegt werden.");
        }
    }
}
