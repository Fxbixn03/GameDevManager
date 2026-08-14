namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein übersetzter Text: <b>welcher</b> Text an <b>welcher</b> Entität in <b>welcher</b> Sprache.
/// <para>
/// Adressiert wird wie bei Feldwerten, Bedingungen und Assets über die GUID des Besitzers plus
/// sein Modul — ohne Fremdschlüssel, weil die Entität in jedem Modul liegen kann. Beim Löschen
/// einer Entität räumt <c>EntityCleanup</c> mit auf.
/// </para>
/// </summary>
public class ContentTranslation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    public Guid OwnerEntityId { get; set; }

    /// <summary>Modul der besitzenden Entität — siehe <see cref="ModuleKeys"/>.</summary>
    public required string OwnerModuleKey { get; set; }

    /// <summary>
    /// Welcher Text gemeint ist: <see cref="TranslationSlots.Name"/>,
    /// <see cref="TranslationSlots.Description"/> oder die GUID einer <see cref="FieldDefinition"/>.
    /// <para>
    /// Eine Textspalte und keine zwei Spalten („Art“ plus „Feld-GUID“): Der Slot ist ein
    /// Schlüssel, kein Verweis — dieselbe Überlegung wie beim <c>Slot</c> der Bedingungssätze.
    /// </para>
    /// </summary>
    public required string Slot { get; set; }

    /// <summary>Das Kürzel der Sprache — siehe <see cref="ContentLanguage.Code"/>.</summary>
    public required string LanguageCode { get; set; }

    /// <summary>Der übersetzte Text. Leer heißt „noch nicht übersetzt“ und wird nicht gespeichert.</summary>
    public required string Text { get; set; }

    /// <summary>
    /// Der Ausgangstext, wie er beim Übersetzen aussah. Er beantwortet die Frage, die eine
    /// reine Übersetzungstabelle nicht beantworten kann: Ist die Übersetzung noch aktuell?
    /// Ändert sich das Original, steht die Übersetzung als <b>veraltet</b> da, statt still
    /// falsch zu bleiben.
    /// </summary>
    public string? SourceText { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Die festen Slot-Schlüssel. Alles andere ist die GUID einer Felddefinition — so kommen neue
/// Textfelder ohne Änderung an dieser Stelle mit.
/// </summary>
public static class TranslationSlots
{
    public const string Name = "name";

    public const string Description = "description";

    /// <summary>Ob der Slot ein benutzerdefiniertes Feld meint statt der Stammdaten.</summary>
    public static bool IsField(string slot) => Guid.TryParse(slot, out _);

    public static string ForField(Guid fieldDefinitionId) => fieldDefinitionId.ToString();
}
