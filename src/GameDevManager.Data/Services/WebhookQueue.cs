using System.Collections.Concurrent;
using GameDevManager.Domain.Entities;

namespace GameDevManager.Data.Services;

/// <summary>Ein zuzustellendes Ereignis: was sich in welchem Modul geändert hat.</summary>
public sealed record WebhookEvent(
    Guid GameProjectId,
    string ModuleKey,
    Guid EntityId,
    string EntityName,
    ChangeAction Action,
    string UserName,
    DateTime OccurredAtUtc);

/// <summary>
/// Die Warteschlange zwischen dem <see cref="ChangeLogInterceptor"/> und dem Hintergrunddienst,
/// der die Webhooks aufruft.
/// <para>
/// <b>Reiner Arbeitsspeicher, keine Tabelle</b> — dieselbe Bauart wie <see cref="EditingPresence"/>
/// und der <c>WhiteboardNotifier</c>. Ein Webhook meldet „es hat sich etwas geändert“; geht die
/// Nachricht bei einem Neustart verloren, meldet die nächste Änderung es erneut. Eine
/// Auslieferungstabelle wäre eine Warteschlange in der Datenbank für eine Nachricht, die in
/// Sekunden veraltet — und sie brächte vier Migrationen mit.
/// </para>
/// <para>
/// Die Schlange hat eine <b>Obergrenze</b>: Steht der Empfänger still, während jemand einen
/// Import fährt, wüchse sie sonst unbegrenzt. Ältestes fällt zuerst — die jüngere Nachricht
/// beschreibt den aktuelleren Stand.
/// </para>
/// </summary>
public class WebhookQueue
{
    /// <summary>So viele Ereignisse warten höchstens. Darüber fällt das älteste heraus.</summary>
    private const int Capacity = 1000;

    private readonly ConcurrentQueue<WebhookEvent> _pending = new();

    /// <summary>
    /// Wird vom Hintergrunddienst gesetzt und beim Einstellen gelesen: Ohne eingeschalteten
    /// Webhook im Bestand wird gar nicht erst eingereiht. Das hält den häufigsten Vorgang des
    /// Tools — Speichern — frei von einer Schlange, die niemand leert.
    /// </summary>
    public bool HasSubscribers { get; set; }

    public void Enqueue(WebhookEvent entry)
    {
        if (!HasSubscribers)
        {
            return;
        }

        _pending.Enqueue(entry);

        while (_pending.Count > Capacity && _pending.TryDequeue(out _))
        {
            // Ältestes zuerst: Die jüngere Nachricht beschreibt den aktuelleren Stand.
        }
    }

    /// <summary>Nimmt alles Wartende heraus. Der Dienst stellt es danach in einem Rutsch zu.</summary>
    public List<WebhookEvent> DrainAll()
    {
        var drained = new List<WebhookEvent>();

        while (_pending.TryDequeue(out var entry))
        {
            drained.Add(entry);
        }

        return drained;
    }
}
