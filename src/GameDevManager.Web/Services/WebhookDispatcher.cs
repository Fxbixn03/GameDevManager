using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;

namespace GameDevManager.Web.Services;

/// <summary>
/// Ruft die Webhooks eines Projekts auf, wenn sich etwas geändert hat (F37).
/// <para>
/// Ein Hintergrunddienst mit Warteschlange und <b>nicht</b> ein Aufruf im <c>SaveChanges</c>:
/// Eine hängende HTTP-Anfrage darf keine Transaktion aufhalten — und der Empfänger ist ein
/// fremder Server, dessen Erreichbarkeit niemand garantiert. Der
/// <see cref="ChangeLogInterceptor"/> stellt ein, dieser Dienst stellt zu.
/// </para>
/// <para>
/// Zugestellt wird <b>gebündelt</b>: Wer eine Maske speichert, erzeugt eine Änderung, wer
/// zwanzig Zeilen bearbeitet, zwanzig — daraus wird ein Aufruf mit zwanzig Einträgen. Ein
/// Aufruf je Änderung machte aus einer Bearbeitungssitzung einen Sturm.
/// </para>
/// </summary>
public sealed class WebhookDispatcher(
    IServiceScopeFactory scopes,
    WebhookQueue queue,
    IHttpClientFactory clients,
    BackgroundRunTracker runs,
    ILogger<WebhookDispatcher> log) : BackgroundService
{
    /// <summary>
    /// Wie oft die Schlange geleert wird. Fünf Sekunden bündeln die Änderungen einer Maske
    /// zuverlässig und sind für einen Build-Server immer noch „sofort“.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Wie oft der Bestand an Webhooks neu gelesen wird. Ein neu angelegter Webhook meldet sich
    /// über <c>WebhookQueue.HasSubscribers</c> sofort; dieser Takt fängt den umgekehrten Fall
    /// ab — einen gelöschten, den sonst niemand abmeldet.
    /// </summary>
    private static readonly TimeSpan ReloadInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Abstände zwischen den Versuchen. Drei Versuche: Ein Empfänger, der nach einer Minute
    /// nicht steht, steht auch nach zehn nicht — und die nächste Änderung ruft ohnehin wieder an.
    /// </summary>
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.Zero, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)
    ];

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private List<Webhook> _subscribers = [];
    private DateTime _loadedAtUtc = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await TickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Die Anwendung fährt herunter — kein Fehler.
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await RefreshSubscribersAsync(ct);

            var pending = queue.DrainAll();

            if (pending.Count == 0 || _subscribers.Count == 0)
            {
                // Ein leerer Takt ist kein Lauf — er soll die Kennzahl „letzte Zustellung“
                // nicht alle fünf Sekunden überschreiben.
                return;
            }

            foreach (var hook in _subscribers)
            {
                var relevant = pending
                    .Where(entry => entry.GameProjectId == hook.GameProjectId
                        && WebhookService.Listens(hook, entry.ModuleKey))
                    .ToList();

                if (relevant.Count > 0)
                {
                    await DeliverAsync(hook, relevant, ct);
                }
            }

            runs.Record(BackgroundRunTracker.WebhookDispatcher, watch.Elapsed, success: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Eine noch nicht migrierte oder gerade nicht erreichbare Datenbank darf den
            // Hintergrunddienst nicht beenden.
            log.LogWarning(ex, "Die Webhooks konnten nicht zugestellt werden.");
            runs.Record(BackgroundRunTracker.WebhookDispatcher, watch.Elapsed, success: false);
        }
    }

    private async Task RefreshSubscribersAsync(CancellationToken ct)
    {
        var stale = DateTime.UtcNow - _loadedAtUtc > ReloadInterval;

        // Neu angelegt heißt: Es gibt jetzt bestimmt einen — dann sofort nachladen, sonst
        // liefe die erste Änderung ins Leere.
        if (!stale && !(queue.HasSubscribers && _subscribers.Count == 0))
        {
            return;
        }

        using var scope = scopes.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<WebhookService>();

        _subscribers = await service.GetActiveAsync(ct);
        _loadedAtUtc = DateTime.UtcNow;

        // Ohne Empfänger wird gar nicht erst eingereiht — das hält das Speichern frei von
        // einer Schlange, die niemand leert.
        queue.HasSubscribers = _subscribers.Count > 0;
    }

    private async Task DeliverAsync(Webhook hook, List<WebhookEvent> events, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                projectId = hook.GameProjectId,
                deliveredAtUtc = DateTime.UtcNow,
                changes = events.Select(entry => new
                {
                    moduleKey = entry.ModuleKey,
                    entityId = entry.EntityId,
                    entityName = entry.EntityName,
                    action = entry.Action.ToString(),
                    userName = entry.UserName,
                    occurredAtUtc = entry.OccurredAtUtc
                })
            },
            Json);

        var client = clients.CreateClient(nameof(WebhookDispatcher));

        int? status = null;
        string? error = null;

        for (var attempt = 0; attempt < Backoff.Length; attempt++)
        {
            if (Backoff[attempt] > TimeSpan.Zero)
            {
                await Task.Delay(Backoff[attempt], ct);
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, hook.Url)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };

                // Der signierte Kopfeintrag: Der Empfänger kann prüfen, dass die Nachricht
                // wirklich von dieser Installation kommt — ohne ihn wäre die URL selbst das
                // einzige Geheimnis.
                if (!string.IsNullOrWhiteSpace(hook.Secret))
                {
                    request.Headers.TryAddWithoutValidation("X-GDM-Signature", Sign(payload, hook.Secret));
                }

                using var response = await client.SendAsync(request, ct);

                status = (int)response.StatusCode;
                error = response.IsSuccessStatusCode ? null : response.ReasonPhrase;

                if (response.IsSuccessStatusCode)
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                status = null;
                error = ex.Message;
            }
        }

        using var scope = scopes.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<WebhookService>()
            .RecordDeliveryAsync(hook.Id, status, error, ct);
    }

    /// <summary>
    /// HMAC-SHA256 über den Nachrichtentext, als Hex. Selbst gerechnet und nicht als
    /// Fremdbibliothek — es ist ein Aufruf, und das Format ist dasselbe, das GitHub und
    /// Stripe benutzen; ein Empfänger kennt es also schon.
    /// </summary>
    private static string Sign(string payload, string secret)
    {
        using var mac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));

        return "sha256=" + Convert.ToHexStringLower(mac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }
}
