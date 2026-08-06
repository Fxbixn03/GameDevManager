using GameDevManager.Domain.Entities;

namespace GameDevManager.Data.Services;

/// <summary>
/// Fachlicher Fehler, dessen Meldung direkt in der Oberfläche angezeigt werden darf —
/// z. B. wenn eine Art gelöscht werden soll, die noch verwendet wird.
/// </summary>
public class ContentValidationException(string message) : Exception(message);

/// <summary>Kurzfassung einer Entität für Listen, Auswahlfelder und die Referenzansicht.</summary>
public sealed record EntitySummary(Guid Id, string ModuleKey, string Name, string? TypeName);

/// <summary>
/// Eine Fundstelle der Referenzansicht: eine Entität, die über ein Feld auf die gesuchte
/// GUID verweist.
/// </summary>
public sealed record EntityReferenceHit(
    Guid SourceEntityId,
    string SourceModuleKey,
    string SourceName,
    string FieldName);

/// <summary>Eine Zeile der Item-Übersicht — bewusst nur die Spalten, die die Liste zeigt.</summary>
public sealed record ItemListRow(
    Guid Id,
    string Name,
    string? Description,
    Guid? ContentTypeId,
    string? TypeName,
    DateTime UpdatedAtUtc);

/// <summary>
/// Alles, was die Bearbeitungsmaske eines Items braucht, in einem Zug geladen: das Item selbst,
/// die verfügbaren Arten samt Feldern, die individuellen Felder dieses Items und die bereits
/// erfassten Werte.
/// <para>
/// Weil sämtliche Arten mitgeladen werden, kommt der Wechsel der Art in der Maske ohne
/// erneuten Datenbankzugriff aus.
/// </para>
/// </summary>
public sealed class ItemEditContext
{
    public required Item Item { get; init; }

    /// <summary>Das Item wurde noch nie gespeichert.</summary>
    public required bool IsNew { get; init; }

    public required IReadOnlyList<ContentType> AvailableTypes { get; init; }

    /// <summary>Nur für dieses Item definierte Felder (exotische Items mit einzigartigen Werten).</summary>
    public required List<FieldDefinition> IndividualFields { get; init; }

    /// <summary>Werte je Felddefinition; fehlende Einträge legt <see cref="ValueFor"/> bei Bedarf an.</summary>
    public required Dictionary<Guid, FieldValue> Values { get; init; }

    /// <summary>Die aktuell gewählte Art, oder <c>null</c> wenn das Item keiner Art zugeordnet ist.</summary>
    public ContentType? SelectedType =>
        AvailableTypes.FirstOrDefault(t => t.Id == Item.ContentTypeId);

    /// <summary>Die Felder der gewählten Art, in ihrer Sortierreihenfolge.</summary>
    public IReadOnlyList<FieldDefinition> TypeFields =>
        SelectedType?.Fields ?? [];

    /// <summary>
    /// Liefert den Wert zu einem Feld und legt ihn beim ersten Zugriff an, damit die Maske
    /// direkt an das Objekt binden kann.
    /// </summary>
    public FieldValue ValueFor(FieldDefinition definition)
    {
        if (Values.TryGetValue(definition.Id, out var existing))
        {
            return existing;
        }

        var created = new FieldValue
        {
            FieldDefinitionId = definition.Id,
            OwnerEntityId = Item.Id,
            OwnerModuleKey = Item.ModuleKey
        };

        Values[definition.Id] = created;
        return created;
    }
}
