namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine benannte Listenansicht: ein Filter plus die Spalten, die man sehen will — „alle Waffen
/// ohne Sprite, Schaden über 50, Status Entwurf“.
/// <para>
/// Filter und Spalten stehen als <b>JSON-Text</b> und nicht als Spaltensatz: Als Text gehen sie
/// ohne Zutun durch Export und Duplizieren, und eine neue Filterart braucht keine Migration —
/// dieselbe Überlegung wie beim Feldtyp „Formel/Kurve“.
/// </para>
/// <para>
/// Je Projekt <b>und je Benutzer</b>: Eine gespeicherte Suche ist eine Arbeitsgewohnheit, keine
/// Aussage über den Spielinhalt. Werkzeug-Daten wie die Favoriten — nicht im Export, sie
/// überstehen den ersetzenden Import, und ein gelöschtes Konto nimmt seine Ansichten mit.
/// </para>
/// </summary>
public class SavedView
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    /// <summary>
    /// Der Besitzer. Anders als sonst ein echter Fremdschlüssel — wie bei
    /// <see cref="UserPin"/>, und aus demselben Grund: Ohne sein Konto bedeutet er nichts.
    /// </summary>
    public Guid AppUserId { get; set; }

    public AppUser? User { get; set; }

    /// <summary>Das Modul, dessen Bestand die Ansicht zeigt — siehe <see cref="ModuleKeys"/>.</summary>
    public required string ModuleKey { get; set; }

    public required string Name { get; set; }

    /// <summary>Der Filter als JSON — siehe <c>ContentFilter</c> in der Datenschicht.</summary>
    public required string FilterJson { get; set; }

    /// <summary>
    /// Die gewählten Spalten als semikolongetrennte Liste von Feld-GUIDs, in ihrer
    /// Reihenfolge. Leer heißt „alle Felder der gewählten Art“ — die Vorgabe, mit der man
    /// anfängt. Text und keine Zuordnungstabelle, wie die Modul-Freigaben eines Benutzers.
    /// </summary>
    public string? ColumnFieldIds { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
