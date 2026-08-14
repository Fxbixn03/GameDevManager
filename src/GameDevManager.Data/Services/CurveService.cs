using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Eine gefüllte Kurve im Projekt, wie die Vergleichsauswahl sie anbietet: wem sie gehört,
/// in welchem Feld sie steht und ihr gespeicherter Text. Ausgewertet wird sie erst in der
/// Oberfläche über <c>CurveDefinition.Parse</c> — die Datenschicht reicht den Wert durch, wie
/// sie es bei jedem anderen Feldwert auch tut.
/// </summary>
public sealed record CurveReference(
    Guid OwnerEntityId,
    string OwnerModuleKey,
    string OwnerName,
    Guid FieldDefinitionId,
    string FieldName,
    string Stored);

/// <summary>
/// Sammelt die Kurven eines Projekts modulübergreifend ein — die Grundlage dafür, zwei
/// Levelkurven übereinander zu zeichnen (Spieler gegen Gegner, Klasse A gegen B).
/// <para>
/// Wie beim <see cref="SearchService"/> läuft der Weg über die <see cref="IModuleEntitySource"/>
/// und nicht über einen <c>switch</c> je Modul: Ein Feld vom Typ „Formel/Kurve“ kann an jeder
/// Art in jedem Modul hängen, und ein neues Modul soll von selbst mitkommen.
/// </para>
/// </summary>
public class CurveService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources)
{
    /// <summary>Alle gefüllten Kurven des Projekts, nach Besitzer und Feldname sortiert.</summary>
    public async Task<List<CurveReference>> GetCurvesAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Die Felder vom Typ „Formel/Kurve“ — ohne Projektfilter: Ein Feld hängt entweder an
        // einer Art (die kennt ihr Projekt) oder als individuelles Feld an einer Entität (die
        // nicht). Eingegrenzt wird deshalb erst unten über die Entitäten des Projekts.
        var fields = await db.FieldDefinitions
            .AsNoTracking()
            .Where(field => field.Type == ContentFieldType.Curve)
            .Select(field => new { field.Id, field.Name })
            .ToListAsync(ct);

        if (fields.Count == 0)
        {
            return [];
        }

        var fieldNames = fields.ToDictionary(field => field.Id, field => field.Name);
        var fieldIds = fieldNames.Keys.ToList();

        var values = await db.FieldValues
            .AsNoTracking()
            .Where(value => fieldIds.Contains(value.FieldDefinitionId)
                && value.TextValue != null
                && value.TextValue != "")
            .Select(value => new
            {
                value.FieldDefinitionId,
                value.OwnerEntityId,
                value.OwnerModuleKey,
                value.TextValue
            })
            .ToListAsync(ct);

        var curves = new List<CurveReference>();

        foreach (var byModule in values.GroupBy(value => value.OwnerModuleKey, StringComparer.Ordinal))
        {
            var source = sources.FirstOrDefault(entry => entry.ModuleKey == byModule.Key);
            if (source is null)
            {
                // Ein Wert aus einem Modul, das es nicht mehr gibt — nichts, was sich anbieten ließe.
                continue;
            }

            // Der Umweg über die Entitäten des Moduls im Projekt hat zwei Aufgaben auf einmal:
            // Er liefert die Namen für die Auswahl und hält den Treffer im aktuellen Projekt.
            // Feldwerte tragen keine Projekt-Spalte — dieselbe Überlegung wie bei
            // ModuleEntitySource.SearchFieldValuesAsync.
            var owners = (await source.GetEntitiesAsync(db, projectId, ct))
                .ToDictionary(entity => entity.Id, entity => entity.Name);

            foreach (var value in byModule)
            {
                if (!owners.TryGetValue(value.OwnerEntityId, out var ownerName))
                {
                    continue;
                }

                curves.Add(new CurveReference(
                    value.OwnerEntityId,
                    byModule.Key,
                    ownerName,
                    value.FieldDefinitionId,
                    fieldNames[value.FieldDefinitionId],
                    value.TextValue!));
            }
        }

        return
        [
            .. curves
                .OrderBy(curve => curve.OwnerName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(curve => curve.FieldName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(curve => curve.OwnerEntityId)
        ];
    }
}
