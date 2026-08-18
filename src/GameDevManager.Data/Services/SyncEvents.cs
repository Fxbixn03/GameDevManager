using System.Collections.Concurrent;
using System.Threading.Channels;

namespace GameDevManager.Data.Services;

/// <summary>Die Eckdaten des Live-Sync-Protokolls — dokumentiert in knowledge/live-sync.md.</summary>
public static class SyncProtocol
{
    /// <summary>
    /// Die Protokollversion — analog zur <c>FormatVersion</c> des Exports: Bei jeder
    /// Format-Änderung der Ereignisse erhöhen. Der Client vergleicht sie im
    /// <c>hello</c>-Ereignis und trennt bei einer fremden Version mit klarer Meldung,
    /// statt Nachrichten zu raten.
    /// </summary>
    public const int Version = 1;
}

/// <summary>
/// Ein Änderungsereignis für verbundene Engine-Editoren: Modul, GUID, Name, was geschah.
/// <paramref name="Action"/> ist der Name der <see cref="Domain.Entities.ChangeAction"/> —
/// als Text, damit das Protokoll ohne die Enum-Werte des Tools lesbar bleibt.
/// </summary>
public sealed record SyncEvent(
    Guid GameProjectId,
    string ModuleKey,
    Guid EntityId,
    string EntityName,
    string Action,
    DateTime OccurredAtUtc);

/// <summary>
/// Verteilt Änderungsereignisse an verbundene Engine-Editoren — die Serverseite des
/// Unity-Live-Sync. Eingestellt wird im <see cref="ChangeLogInterceptor"/> (der sieht jede
/// Änderung ohnehin), gelesen über den SSE-Endpunkt <c>/api/v1/sync/events</c>.
/// <para>
/// <b>Reiner Arbeitsspeicher</b>, dieselbe Bauart wie die <see cref="WebhookQueue"/>: Ein
/// Ereignis sagt „es hat sich etwas geändert“ — geht es bei einem Neustart verloren, holt
/// der Voll-Abgleich beim Wiederverbinden alles nach. Je Verbindung ein eigener Kanal mit
/// Obergrenze (Ältestes fällt zuerst): Ein hängender Editor darf den Speicher nicht füllen —
/// und verliert er Ereignisse, ist die Antwort ohnehin der Voll-Abgleich.
/// </para>
/// <para>
/// <b>Ohne Abnehmer wird gar nicht erst veröffentlicht</b> (<see cref="HasSubscribers"/>) —
/// dasselbe Muster wie bei den Webhooks: Das Speichern, der häufigste Vorgang des Tools,
/// bleibt frei von Arbeit für niemanden.
/// </para>
/// </summary>
public sealed class SyncEventBroadcaster
{
    /// <summary>Obergrenze je Verbindung — wie die Warteschlange der Webhooks.</summary>
    private const int MaxPending = 1000;

    private readonly ConcurrentDictionary<Guid, Channel<SyncEvent>> _subscribers = new();

    public bool HasSubscribers => !_subscribers.IsEmpty;

    public (Guid Id, ChannelReader<SyncEvent> Reader) Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<SyncEvent>(new BoundedChannelOptions(MaxPending)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

        _subscribers[id] = channel;

        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    public void Publish(SyncEvent syncEvent)
    {
        foreach (var channel in _subscribers.Values)
        {
            channel.Writer.TryWrite(syncEvent);
        }
    }
}
