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
    DateTime UpdatedAtUtc,
    Guid? PrimaryAssetId);

/// <summary>
/// Ein Asset in der Bibliothek samt dem Namen der Entität, zu der es gehört. Der Name steht
/// nicht am Asset, weil die Entität in jedem Modul liegen kann und über ihre GUID hängt.
/// </summary>
public sealed record AssetLibraryEntry(Asset Asset, string? OwnerName);

/// <summary>
/// Alles, was die Bearbeitungsmaske einer Modul-Entität braucht, in einem Zug geladen: die
/// Entität selbst, die verfügbaren Arten samt Feldern, ihre individuellen Felder und die
/// bereits erfassten Werte.
/// <para>
/// Weil sämtliche Arten mitgeladen werden, kommt der Wechsel der Art in der Maske ohne
/// erneuten Datenbankzugriff aus.
/// </para>
/// <para>
/// Modulübergreifend, damit jedes Modul dieselbe Maske bekommt — die Felder funktionieren
/// laut Konzept überall gleich.
/// </para>
/// </summary>
public sealed class ContentEditContext<TEntity>
    where TEntity : ContentEntity
{
    public required TEntity Entity { get; init; }

    /// <summary>Die Entität wurde noch nie gespeichert.</summary>
    public required bool IsNew { get; init; }

    public required IReadOnlyList<ContentType> AvailableTypes { get; init; }

    /// <summary>Nur für diese eine Entität definierte Felder (etwa exotische Items).</summary>
    public required List<FieldDefinition> IndividualFields { get; init; }

    /// <summary>Werte je Felddefinition; fehlende Einträge legt <see cref="ValueFor"/> bei Bedarf an.</summary>
    public required Dictionary<Guid, FieldValue> Values { get; init; }

    /// <summary>Die aktuell gewählte Art, oder <c>null</c> wenn die Entität keiner zugeordnet ist.</summary>
    public ContentType? SelectedType =>
        AvailableTypes.FirstOrDefault(t => t.Id == Entity.ContentTypeId);

    /// <summary>Die Felder der gewählten Art, in ihrer Sortierreihenfolge.</summary>
    public IReadOnlyList<FieldDefinition> TypeFields =>
        SelectedType?.Fields ?? [];

    /// <summary>Alle gerade geltenden Felder — Art-Felder und individuelle zusammen.</summary>
    public IEnumerable<FieldDefinition> ApplicableFields =>
        TypeFields.Concat(IndividualFields);

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
            OwnerEntityId = Entity.Id,
            OwnerModuleKey = Entity.ModuleKey
        };

        Values[definition.Id] = created;
        return created;
    }
}

/// <summary>Eine Zeile der Dialog-Übersicht.</summary>
public sealed record DialogueListRow(
    Guid Id,
    string Name,
    string? Description,
    DialogueKind Kind,
    bool IncludesPlayer,
    Guid? ContentTypeId,
    string? TypeName,
    int ParticipantCount,
    int LineCount,
    DateTime UpdatedAtUtc);

/// <summary>Eine Zeile der Karten-Übersicht.</summary>
public sealed record MapListRow(
    Guid Id,
    string Name,
    string? Description,
    Guid? ContentTypeId,
    string? TypeName,
    int MarkerCount,
    int MapLinkCount,
    DateTime UpdatedAtUtc,
    Guid? ImageAssetId);

/// <summary>Wo eine Entität auf einer Karte markiert ist — für NPCs sind das ihre Spawn-Orte.</summary>
public sealed record MapPlacement(
    Guid MapId,
    string MapName,
    Guid MarkerId,
    string? Label,
    double X,
    double Y,
    double? Radius);

/// <summary>Eine Zeile der Loot-Table-Übersicht.</summary>
/// <param name="TotalChance">Summe aller Wahrscheinlichkeiten — trägt den Health Check.</param>
/// <param name="UsedByNpcCount">Wie viele NPCs diese Tabelle verwenden.</param>
public sealed record LootTableListRow(
    Guid Id,
    string Name,
    string? Description,
    LootRollMode RollMode,
    Guid? ContentTypeId,
    string? TypeName,
    int EntryCount,
    double TotalChance,
    int UsedByNpcCount,
    DateTime UpdatedAtUtc);

