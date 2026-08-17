using System.Collections.Concurrent;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Der schreibende Teil der HTTP-API (F36) — der Weg, auf dem ein Skript zweihundert Items aus
/// einer Tabelle einspielt, ohne den Umweg über CSV und Browser.
/// <para>
/// Der Schreibpfad führt durch <see cref="IModuleEntitySource.SaveAsync"/> und damit durch
/// dieselbe Strecke wie die Maske: Pflichtfelder, Wertegrenzen, Variantenprüfung,
/// Schreibkonflikt-Erkennung, <c>WriteGuardInterceptor</c> und <c>ChangeLogInterceptor</c>
/// greifen von selbst. Genau das war die Anforderungsliste, unter der die API bisher nur
/// lesen durfte.
/// </para>
/// <para>
/// <b>Gelöscht wird hier nicht.</b> Löschen räumt Assets, Kind-Sammlungen, Bedingungen und den
/// Papierkorb ab — das steckt in den Modul-Diensten, je Modul anders, und ein generischer
/// Löschpfad wäre genau die Stelle, an der ein Modul etwas liegen ließe. Wer löschen will,
/// tut es in der Oberfläche.
/// </para>
/// </summary>
public class ContentApiWriteService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>
    /// Wie lange ein Idempotenz-Schlüssel nachwirkt. Er fängt den wiederholten Aufruf nach
    /// einem Verbindungsabbruch ab — dafür sind Minuten reichlich, und länger zu merken hieße,
    /// eine Warteschlange für etwas zu führen, das binnen Sekunden entschieden ist.
    /// </summary>
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Die schon beantworteten Idempotenz-Schlüssel. Reiner Arbeitsspeicher und keine Tabelle
    /// — dieselbe Bauart wie die Webhook-Warteschlange: Das Tool läuft in einem Prozess, und
    /// nach einem Neustart ist der abgebrochene Aufruf ohnehin Geschichte.
    /// </summary>
    private static readonly ConcurrentDictionary<string, (DateTime At, ContentWriteResult Result)> Answered = new();

    /// <summary>
    /// Legt eine Entität an oder ändert sie. <paramref name="idempotencyKey"/> darf leer sein;
    /// mit ihm liefert ein wiederholter Aufruf dieselbe Antwort, statt ein zweites Mal anzulegen.
    /// </summary>
    public async Task<ContentWriteResult> WriteAsync(
        Guid projectId, string moduleKey, ContentWrite write,
        string? idempotencyKey = null, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey) && TryRemember(idempotencyKey, out var known))
        {
            return known;
        }

        var source = sources.FirstOrDefault(s => s.ModuleKey == moduleKey)
            ?? throw new ContentValidationException(messages["Api_ModuleUnknown", moduleKey]);

        await using var db = await factory.CreateDbContextAsync(ct);

        var project = await db.GameProjects.AnyAsync(p => p.Id == projectId, ct);

        if (!project)
        {
            throw new ContentValidationException(messages["Export_ProjectMissing"]);
        }

        var result = await source.SaveAsync(db, projectId, write, ct);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            Answered[idempotencyKey] = (DateTime.UtcNow, result);
        }

        return result;
    }

    /// <summary>
    /// Ob dieser Schlüssel schon beantwortet wurde. Räumt dabei ab, was aus dem Fenster
    /// gefallen ist — ein eigener Wartungslauf für eine Handvoll Einträge wäre einer zu viel.
    /// </summary>
    private static bool TryRemember(string key, out ContentWriteResult result)
    {
        var cutoff = DateTime.UtcNow - IdempotencyWindow;

        foreach (var stale in Answered.Where(entry => entry.Value.At < cutoff).Select(entry => entry.Key))
        {
            Answered.TryRemove(stale, out _);
        }

        if (Answered.TryGetValue(key, out var known))
        {
            result = known.Result;
            return true;
        }

        result = default!;
        return false;
    }
}
