using GameDevManager.Data.Services;

namespace GameDevManager.Web.Services;

/// <summary>
/// Der Wartungslauf, der das Änderungsprotokoll auf die eingestellte Aufbewahrung kürzt
/// (<see cref="ChangeLogRetentionOptions"/>) — einmal beim Start und danach in festem Abstand.
/// <para>
/// Ein Hintergrunddienst und nicht ein Anhängsel des Speicherns: Das Protokoll wächst bei
/// jeder Änderung, und bei jeder Änderung aufzuräumen hieße, den häufigsten Vorgang des Tools
/// mit einer Abfrage zu belasten, die fast immer nichts findet. Ebenso wenig gehört es an die
/// Protokollseite — dann räumte nur auf, wer zufällig hinsieht.
/// </para>
/// <para>
/// Gearbeitet wird in einem eigenen Scope: Die Context-Factory ist scoped registriert, und
/// außerhalb von Anfrage und Blazor-Kreis gilt „alles erlaubt“ — das Verwalterrecht, das
/// <see cref="ChangeLogService.PruneAllProjectsAsync"/> verlangt, ist damit erfüllt, ohne dass
/// es dafür einen ungeprüften Weg an der Rechteprüfung vorbei geben müsste.
/// </para>
/// </summary>
public sealed class ChangeLogMaintenance(
    IServiceScopeFactory scopes,
    ChangeLogRetentionOptions retention,
    ILogger<ChangeLogMaintenance> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!retention.HasRetentionRule)
        {
            // Ohne Grenzen gibt es nichts zu tun — dann läuft auch kein Zeitgeber mit.
            return;
        }

        var interval = TimeSpan.FromHours(Math.Clamp(retention.SweepHours, 1, 24 * 30));
        using var timer = new PeriodicTimer(interval);

        try
        {
            do
            {
                await SweepAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Die Anwendung fährt herunter — kein Fehler.
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var changeLog = scope.ServiceProvider.GetRequiredService<ChangeLogService>();

            var removed = await changeLog.PruneAllProjectsAsync(ct);

            if (removed > 0)
            {
                log.LogInformation(
                    "Änderungsprotokoll aufgeräumt: {Removed} Einträge entfernt.", removed);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Eine noch nicht migrierte oder gerade nicht erreichbare Datenbank darf den
            // Hintergrunddienst nicht beenden — beim nächsten Durchlauf steht sie vielleicht.
            log.LogWarning(ex, "Das Änderungsprotokoll konnte nicht aufgeräumt werden.");
        }
    }
}