/// <summary>Eine Loot-Table, in der ein bestimmtes Item vorkommt — für die Item-Maske.</summary>
public sealed record LootSourceForItem(
    Guid LootTableId,
    string LootTableName,
    double Chance,
    int MinQuantity,
    int MaxQuantity);

/// <summary>Eine Zeile der NPC-Übersicht.</summary>
public sealed record NpcListRow(
    Guid Id,
    string Name,
    string? Description,
    NpcKind Kind,
    bool IsTrader,
    bool IsQuestGiver,
    bool HasLootTable,
    Guid? ContentTypeId,
    string? TypeName,
    int OfferCount,
    DateTime UpdatedAtUtc,
    Guid? PrimaryAssetId);

/// <summary>Ein Händler, der ein bestimmtes Item führt — für die Item-Maske.</summary>
public sealed record TraderForItem(
    Guid NpcId,
    string NpcName,
    double? SellPrice,
    double? BuyPrice,
    string? CurrencyLabel);

/// <summary>Eine Zeile der Fraktions-Übersicht.</summary>
public sealed record FactionListRow(
    Guid Id,
    string Name,
    string? Description,
    Guid? ContentTypeId,
    string? TypeName,
    int MemberCount,
    DateTime UpdatedAtUtc,
    Guid? PrimaryAssetId);

/// <summary>Eine Fraktion, in der ein bestimmter NPC Mitglied ist — für die NPC-Maske.</summary>
public sealed record FactionForNpc(Guid FactionId, string FactionName, string? Role);

/// <summary>Eine Zeile der Währungs-Übersicht.</summary>
public sealed record CurrencyListRow(
    Guid Id,
    string Name,
    string? Symbol,
    string? Description,
    Guid? ContentTypeId,
    string? TypeName,
    DateTime UpdatedAtUtc,
    Guid? PrimaryAssetId);

/// <summary>Eine Zeile der Rezept-Übersicht.</summary>
public sealed record RecipeListRow(
    Guid Id,
    string Name,
    Guid? ContentTypeId,
    string? TypeName,
    Guid? OutputItemId,
    string? OutputItemName,
    int OutputQuantity,
    Guid? OutputAssetId,
    int IngredientCount,
    DateTime UpdatedAtUtc);

/// <summary>
/// Ein Knoten des Crafting-Baums: ein Item in einer bestimmten Menge, darunter die Zutaten
/// des Rezepts, das es herstellt.
/// </summary>
/// <param name="Quantity">Wie viele Stück an dieser Stelle gebraucht werden.</param>
/// <param name="RecipeId">Das Rezept, das dieses Item herstellt — <c>null</c> bei Grundstoffen.</param>
/// <param name="AlternativeRecipeCount">
/// Weitere Rezepte, die dasselbe Item herstellen. Aufgeklappt wird nur das erste; der Rest
/// wird angezeigt, damit nicht der Eindruck entsteht, es gäbe nur einen Weg.
/// </param>
/// <param name="IsCycle">
/// Dieses Item kommt im Pfad bereits vor. Der Baum bricht hier ab — zyklische Rezepte sind
/// laut Konzept ein Health-Check-Fall und keine Endlosschleife.
/// </param>
/// <summary>Ein Grundstoff samt Menge, die für eine Herstellung zusammenkommt.</summary>
public sealed record CraftingRequirement(Guid ItemId, string Name, int Quantity, Guid? PrimaryAssetId);

public sealed record CraftingTreeNode(
    Guid ItemId,
    string ItemName,
    Guid? PrimaryAssetId,
    int Quantity,
    Guid? RecipeId,
    string? RecipeName,
    int RecipeOutputQuantity,
    int AlternativeRecipeCount,
    bool IsCycle,
    IReadOnlyList<CraftingTreeNode> Children);
