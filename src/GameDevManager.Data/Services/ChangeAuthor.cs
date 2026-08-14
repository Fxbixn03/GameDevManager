namespace GameDevManager.Data.Services;

/// <summary>Wer gerade handelt — die Momentaufnahme, die an jedem Protokolleintrag landet.</summary>
/// <param name="UserId"><c>null</c>, wenn ohne Anmeldung gearbeitet wird.</param>
public sealed record ChangeAuthor(Guid? UserId, string UserName);

/// <summary>
/// Woher das Änderungsprotokoll erfährt, wer gerade handelt.
/// <para>
/// Eine Schnittstelle und keine feste Klasse, weil die Antwort in der Web-Schicht liegt (dem
/// angemeldeten Benutzer der Verbindung), die Datenschicht sie aber beim Speichern braucht und
/// nicht auf Blazor verweisen darf. Die Tests setzen eine eigene Fassung ein.
/// </para>
/// </summary>
public interface IChangeAuthorProvider
{
    ValueTask<ChangeAuthor> GetCurrentAsync(CancellationToken ct = default);
}

/// <summary>
/// Die Vorgabe für alles, was ohne Anmeldung läuft — Wartungsaufgaben, Tests, der erste Start
/// vor der Ersteinrichtung. Der Eintrag entsteht trotzdem; es fehlt nur der Name dahinter.
/// </summary>
public sealed class SystemChangeAuthorProvider(string name = "System") : IChangeAuthorProvider
{
    private readonly ChangeAuthor _author = new(null, name);

    public ValueTask<ChangeAuthor> GetCurrentAsync(CancellationToken ct = default) =>
        ValueTask.FromResult(_author);
}
