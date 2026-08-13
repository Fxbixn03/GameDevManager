using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Eine fallende Entität des Startscreens: der Suchtreffer, den Suche und Dashboard ebenso
/// verwenden, dazu die Farbe ihrer Seltenheit.
/// </summary>
/// <param name="RarityColor">
/// Die Anzeigefarbe der Seltenheit, oder <c>null</c> wenn die Entität keine trägt — dann
/// bleibt die Kachel beim Akzentgelb.
/// </param>
public sealed record StartScreenEntity(SearchHit Hit, string? RarityColor);

/// <summary>
/// Eine zufällige Auswahl angelegter Entitäten quer durch alle Module — die Inhalte, die auf
/// dem Startscreen durchs Bild regnen. Rein dekorativ: schlägt etwas fehl oder ist das Projekt
/// noch leer, bleibt der Startscreen einfach ohne Regen.
/// <para>
/// Die Module melden sich wie überall über <see cref="IModuleEntitySource"/>; ein neues Modul
/// regnet damit von selbst mit. Getragen wird die Auswahl von <see cref="SearchHit"/>, weil er
/// genau das enthält, was eine fallende Kachel zeigt — Name, Modul und Sprite —, und die Module
/// ihn ohnehin schon je Entität bilden.
/// </para>
/// </summary>
public class StartScreenService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources)
{
    /// <summary>
    /// Wie viele Entitäten je Modul in den Topf kommen, aus dem reihum gezogen wird. Je Modul
    /// gleich groß, damit ein Modul mit tausend Items die übrigen nicht verdrängt; welchen
    /// Ausschnitt seines Bestands ein Modul beisteuert, würfelt es je Aufruf neu aus.
    /// </summary>
    private const int PoolPerModule = 8;

    public async Task<List<StartScreenEntity>> SampleEntitiesAsync(
        Guid projectId, int count, CancellationToken ct = default)
    {
        if (count <= 0)
        {
            return [];
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var pools = new List<SearchHit[]>();

        foreach (var source in sources)
        {
            var sample = await source.SampleAsync(db, projectId, PoolPerModule, ct);

            if (sample.Count == 0)
            {
                continue;
            }

            // Gemischt wird im Speicher: eine Zufallssortierung schreibt sich in jedem der
            // vier Provider anders (NEWID(), RANDOM(), RAND()), und die Töpfe sind klein.
            var pool = sample.ToArray();
            Random.Shared.Shuffle(pool);
            pools.Add(pool);
        }

        var drawn = DrawAcrossModules(pools, count);
        var colors = await LoadRarityColorsAsync(db, drawn, ct);

        return
        [
            .. drawn.Select(hit => new StartScreenEntity(
                hit,
                colors.GetValueOrDefault(hit.Id)))
        ];
    }

    /// <summary>
    /// Verteilt die Tropfen reihum über die Module: erst je einer aus jedem Modul, dann der
    /// zweite und so fort. Aus einem gemeinsamen Topf zu ziehen hätte bei zwanzig Modulen und
    /// sechzehn Tropfen regelmäßig ganze Module ausgelassen — gerade die kleinen, von denen
    /// nur ein Eintrag im Topf liegt.
    /// <para>
    /// Zum Schluss wird noch einmal gemischt: die Seite verteilt die Tropfen der Reihe nach
    /// auf die Bahnen, und reihum gezogen stünden die Module sonst in Spalten nebeneinander.
    /// </para>
    /// </summary>
    private static List<SearchHit> DrawAcrossModules(List<SearchHit[]> pools, int count)
    {
        if (pools.Count == 0)
        {
            return [];
        }

        // Auch die Reihenfolge der Module wird gewürfelt: reichen die Tropfen nicht für eine
        // volle Runde, kämen sonst immer dieselben Module zum Zug — die Reihenfolge hier ist
        // die ihrer Registrierung.
        var order = pools.ToArray();
        Random.Shared.Shuffle(order);

        var drawn = new List<SearchHit>(count);
        var deepest = order.Max(pool => pool.Length);

        for (var round = 0; round < deepest && drawn.Count < count; round++)
        {
            foreach (var pool in order)
            {
                if (round >= pool.Length)
                {
                    continue;
                }

                drawn.Add(pool[round]);

                if (drawn.Count == count)
                {
                    break;
                }
            }
        }

        var mixed = drawn.ToArray();
        Random.Shared.Shuffle(mixed);

        return [.. mixed];
    }

    /// <summary>
    /// Die Seltenheitsfarbe je gezogener Entität — erst nach dem Ziehen und nur für die
    /// wenigen Tropfen, statt für den ganzen Topf.
    /// <para>
    /// Eine Seltenheit hängt wie jeder andere Feldwert über die GUID an der Entität: ein Feld
    /// vom Typ <see cref="ContentFieldType.Rarity"/> zeigt auf eine <see cref="Rarity"/>. Trägt
    /// eine Entität mehrere solche Felder, gewinnt das in der Maske oberste — irgendeine feste
    /// Regel braucht es, sonst wechselte die Farbe von Aufruf zu Aufruf.
    /// </para>
    /// </summary>
    private static async Task<Dictionary<Guid, string>> LoadRarityColorsAsync(
        GameDevManagerDbContext db, List<SearchHit> hits, CancellationToken ct)
    {
        if (hits.Count == 0)
        {
            return [];
        }

        var ids = hits.Select(hit => hit.Id).ToList();

        var byField = await (
            from value in db.FieldValues.AsNoTracking()
            join definition in db.FieldDefinitions on value.FieldDefinitionId equals definition.Id
            join rarity in db.Rarities on value.ReferenceValue equals (Guid?)rarity.Id
            where ids.Contains(value.OwnerEntityId)
                && definition.Type == ContentFieldType.Rarity
                && rarity.Color != null
            orderby definition.SortOrder, definition.Name
            select new { value.OwnerEntityId, rarity.Color })
            .ToListAsync(ct);

        var colors = new Dictionary<Guid, string>();

        foreach (var row in byField)
        {
            colors.TryAdd(row.OwnerEntityId, row.Color!);
        }

        // Eine fallende Seltenheit trägt ihre eigene Farbe: sie verweist nicht auf eine Stufe,
        // sie ist eine.
        var rarityIds = hits
            .Where(hit => hit.ModuleKey == ModuleKeys.Rarities)
            .Select(hit => hit.Id)
            .ToList();

        if (rarityIds.Count == 0)
        {
            return colors;
        }

        var own = await db.Rarities
            .AsNoTracking()
            .Where(rarity => rarityIds.Contains(rarity.Id) && rarity.Color != null)
            .Select(rarity => new { rarity.Id, rarity.Color })
            .ToListAsync(ct);

        foreach (var row in own)
        {
            colors[row.Id] = row.Color!;
        }

        return colors;
    }
}
