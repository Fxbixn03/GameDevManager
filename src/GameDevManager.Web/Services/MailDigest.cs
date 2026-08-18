using GameDevManager.Data.Services;

namespace GameDevManager.Web.Services;

/// <summary>
/// Verschickt den Mail-Digest im eingestellten Takt (<see cref="MailOptions.DigestMinutes"/>,
/// Vorgabe 15 Minuten) — gebündelt statt je Ereignis, damit aus einer Bearbeitungssitzung
/// keine zwanzig Mails werden.
/// <para>
/// Dasselbe Muster wie <see cref="ChangeLogMaintenance"/>: Hintergrunddienst, eigener Scope,
/// ein Fehler beendet ihn nicht. Ohne SMTP-Konfiguration startet er gar nicht erst — dann
/// läuft auch kein Zeitgeber mit.
/// </para>
/// <para>
/// Der Startpunkt ist der Prozessstart, nicht „alles seit je“: Nach einem Neustart soll
/// niemand die Ereignisse der letzten Woche noch einmal bekommen. Was zwischen Stopp und
/// Start passiert, geht verloren — der Aktivitäts-Feed bleibt die vollständige Auskunft,
/// die Mail ist nur der Anstoß, hineinzusehen.
/// </para>
/// </summary>
public sealed class MailDigest(
    IServiceScopeFactory scopes,
    IMailSender sender,
    MailOptions options,
    BackgroundRunTracker runs,
    ILogger<MailDigest> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!sender.IsConfigured)
        {
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Clamp(options.DigestMinutes, 1, 24 * 60));
        using var timer = new PeriodicTimer(interval);

        var since = DateTime.UtcNow;

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                since = await RunAsync(since, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Die Anwendung fährt herunter — kein Fehler.
        }
    }

    private async Task<DateTime> RunAsync(DateTime sinceUtc, CancellationToken ct)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        // Der Schnitt liegt vor dem Sammeln: Was während des Laufs passiert, gehört in den
        // nächsten — sonst fiele es in die Lücke zwischen Abfrage und Zeitstempel.
        var next = DateTime.UtcNow;

        try
        {
            using var scope = scopes.CreateScope();

            var sent = await scope.ServiceProvider
                .GetRequiredService<MailNotificationService>()
                .SendDigestAsync(sinceUtc, ct);

            if (sent > 0)
            {
                log.LogInformation("Mail-Digest verschickt: {Sent} Mails.", sent);
            }

            runs.Record(BackgroundRunTracker.MailDigest, watch.Elapsed, success: true);

            return next;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Eine gerade nicht erreichbare Datenbank darf den Takt nicht beenden — und der
            // Zeitraum bleibt stehen, damit die Ereignisse im nächsten Lauf nachkommen.
            log.LogWarning(ex, "Der Mail-Digest konnte nicht verschickt werden.");
            runs.Record(BackgroundRunTracker.MailDigest, watch.Elapsed, success: false);

            return sinceUtc;
        }
    }
}
