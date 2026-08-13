using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Eine zufällige Auswahl angelegter Entitäten quer durch alle Module — die Inhalte, die auf
/// dem Startscreen durchs Bild regnen. Rein dekorativ: schlägt etwas fehl oder ist das Projekt
/// noch leer, bleibt der Startscreen einfach ohne Regen.
/// <para>
/// Die Module melden sich wie überall über <see cref="IModuleEntitySource"/>; ein neues Modul
/// regnet damit von selbst mit. Zurück kommt <see cref="SearchHit"/>, weil er genau das trägt,
/// was eine fallende Kachel zeigt — Name, Modul und Sprite —, und die Module ihn ohnehin schon
/// je Entität bilden.
/// </para>
/// </summary>
public class StartScreenService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources)
{
    /// <summary>
    /// Wie viele Entitäten je Modul in den Topf kommen, aus dem gezogen wird. Der Topf ist
    /// bewusst deutlich größer als die Zahl der Tropfen und je Modul gleich groß: so kommt bei
    /// jedem Laden eine andere Mischung heraus, und ein Modul mit tausend Items verdrängt nicht
    /// die übrigen.
    /// </summary>
    private const int PoolPerModule = 8;

    public async Task<List<SearchHit>> SampleEntitiesAsync(
        Guid projectId, int count, CancellationToken ct = default)
    {
        if (count <= 0)
        {
            return [];
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var pool = new List<SearchHit>();

        foreach (var source in sources)
        {
            pool.AddRange(await source.SampleAsync(db, projectId, PoolPerModule, ct));
        }

        // Gemischt wird im Speicher: eine Zufallssortierung schreibt sich in jedem der vier
        // Provider anders (NEWID(), RANDOM(), RAND()), und der Topf ist klein genug dafür.
        var shuffled = pool.ToArray();
        Random.Shared.Shuffle(shuffled);

        return [.. shuffled.Take(count)];
    }
}
