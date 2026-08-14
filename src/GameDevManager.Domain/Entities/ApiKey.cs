namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein Schlüssel für die lesende HTTP-API. Damit zieht ein Editor-Plugin in Unity oder Godot
/// den Stand direkt, statt über das Export-ZIP zu gehen.
/// <para>
/// Gespeichert wird nur der <b>Hash</b> — wie bei den Passwörtern und aus demselben Grund: Ein
/// Schlüssel, der im Klartext in der Datenbank steht, ist mit ihr zusammen weg. Der Klartext
/// wird beim Anlegen genau einmal gezeigt.
/// </para>
/// <para>
/// Schlüssel gehören der Installation, nicht einem Projekt — wie die Benutzer. Über
/// <see cref="GameProjectId"/> lässt sich einer aber auf ein Projekt einschränken: Ein
/// Plugin, das ein Spiel baut, braucht die anderen nicht zu sehen.
/// </para>
/// </summary>
public class ApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Wofür der Schlüssel da ist — „Unity-Plugin“, „Build-Server“.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// Die ersten Zeichen des Klartexts. Sie stehen im Klartext da und sind kein Geheimnis:
    /// Ohne sie ließe sich in einer Liste aus fünf Schlüsseln keiner wiedererkennen.
    /// </summary>
    public required string Prefix { get; set; }

    public required string KeyHash { get; set; }

    /// <summary>Auf dieses Projekt beschränkt; <c>null</c> heißt „alle Projekte“.</summary>
    public Guid? GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Wann der Schlüssel zuletzt getragen hat — die Antwort auf „wird der noch benutzt?“.</summary>
    public DateTime? LastUsedAtUtc { get; set; }

    /// <summary>Ab hier gilt er nicht mehr. <c>null</c> heißt „ohne Ablauf“.</summary>
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>Gesperrt statt gelöscht — so bleibt sichtbar, dass es ihn gab.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Ob er jetzt gerade gilt.</summary>
    public bool IsValidNow => !IsDisabled && (ExpiresAtUtc is null || ExpiresAtUtc > DateTime.UtcNow);
}
