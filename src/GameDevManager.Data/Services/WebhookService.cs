using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Verwaltet die Webhooks eines Projekts. Das Zustellen selbst macht der Hintergrunddienst in
/// der Web-Schicht — hier steht nur, wohin und mit welchem Filter.
/// </summary>
public class WebhookService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    WebhookQueue queue,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<Webhook>> GetWebhooksAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Webhooks
            .AsNoTracking()
            .Where(hook => hook.GameProjectId == projectId)
            .OrderBy(hook => hook.Name)
            .ToListAsync(ct);
    }

    /// <summary>Alle eingeschalteten Webhooks aller Projekte — für den Zustelldienst.</summary>
    public async Task<List<Webhook>> GetActiveAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Webhooks.AsNoTracking().Where(hook => hook.IsEnabled).ToListAsync(ct);
    }

    public async Task SaveWebhookAsync(Webhook hook, CancellationToken ct = default)
    {
        await guard.EnsureCanWriteAsync(ct);

        if (string.IsNullOrWhiteSpace(hook.Name))
        {
            throw new ContentValidationException(messages["WebhookNameRequired"]);
        }

        // Nur http(s): Ein anderes Schema wäre kein Aufruf, sondern ein Weg, den Server dazu
        // zu bringen, etwas anderes zu tun — dieselbe Zurückhaltung wie bei den Links im
        // Story-Markdown.
        if (!Uri.TryCreate(hook.Url, UriKind.Absolute, out var url)
            || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            throw new ContentValidationException(messages["WebhookUrlInvalid"]);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var stored = await db.Webhooks.FirstOrDefaultAsync(h => h.Id == hook.Id, ct);

        if (stored is null)
        {
            stored = new Webhook
            {
                Id = hook.Id,
                GameProjectId = hook.GameProjectId,
                Name = hook.Name.Trim(),
                Url = hook.Url.Trim()
            };

            db.Webhooks.Add(stored);
        }

        stored.Name = hook.Name.Trim();
        stored.Url = hook.Url.Trim();
        stored.Secret = string.IsNullOrWhiteSpace(hook.Secret) ? null : hook.Secret.Trim();
        stored.ModuleKeys = string.IsNullOrWhiteSpace(hook.ModuleKeys) ? null : hook.ModuleKeys.Trim();
        stored.IsEnabled = hook.IsEnabled;

        await db.SaveChangesAsync(ct);

        // Der Zustelldienst fragt seinen Bestand nur in Abständen ab; ohne das hier bliebe der
        // erste Webhook eines Projekts bis dahin ungehört.
        queue.HasSubscribers = true;
    }

    public async Task DeleteWebhookAsync(Guid webhookId, CancellationToken ct = default)
    {
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        await db.Webhooks.Where(hook => hook.Id == webhookId).ExecuteDeleteAsync(ct);
    }

    /// <summary>Schreibt das Ergebnis eines Zustellversuchs fort — aufgerufen vom Dienst.</summary>
    public async Task RecordDeliveryAsync(
        Guid webhookId, int? statusCode, string? error, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var hook = await db.Webhooks.FirstOrDefaultAsync(h => h.Id == webhookId, ct);

        if (hook is null)
        {
            return;
        }

        hook.LastDeliveryAtUtc = DateTime.UtcNow;
        hook.LastStatusCode = statusCode;
        hook.LastError = error is null ? null : Shorten(error);

        await db.SaveChangesAsync(ct);
    }

    private static string Shorten(string text) =>
        text.Length <= 500 ? text : text[..500];

    /// <summary>
    /// Hört dieser Webhook auf dieses Modul? Ohne Filter auf alle — die Vorgabe, mit der ein
    /// Build-Server anfängt.
    /// </summary>
    public static bool Listens(Webhook hook, string moduleKey)
    {
        if (string.IsNullOrWhiteSpace(hook.ModuleKeys))
        {
            return true;
        }

        return hook.ModuleKeys
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(key => string.Equals(key, moduleKey, StringComparison.OrdinalIgnoreCase));
    }
}
