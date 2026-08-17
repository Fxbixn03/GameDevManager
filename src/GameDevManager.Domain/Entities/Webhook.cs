namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein Ziel, das bei Änderungen im Projekt aufgerufen wird — damit ein Build-Server den Export
/// abholen kann, wenn sich etwas geändert hat.
/// <para>
/// Aufgerufen wird <b>nicht</b> im <c>SaveChanges</c>, sondern aus einem Hintergrunddienst mit
/// Warteschlange: Eine hängende HTTP-Anfrage darf keine Transaktion aufhalten. Der
/// <c>ChangeLogInterceptor</c> sieht ohnehin jede Änderung an genau einer Stelle und stellt
/// von dort ein.
/// </para>
/// </summary>
public class Webhook
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    public required string Name { get; set; }

    /// <summary>Die aufgerufene Adresse. Nur <c>http</c> und <c>https</c> — der Dienst prüft das.</summary>
    public required string Url { get; set; }

    /// <summary>
    /// Das gemeinsame Geheimnis für die Signatur. Es steht im Klartext da und nicht als Hash:
    /// Anders als ein Passwort muss das Tool damit <b>rechnen</b> — es signiert jede Nachricht
    /// über HMAC-SHA256, und dafür braucht es den Wert selbst. Wer die Datenbank lesen kann,
    /// kann ohnehin den ganzen Bestand lesen.
    /// </summary>
    public string? Secret { get; set; }

    /// <summary>
    /// Auf welche Module gehört wird — kommagetrennt, leer heißt „alle“. Eine Textspalte wie
    /// die Modul-Freigaben eines Benutzers und die Modulauswahl eines Export-Profils.
    /// </summary>
    public string? ModuleKeys { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>Wann zuletzt zugestellt wurde — die Antwort auf „läuft das noch?“.</summary>
    public DateTime? LastDeliveryAtUtc { get; set; }

    /// <summary>
    /// Der HTTP-Status des letzten Versuchs, oder <c>null</c>, wenn die Verbindung gar nicht
    /// zustande kam. Eine Zahl und kein Verlauf: Gefragt ist „kommt es an?“, und das beantwortet
    /// der letzte Versuch.
    /// </summary>
    public int? LastStatusCode { get; set; }

    /// <summary>Die Fehlermeldung des letzten Versuchs, sofern er scheiterte.</summary>
    public string? LastError { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
