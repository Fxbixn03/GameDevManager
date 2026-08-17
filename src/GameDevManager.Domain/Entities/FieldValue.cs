namespace GameDevManager.Domain.Entities;

/// <summary>
/// Der Wert eines benutzerdefinierten Feldes an einer konkreten Entität.
/// <para>
/// Je nach <see cref="FieldDefinition.Type"/> trägt genau eine der Wertspalten den Inhalt;
/// getrennte Spalten statt einer generischen Zeichenkette, damit sich nach Zahlen, Daten und
/// Referenzen auch sinnvoll filtern und sortieren lässt.
/// </para>
/// </summary>
public class FieldValue
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FieldDefinitionId { get; set; }

    public FieldDefinition? FieldDefinition { get; set; }

    /// <summary>
    /// GUID der Entität, zu der der Wert gehört. Ohne Fremdschlüssel, weil die Entität in
    /// jedem Modul liegen kann — <see cref="OwnerModuleKey"/> sagt, in welchem.
    /// </summary>
    public Guid OwnerEntityId { get; set; }

    /// <summary>Modul der besitzenden Entität — siehe <see cref="ModuleKeys"/>.</summary>
    public required string OwnerModuleKey { get; set; }

    /// <summary>Wert für <see cref="ContentFieldType.Text"/>, <see cref="ContentFieldType.MultilineText"/> und <see cref="ContentFieldType.Color"/>.</summary>
    public string? TextValue { get; set; }

    /// <summary>
    /// Wert für <see cref="ContentFieldType.Integer"/> und <see cref="ContentFieldType.Decimal"/>.
    /// Bewusst <c>double</c> und nicht <c>decimal</c>: SQLite kennt keinen Dezimaltyp und
    /// sortiert/vergleicht ihn nur eingeschränkt.
    /// </summary>
    public double? NumberValue { get; set; }

    /// <summary>Wert für <see cref="ContentFieldType.Boolean"/>.</summary>
    public bool? BooleanValue { get; set; }

    /// <summary>Wert für <see cref="ContentFieldType.Date"/>.</summary>
    public DateTime? DateValue { get; set; }

    /// <summary>Wert für <see cref="ContentFieldType.EntityReference"/>: die GUID der Zielentität.</summary>
    public Guid? ReferenceValue { get; set; }

    /// <summary>Wert für <see cref="ContentFieldType.Select"/>: die gewählte <see cref="FieldOption"/>.</summary>
    public Guid? OptionId { get; set; }

    /// <summary>
    /// Von welcher Entität dieser Wert stammt, wenn er nicht am Besitzer selbst steht, sondern
    /// über <see cref="ContentEntity.BasedOnId"/> von einem Vorbild geerbt ist.
    /// <para>
    /// <b>Nicht persistiert</b> — eine geerbte Zeile gibt es in der Datenbank nicht, sie
    /// entsteht beim Auflösen. Der Export schreibt sie trotzdem: Die Engine soll die
    /// Vererbungskette nicht selbst auflösen müssen, und die Herkunft daneben sagt, warum ein
    /// Wert dort steht, an dem die Maske nichts zeigt. Der Import überspringt sie umgekehrt —
    /// sonst wäre die Vererbung nach einem Umzug materialisiert und damit aufgelöst.
    /// </para>
    /// </summary>
    public Guid? InheritedFromEntityId { get; set; }

    /// <summary>Der Wert steht nicht am Besitzer, sondern kommt von seinem Vorbild.</summary>
    public bool IsInherited => InheritedFromEntityId is not null;

    /// <summary>Es ist nichts hinterlegt — solche Werte werden beim Speichern nicht angelegt.</summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(TextValue)
        && NumberValue is null
        && BooleanValue is null
        && DateValue is null
        && ReferenceValue is null
        && OptionId is null;

    /// <summary>Leert alle Wertspalten, ohne die Zuordnung zu Feld und Entität zu verlieren.</summary>
    public void Clear()
    {
        TextValue = null;
        NumberValue = null;
        BooleanValue = null;
        DateValue = null;
        ReferenceValue = null;
        OptionId = null;
    }
}
