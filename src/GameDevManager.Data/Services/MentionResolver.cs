using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Löst <c>@Name</c> in Fließtexten gegen den Bestand des Projekts auf und schreibt die
/// stabile Form <c>[[modul:guid|Name]]</c>.
/// <para>
/// Gesucht wird über die <see cref="IModuleEntitySource"/> in <b>allen</b> Modulen — ein neues
/// Modul ist damit von selbst dabei, dieselbe Überlegung wie bei Suche, Referenzansicht und
/// Duplizieren.
/// </para>
/// <para>
/// Bei zwei gleichnamigen Entitäten in verschiedenen Modulen gewinnt die <b>erste</b> in der
/// Reihenfolge der Registry, und die ist die Umsetzungsreihenfolge — Items vor Effekten. Das
/// ist eine Setzung und keine Wahrheit; wer es genauer braucht, verlinkt über die
/// Referenz-Auswahl statt über einen Namen. Stillschweigend beide zu verwerfen wäre die
/// schlechtere Antwort: Dann führte die häufigste Erwähnung nirgendwohin.
/// </para>
/// </summary>
public class MentionResolver(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources)
{
    /// <summary>Schreibt die stabile Form. Was sich nicht auflösen lässt, bleibt stehen.</summary>
    public async Task<string?> ResolveAsync(Guid projectId, string? text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.Contains('@'))
        {
            return text;
        }

        return ContentMentions.Resolve(text, await BuildIndexAsync(projectId, ct));
    }

    private async Task<Dictionary<string, ContentMention>> BuildIndexAsync(
        Guid projectId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var index = new Dictionary<string, ContentMention>(StringComparer.Ordinal);

        foreach (var source in sources)
        {
            foreach (var entity in await source.GetEntitiesAsync(db, projectId, ct))
            {
                if (string.IsNullOrWhiteSpace(entity.Name))
                {
                    continue;
                }

                // TryAdd: Die erste Quelle gewinnt — siehe die Begründung an der Klasse.
                index.TryAdd(entity.Name, new ContentMention(entity.ModuleKey, entity.Id, entity.Name));
            }
        }

        return index;
    }
}
