namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine Auswahlmöglichkeit eines Feldes vom Typ <see cref="FieldType.Select"/>,
/// z. B. „Gewöhnlich", „Selten", „Episch" für ein Seltenheits-Feld.
/// </summary>
public class FieldOption
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FieldDefinitionId { get; set; }

    public FieldDefinition? FieldDefinition { get; set; }

    public required string Label { get; set; }

    public int SortOrder { get; set; }

    public FieldOption Clone() => new()
    {
        Id = Id,
        FieldDefinitionId = FieldDefinitionId,
        Label = Label,
        SortOrder = SortOrder
    };
}
