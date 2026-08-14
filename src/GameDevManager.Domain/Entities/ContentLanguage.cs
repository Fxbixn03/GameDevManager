namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine Sprache, in der die Spielinhalte eines Projekts vorliegen.
/// <para>
/// Die <b>Ausgangssprache</b> (<see cref="IsSource"/>) ist keine Übersetzung: Ihre Texte stehen
/// dort, wo sie immer standen — im Namen der Entität, ihrer Beschreibung und ihren Feldwerten.
/// Alle anderen Sprachen hängen als <see cref="ContentTranslation"/> daneben. Sonst müsste
/// jedes Modul seine Stammdaten durch eine Übersetzungstabelle ersetzen, und ein Projekt ohne
/// zweite Sprache zahlte für etwas, das es nicht braucht.
/// </para>
/// </summary>
public class ContentLanguage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    /// <summary>
    /// Das Sprachkürzel, unter dem die Engine die Texte sucht — „de“, „en“, „pt-BR“.
    /// Es ist der Schlüssel der Übersetzungen und je Projekt eindeutig.
    /// </summary>
    public required string Code { get; set; }

    /// <summary>Der Anzeigename, z. B. „Englisch“.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// Ob dies die Ausgangssprache ist — die, in der die Inhalte erfasst werden. Genau eine
    /// Sprache je Projekt trägt den Schalter; sie hat keine Übersetzungszeilen.
    /// </summary>
    public bool IsSource { get; set; }

    /// <summary>Reihenfolge in Listen und Fortschrittsanzeige.</summary>
    public int SortOrder { get; set; }
}
