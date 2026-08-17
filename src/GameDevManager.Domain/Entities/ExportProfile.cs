namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein benannter Export: „Unity, nur Fertiges, ohne Werkzeug-Module“. Damit wird aus fünf
/// Schaltern, die man jedes Mal gleich setzt, ein Klick.
/// <para>
/// Kein Spielinhalt, sondern eine Vorschrift für den Export — deshalb keine
/// <see cref="ContentEntity"/>: keine Arten, keine Felder, kein Sprite, kein Eintrag in Suche,
/// Referenzansicht und Duplizieren. Dieselbe Überlegung wie beim <see cref="EnginePreset"/>,
/// und wie dort steht das Profil trotzdem im Export — es gehört zum Projekt.
/// </para>
/// </summary>
public class ExportProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// Das Ziel als Text und nicht als Enum-Spalte: <c>ExportTarget</c> liegt in der
    /// Datenschicht, die Domäne kennt es nicht — und als Text übersteht der Wert ein
    /// hinzugekommenes Ziel, ohne dass eine Zahl plötzlich etwas anderes bedeutet.
    /// </summary>
    public string Target { get; set; } = "Json";

    public bool IncludeAssets { get; set; } = true;

    /// <summary>
    /// Wie die Inhaltsdateien im Archiv liegen — <c>SingleFile</c> (eine Datei je Modul) oder
    /// <c>PerEntity</c> (eine Datei je Entität, für Git). Als Text und aus demselben Grund wie
    /// beim <see cref="Target"/>: Das Enum liegt in der Datenschicht.
    /// </summary>
    public string Layout { get; set; } = "SingleFile";

    /// <summary>
    /// Mindest-Bearbeitungsstand; <c>null</c> heißt „alles“. Ein Mindeststand und kein
    /// einzelner — siehe <see cref="ContentStatus"/>.
    /// </summary>
    public ContentStatus? MinimumStatus { get; set; }

    /// <summary>
    /// Die Module, die mitgehen — kommagetrennt, <c>null</c> heißt „alle“. Eine Textspalte wie
    /// bei den Modul-Freigaben eines Benutzers und aus demselben Grund: Eine Zuordnungstabelle
    /// verlangte vier Migrationen mehr für eine Liste kurzer Schlüssel.
    /// </summary>
    public string? ModuleKeys { get; set; }

    public int SortOrder { get; set; }
}
