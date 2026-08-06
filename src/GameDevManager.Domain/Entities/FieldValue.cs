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
