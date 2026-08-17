namespace GameDevManager.Domain.Entities;

/// <summary>Was eine eigene Prüfung feststellt.</summary>
public enum ContentRuleCheck
{
    /// <summary>Ein bestimmtes Feld ist nicht ausgefüllt — die häufigste Regel überhaupt.</summary>
    FieldEmpty = 0,

    /// <summary>Die Entität hat kein primäres Sprite. „Jedes Item braucht ein Bild.“</summary>
    NoPrimarySprite = 1,

    /// <summary>Die Beschreibung ist leer.</summary>
    NoDescription = 2,

    /// <summary>Kein Tag vergeben — oder, mit <see cref="ContentRule.TagId"/>, dieses fehlt.</summary>
    NoTag = 3,

    /// <summary>
    /// In einem <see cref="ContentRule.Slot"/> hängt kein Bedingungssatz. „Jede Quest braucht
    /// eine Freischaltbedingung.“
    /// </summary>
    NoConditions = 4,

    /// <summary>Die Entität ist keiner Art zugeordnet.</summary>
    NoContentType = 5
}

/// <summary>Wie schwer ein Fund wiegt.</summary>
public enum ContentRuleSeverity
{
    /// <summary>Ein Hinweis — schön, wenn behoben.</summary>
    Info = 0,

    /// <summary>Eine Warnung — sollte vor dem Export behoben sein.</summary>
    Warning = 1
}

/// <summary>
/// Eine eigene Prüfung: „jedes Item braucht ein Sprite“, „kein NPC ohne Art“, „jede Quest
/// braucht eine Freischaltbedingung“.
/// <para>
/// Bewusst <b>keine freie Skriptsprache</b>: Eine Handvoll Regelarten deckt neunzig Prozent ab
/// und lässt sich in einer Maske erfassen — ein Ausdrucksrechner wäre ein zweites Werkzeug im
/// Werkzeug, und wer ihn nicht beherrscht, bekäme keine Regel zustande.
/// </para>
/// <para>
/// Ausgewertet wird über die <see cref="ModuleKeys"/>-Quelle des Moduls, also über denselben
/// Weg wie Suche, Referenzansicht und die gespeicherten Ansichten — ein neues Modul ist von
/// selbst dabei.
/// </para>
/// </summary>
public class ContentRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    /// <summary>Der Name, unter dem der Fund in der Liste steht.</summary>
    public required string Name { get; set; }

    /// <summary>Das Modul, dessen Bestand geprüft wird — siehe <see cref="ModuleKeys"/>.</summary>
    public required string ModuleKey { get; set; }

    /// <summary>
    /// Nur Entitäten dieser Art prüfen. <c>null</c> heißt „alle“. Unterarten zählen mit — wer
    /// „Waffe“ wählt, meint auch „Nahkampf“, dieselbe Regel wie in den gespeicherten Ansichten.
    /// <para>
    /// Ohne Fremdschlüssel: Eine gelöschte Art soll die Regel nicht mitreißen, sie prüft dann
    /// eben wieder alles — der Nutzer sieht es am leeren Auswahlfeld.
    /// </para>
    /// </summary>
    public Guid? ContentTypeId { get; set; }

    public ContentRuleCheck Check { get; set; }

    /// <summary>Das geprüfte Feld — nur bei <see cref="ContentRuleCheck.FieldEmpty"/>.</summary>
    public Guid? FieldDefinitionId { get; set; }

    /// <summary>Das verlangte Tag — nur bei <see cref="ContentRuleCheck.NoTag"/>; <c>null</c> heißt „irgendeines“.</summary>
    public Guid? TagId { get; set; }

    /// <summary>Der geprüfte Bedingungs-Slot — nur bei <see cref="ContentRuleCheck.NoConditions"/>.</summary>
    public string? Slot { get; set; }

    public ContentRuleSeverity Severity { get; set; } = ContentRuleSeverity.Warning;

    /// <summary>Abgeschaltete Regeln bleiben stehen, prüfen aber nicht — wie ein Modul-Schalter.</summary>
    public bool IsEnabled { get; set; } = true;

    public int SortOrder { get; set; }
}
