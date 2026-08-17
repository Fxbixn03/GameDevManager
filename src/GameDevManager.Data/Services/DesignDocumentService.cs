using System.Net;
using System.Text;
using GameDevManager.Data.Assets;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Welche Kapitel das Design-Dokument bekommt — feste Reihenfolge, freie Auswahl.</summary>
public sealed record DesignChapters(bool Story, bool Factions, bool Npcs, bool Quests, bool Items)
{
    public static DesignChapters All { get; } = new(true, true, true, true, true);
}

/// <summary>
/// Erzeugt aus dem Bestand ein lesbares Dokument (F40): Story, Figuren, Fraktionen, Quests und
/// Items als eine <b>eigenständige</b> HTML-Datei — Bilder als <c>data:</c>-URI eingebettet,
/// kein Verweis nach außen. Etwas, das man einem Publisher oder neuen Teammitglied in die Hand
/// gibt; das PDF liefert der Browser-Druck, ohne dass eine PDF-Bibliothek einzieht.
/// <para>
/// Das Export-ZIP ist für Maschinen, die Oberfläche für die tägliche Arbeit — dieses Dokument
/// ist zum Zeigen. Es ist vollständig abgeleitet und hat kein eigenes Format: keine
/// <c>FormatVersion</c>, kein Import, kein Diff.
/// </para>
/// </summary>
public class DesignDocumentService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IAssetStorage storage,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>Bilder über dieser Größe bleiben draußen — ein Dokument, kein Archiv.</summary>
    private const long MaxEmbeddedImageBytes = 1_500_000;

    public async Task<string> BuildHtmlAsync(
        Guid projectId, DesignChapters chapters, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var project = await db.GameProjects.AsNoTracking().FirstAsync(p => p.Id == projectId, ct);

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"de\"><head><meta charset=\"utf-8\">");
        html.Append("<title>").Append(Encode(project.Name)).Append("</title>");
        html.Append("<style>").Append(Stylesheet).Append("</style></head><body>");

        html.Append("<header><h1>").Append(Encode(project.Name)).Append("</h1>");
        if (!string.IsNullOrWhiteSpace(project.Description))
        {
            html.Append("<p class=\"lead\">").Append(Encode(project.Description)).Append("</p>");
        }
        html.Append("<p class=\"meta\">")
            .Append(Encode(messages["Design_GeneratedAt", DateTime.Now.ToString("dd.MM.yyyy HH:mm")]))
            .Append("</p></header>");

        if (chapters.Story)
        {
            await AppendStoryAsync(html, db, projectId, ct);
        }

        if (chapters.Factions)
        {
            await AppendFactionsAsync(html, db, projectId, ct);
        }

        if (chapters.Npcs)
        {
            await AppendNpcsAsync(html, db, projectId, ct);
        }

        if (chapters.Quests)
        {
            await AppendQuestsAsync(html, db, projectId, ct);
        }

        if (chapters.Items)
        {
            await AppendItemsAsync(html, db, projectId, ct);
        }

        html.Append("</body></html>");
        return html.ToString();
    }

    // ------------------------------------------------------------------------ Kapitel

    private async Task AppendStoryAsync(
        StringBuilder html, GameDevManagerDbContext db, Guid projectId, CancellationToken ct)
    {
        var entries = await db.StoryEntries
            .AsNoTracking()
            .Where(entry => entry.GameProjectId == projectId)
            .OrderBy(entry => entry.SortOrder).ThenBy(entry => entry.Name)
            .ToListAsync(ct);

        if (entries.Count == 0)
        {
            return;
        }

        html.Append("<section><h2>").Append(Encode(messages["Design_ChapterStory"])).Append("</h2>");

        foreach (var entry in entries)
        {
            html.Append("<article><h3>").Append(Encode(entry.Name)).Append("</h3>");

            var meta = new[] { entry.GameDate, entry.Location, entry.Mood }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToList();
            if (meta.Count > 0)
            {
                html.Append("<p class=\"meta\">").Append(Encode(string.Join(" · ", meta))).Append("</p>");
            }

            // Der Story-Text ist Markdown mit Erwähnungen — die Stücke kommen fertig maskiert,
            // Erwähnungen als bloßer Anzeigename: Links ins Tool trügen im Dokument nichts.
            foreach (var block in SimpleMarkdown.Render(entry.Body))
            {
                html.Append("<p>");
                foreach (var segment in block)
                {
                    html.Append(segment.Html);
                }
                html.Append("</p>");
            }

            html.Append("</article>");
        }

        html.Append("</section>");
    }

    private async Task AppendFactionsAsync(
        StringBuilder html, GameDevManagerDbContext db, Guid projectId, CancellationToken ct)
    {
        var factions = await db.Factions
            .AsNoTracking()
            .Where(faction => faction.GameProjectId == projectId)
            .OrderBy(faction => faction.Name)
            .Select(faction => new { faction.Id, faction.Name, faction.Description, TypeName = faction.ContentType!.Name })
            .ToListAsync(ct);

        if (factions.Count == 0)
        {
            return;
        }

        var images = await LoadImagesAsync(db, factions.Select(f => f.Id).ToList(), ct);

        html.Append("<section><h2>").Append(Encode(messages["Design_ChapterFactions"])).Append("</h2>");

        foreach (var faction in factions)
        {
            html.Append("<article>");
            AppendCardHeader(html, faction.Name, faction.TypeName, images.GetValueOrDefault(faction.Id));
            AppendDescription(html, faction.Description);
            html.Append("</article>");
        }

        html.Append("</section>");
    }

    private async Task AppendNpcsAsync(
        StringBuilder html, GameDevManagerDbContext db, Guid projectId, CancellationToken ct)
    {
        var npcs = await db.Npcs
            .AsNoTracking()
            .Where(npc => npc.GameProjectId == projectId)
            .OrderBy(npc => npc.Name)
            .Select(npc => new
            {
                npc.Id,
                npc.Name,
                npc.Description,
                npc.Kind,
                npc.Personality,
                npc.Preferences,
                TypeName = npc.ContentType!.Name
            })
            .ToListAsync(ct);

        if (npcs.Count == 0)
        {
            return;
        }

        var images = await LoadImagesAsync(db, npcs.Select(n => n.Id).ToList(), ct);

        html.Append("<section><h2>").Append(Encode(messages["Design_ChapterNpcs"])).Append("</h2>");

        foreach (var npc in npcs)
        {
            var subtitle = npc.Kind == NpcKind.Mob
                ? messages["Design_NpcMob"].Value
                : npc.TypeName;

            html.Append("<article>");
            AppendCardHeader(html, npc.Name, subtitle, images.GetValueOrDefault(npc.Id));
            AppendDescription(html, npc.Description);

            var traits = new[] { npc.Personality, npc.Preferences }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToList();
            if (traits.Count > 0)
            {
                html.Append("<p class=\"meta\">").Append(Encode(string.Join(" · ", traits))).Append("</p>");
            }

            html.Append("</article>");
        }

        html.Append("</section>");
    }

    private async Task AppendQuestsAsync(
        StringBuilder html, GameDevManagerDbContext db, Guid projectId, CancellationToken ct)
    {
        var quests = await db.Quests
            .AsNoTracking()
            .Include(quest => quest.Objectives)
            .Where(quest => quest.GameProjectId == projectId)
            .OrderBy(quest => quest.Name)
            .ToListAsync(ct);

        if (quests.Count == 0)
        {
            return;
        }

        html.Append("<section><h2>").Append(Encode(messages["Design_ChapterQuests"])).Append("</h2>");

        foreach (var quest in quests)
        {
            html.Append("<article><h3>").Append(Encode(quest.Name)).Append("</h3>");
            AppendDescription(html, quest.Description);

            if (quest.Objectives.Count > 0)
            {
                html.Append("<ol>");
                foreach (var objective in quest.Objectives.OrderBy(o => o.SortOrder))
                {
                    html.Append("<li>").Append(Encode(objective.Text));
                    if (objective.IsOptional)
                    {
                        html.Append(" <em>").Append(Encode(messages["Design_Optional"])).Append("</em>");
                    }
                    html.Append("</li>");
                }
                html.Append("</ol>");
            }

            html.Append("</article>");
        }

        html.Append("</section>");
    }

    private async Task AppendItemsAsync(
        StringBuilder html, GameDevManagerDbContext db, Guid projectId, CancellationToken ct)
    {
        var items = await db.Items
            .AsNoTracking()
            .Where(item => item.GameProjectId == projectId)
            .OrderBy(item => item.ContentType!.Name).ThenBy(item => item.Name)
            .Select(item => new { item.Id, item.Name, item.Description, TypeName = item.ContentType!.Name })
            .ToListAsync(ct);

        if (items.Count == 0)
        {
            return;
        }

        var images = await LoadImagesAsync(db, items.Select(i => i.Id).ToList(), ct);

        html.Append("<section><h2>").Append(Encode(messages["Design_ChapterItems"])).Append("</h2>");

        foreach (var item in items)
        {
            html.Append("<article>");
            AppendCardHeader(html, item.Name, item.TypeName, images.GetValueOrDefault(item.Id));
            AppendDescription(html, item.Description);
            html.Append("</article>");
        }

        html.Append("</section>");
    }

    // ------------------------------------------------------------------------ Bausteine

    private static void AppendCardHeader(StringBuilder html, string name, string? subtitle, string? imageUri)
    {
        if (imageUri is not null)
        {
            html.Append("<img class=\"sprite\" alt=\"\" src=\"").Append(imageUri).Append("\">");
        }

        html.Append("<h3>").Append(Encode(name));
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            html.Append(" <small>").Append(Encode(subtitle)).Append("</small>");
        }
        html.Append("</h3>");
    }

    private static void AppendDescription(StringBuilder html, string? description)
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            html.Append("<p>").Append(Encode(description)).Append("</p>");
        }
    }

    /// <summary>
    /// Die primären Sprites der Entitäten als <c>data:</c>-URI. Nur Bilder, nur bis zur
    /// Größengrenze — was fehlt oder zu groß ist, fällt still weg; das Dokument bleibt lesbar.
    /// </summary>
    private async Task<Dictionary<Guid, string>> LoadImagesAsync(
        GameDevManagerDbContext db, List<Guid> ownerIds, CancellationToken ct)
    {
        var assets = await db.Assets
            .AsNoTracking()
            .Where(asset => asset.OwnerEntityId != null
                && ownerIds.Contains(asset.OwnerEntityId.Value)
                && asset.IsPrimary
                && asset.MimeType.StartsWith("image/")
                && asset.SizeBytes <= MaxEmbeddedImageBytes)
            .Select(asset => new { OwnerId = asset.OwnerEntityId!.Value, asset.MimeType, asset.StorageKey })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, string>();

        foreach (var asset in assets)
        {
            await using var stream = storage.OpenRead(asset.StorageKey);
            if (stream is null)
            {
                continue;
            }

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            result[asset.OwnerId] = $"data:{asset.MimeType};base64,{Convert.ToBase64String(buffer.ToArray())}";
        }

        return result;
    }

    private static string Encode(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    /// <summary>
    /// Druckfreundlich und eigenständig: helle Seite, das Akzentgelb nur als Linie — die
    /// Themefarben der Anwendung gelten für die Anwendung, ein Dokument druckt man.
    /// </summary>
    private const string Stylesheet = """
        body{font-family:'Segoe UI',system-ui,sans-serif;color:#1a1a1a;background:#fff;
             max-width:52rem;margin:0 auto;padding:2rem;line-height:1.55;}
        header{border-bottom:3px solid #FFC300;margin-bottom:2rem;padding-bottom:1rem;}
        h1{margin:0 0 .3rem 0;}
        h2{border-bottom:1px solid #ddd;padding-bottom:.3rem;margin-top:2.5rem;page-break-after:avoid;}
        h3{margin:1.2rem 0 .2rem 0;page-break-after:avoid;}
        h3 small{font-weight:normal;color:#666;font-size:.75em;}
        article{page-break-inside:avoid;}
        .lead{font-size:1.1rem;color:#444;}
        .meta{color:#777;font-size:.85rem;margin:.1rem 0 .4rem 0;}
        .sprite{float:right;max-width:96px;max-height:96px;margin:0 0 .5rem 1rem;}
        ol{margin:.3rem 0 .8rem 1.2rem;}
        p{margin:.3rem 0 .6rem 0;}
        @media print{body{padding:0;}}
        """;
}
