namespace GameDevManager.Data.Services;

/// <summary>Ein Stand im Sicherungsziel — Name, Größe, Zeitpunkt der Ablage.</summary>
public sealed record RemoteSnapshot(string FileName, long SizeBytes, DateTime LastModifiedUtc);

/// <summary>
/// Das optionale Sicherungsziel der Exportstände — S3-kompatibler Speicher (MinIO,
/// Backblaze, AWS). Die Schnittstelle sitzt in der Datenschicht, die Umsetzung in Web, wie
/// beim Mailversand und den Übersetzungsvorschlägen.
/// <para>
/// <b>Die Spiegelung ist Beiwerk, nie Blocker</b>: <see cref="UploadAsync"/> wirft nicht —
/// ein nicht erreichbares Sicherungsziel landet im Log der Umsetzung, und der Stand liegt
/// trotzdem lokal. Ein Netz, dessen zweiter Knoten reißt, ist immer noch ein Netz.
/// </para>
/// <para>
/// <b>Das Aufräumen löscht nur lokal.</b> Die Spiegelkopie bleibt stehen — das
/// Sicherungsziel ist das Langzeitgedächtnis, und genau die weggeräumten Stände sind dort
/// noch zu haben. Sie erscheinen in der Export-Historie als eigener Abschnitt; der Weg
/// zurück führt über den Bucket.
/// </para>
/// </summary>
public interface ISnapshotMirror
{
    /// <summary>Ohne Konfiguration bleibt alles still, und die Historie zeigt keinen Abschnitt.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Lädt einen frisch angelegten Stand hoch. Der Schlüssel folgt dem Schema
    /// <c>&lt;prefix&gt;/&lt;projekt-guid&gt;/&lt;dateiname&gt;</c> — je Projekt ein
    /// Ordner, der Zeitstempel steckt im Dateinamen. Wirft nie.
    /// </summary>
    Task UploadAsync(Guid projectId, string fileName, Stream content, CancellationToken ct = default);

    /// <summary>
    /// Die Stände eines Projekts im Sicherungsziel — für den Abschnitt „nur noch entfernt“
    /// der Historie. Ein nicht erreichbares Ziel liefert eine leere Liste statt zu werfen.
    /// </summary>
    Task<IReadOnlyList<RemoteSnapshot>> ListAsync(Guid projectId, CancellationToken ct = default);
}

/// <summary>Die Vorgabe ohne Sicherungsziel — dieselbe Bauart wie der <see cref="NullMailSender"/>.</summary>
public sealed class NullSnapshotMirror : ISnapshotMirror
{
    public bool IsConfigured => false;

    public Task UploadAsync(Guid projectId, string fileName, Stream content, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<RemoteSnapshot>> ListAsync(Guid projectId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RemoteSnapshot>>([]);
}
