using System.Text.Json;
using System.Text.Json.Nodes;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Kopiert eine einzelne Entität samt allem, was an ihrer GUID hängt — „als Vorlage kopieren“.
/// <para>
/// Gearbeitet wird über dieselbe Strecke wie beim Duplizieren eines Projekts: serialisieren,
/// GUIDs tauschen (<see cref="GuidRemap"/>), zurücklesen. Damit kommen die Kind-Sammlungen
/// (Rezept-Zutaten, Händler-Posten, Dialogzeilen) ohne eine Zeile Modulwissen mit, und ein
/// neues Modul ist automatisch dabei. Ein von Hand gepflegter Kopierpfad je Modul wäre die
/// Stelle, an der ein neu hinzugekommenes Kind stillschweigend fehlte.
/// </para>
/// <para>
/// Verweise <b>nach außen</b> bleiben stehen: Die Kopie eines Rezepts stellt dieselben Items
/// her, die Kopie eines NPCs führt dieselbe Loot-Table. Nur was mitkopiert wird, bekommt eine
/// neue GUID. Sprites bleiben bewusst beim Original — sie sind fast immer entitätsspezifisch,
/// und ein zweiter Datensatz auf dieselbe Datei zeigen zu lassen verwirrt beim Löschen.
/// </para>
/// </summary>
internal static class EntityDuplication
{
    /// <summary>
    /// Hängt die Kopie an den Kontext an — gespeichert wird vom Aufrufer, damit Entität und
    /// Anhängsel in einem <c>SaveChanges</c> landen.
    /// </summary>
    internal static async Task<TEntity> CopyAsync<TEntity>(
        GameDevManagerDbContext db, TEntity original, string name, CancellationToken ct)
        where TEntity : ContentEntity
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var json = JsonSerializer.Serialize(original, ExportFormat.JsonOptions);
        GuidRemap.Collect(JsonNode.Parse(json), map);

        var copy = JsonSerializer.Deserialize<TEntity>(GuidRemap.Apply(json, map), ExportFormat.JsonOptions)
            ?? throw new InvalidOperationException($"Die Kopie von {typeof(TEntity).Name} ließ sich nicht lesen.");

        copy.Name = name;
        copy.CreatedAtUtc = DateTime.UtcNow;
        copy.UpdatedAtUtc = copy.CreatedAtUtc;

        db.Set<TEntity>().Add(copy);

        await CopyAttachmentsAsync(db, map, ct);

        return copy;
    }

    /// <summary>
    /// Die modulübergreifenden Anhängsel: Feldwerte, individuelle Felder und Bedingungssätze.
    /// Sie hängen ohne Fremdschlüssel an einer Besitzer-GUID — und zwar auch an denen der
    /// Teilobjekte (ein einzelner Händler-Posten trägt eigene Bedingungen). Deshalb wird über
    /// <b>alle</b> getauschten GUIDs gesucht, nicht nur über die der Entität selbst.
    /// </summary>
    private static async Task CopyAttachmentsAsync(
        GameDevManagerDbContext db, Dictionary<string, string> map, CancellationToken ct)
    {
        var owners = map.Keys.Select(Guid.Parse).ToList();

        var fields = await db.FieldDefinitions
            .AsNoTracking()
            .Include(f => f.Options)
            .Where(f => f.OwnerEntityId != null && owners.Contains(f.OwnerEntityId.Value))
            .ToListAsync(ct);

        var values = await db.FieldValues
            .AsNoTracking()
            .Where(v => owners.Contains(v.OwnerEntityId))
            .ToListAsync(ct);

        var conditions = await db.ConditionSets
            .AsNoTracking()
            .Include(s => s.Conditions)
            .Where(s => owners.Contains(s.OwnerId))
            .ToListAsync(ct);

        if (fields.Count == 0 && values.Count == 0 && conditions.Count == 0)
        {
            return;
        }

        // Alles in einem Text: Ein Feldwert, der auf ein individuelles Feld zeigt, muss der
        // Kopie dieses Feldes folgen — getrennt getauscht zeigte er weiter auf das Original.
        var payload = JsonSerializer.Serialize(
            new Attachments { Fields = fields, Values = values, Conditions = conditions },
            ExportFormat.JsonOptions);

        GuidRemap.Collect(JsonNode.Parse(payload), map);

        var copied = JsonSerializer.Deserialize<Attachments>(
            GuidRemap.Apply(payload, map), ExportFormat.JsonOptions)!;

        db.FieldDefinitions.AddRange(copied.Fields);
        db.FieldValues.AddRange(copied.Values);
        db.ConditionSets.AddRange(copied.Conditions);
    }

    /// <summary>Träger für die Runde durch JSON — dieselben Regeln wie beim Export.</summary>
    private sealed class Attachments
    {
        public List<FieldDefinition> Fields { get; set; } = [];

        public List<FieldValue> Values { get; set; } = [];

        public List<ConditionSet> Conditions { get; set; } = [];
    }
}
