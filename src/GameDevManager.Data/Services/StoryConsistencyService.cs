using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Ein Fund des Konsistenz-Assistenten: Story-Abschnitt und Gegenstelle, beides als
/// Sprungziel. <paramref name="MuteEntityId"/> ist der Anker der Stummschaltung — bei toten
/// Erwähnungen der Story-Abschnitt (die Entität ist ja weg), sonst die erwähnte Entität:
/// Eine bewusst nur erzählte Figur schaltet man einmal stumm, nicht je Abschnitt.
/// </summary>
public sealed record StoryConsistencyFinding(
    string CheckKey,
    Guid StoryEntryId,
    string StoryEntryName,
    Guid TargetEntityId,
    string TargetName,
    string? TargetModuleKey,
    Guid MuteEntityId);

/// <summary>
/// Der Konsistenz-Assistent Story vs. Daten: Die Story-Texte erwähnen Entitäten
/// (<see cref="ContentMentions"/>) — hier wird abgeglichen, ob die Daten dazu passen.
/// Drei Prüfungen, alle <b>gemeldet statt verboten</b>, wie jeder Health Check:
/// <list type="bullet">
/// <item><b>Tote Erwähnungen</b> — der Text nennt eine Entität, die es nicht mehr gibt; der
/// Anzeigename der Erwähnung bleibt lesbar und wird gemeldet.</item>
/// <item><b>Nur erzählte NPCs</b> — ein NPC handelt im Text, kommt aber weder in einem
/// Dialog vor (Beteiligter oder Sprecher) noch zeigt irgendeine Bedingung auf ihn
/// (Quests binden NPCs über Bedingungen an). Kann Absicht sein — deshalb je NPC
/// stummschaltbar.</item>
/// <item><b>Unverortete Karten</b> — ein erwähnter Ort existiert als Karte, trägt aber
/// keine einzige Markierung: erzählt, aber nirgends verortet.</item>
/// </list>
/// </summary>
public class StoryConsistencyService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources)
{
    public async Task<List<StoryConsistencyFinding>> FindProblemsAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var entries = await db.StoryEntries
            .AsNoTracking()
            .Where(entry => entry.GameProjectId == projectId && entry.Body != null)
            .OrderBy(entry => entry.Name)
            .Select(entry => new { entry.Id, entry.Name, entry.Body })
            .ToListAsync(ct);

        // Je Abschnitt und Ziel eine Erwähnung — derselbe Name fällt in einem Text oft.
        var mentions = entries
            .SelectMany(entry => ContentMentions.Parse(entry.Body)
                .DistinctBy(mention => mention.EntityId)
                .Select(mention => (Entry: entry, Mention: mention)))
            .ToList();

        if (mentions.Count == 0)
        {
            return [];
        }

        List<Guid> mentionedIds = [.. mentions.Select(pair => pair.Mention.EntityId).Distinct()];

        // Was es noch gibt, sagt die jeweilige Modul-Quelle — je Modul eine Abfrage,
        // dieselbe Auflösung wie bei der Präsenz-Übersicht.
        var resolved = new Dictionary<Guid, (string ModuleKey, string Name)>();

        foreach (var source in sources)
        {
            foreach (var (id, name) in await source.ResolveNamesAsync(db, mentionedIds, ct))
            {
                resolved.TryAdd(id, (source.ModuleKey, name));
            }
        }

        var findings = new List<StoryConsistencyFinding>();

        // ------------------------------------------------------------- Tote Erwähnungen
        foreach (var (entry, mention) in mentions.Where(pair => !resolved.ContainsKey(pair.Mention.EntityId)))
        {
            findings.Add(new StoryConsistencyFinding(
                HealthCheckKeys.StoryDeadMentions,
                entry.Id, entry.Name,
                mention.EntityId, mention.DisplayName, TargetModuleKey: null,
                MuteEntityId: entry.Id));
        }

        // ------------------------------------------------------------ Nur erzählte NPCs
        List<Guid> npcIds =
        [
            .. mentionedIds.Where(id => resolved.TryGetValue(id, out var hit) && hit.ModuleKey == ModuleKeys.Npcs)
        ];

        if (npcIds.Count > 0)
        {
            var linked = new HashSet<Guid>();

            linked.UnionWith(await db.DialogueParticipants
                .Where(participant => npcIds.Contains(participant.NpcId)
                    && participant.Dialogue!.GameProjectId == projectId)
                .Select(participant => participant.NpcId)
                .ToListAsync(ct));

            linked.UnionWith(await db.DialogueLines
                .Where(line => line.SpeakerNpcId != null
                    && npcIds.Contains(line.SpeakerNpcId!.Value)
                    && line.Dialogue!.GameProjectId == projectId)
                .Select(line => line.SpeakerNpcId!.Value)
                .ToListAsync(ct));

            // Quests, Events und alles Übrige binden NPCs über das Bedingungssystem an —
            // eine Bedingung, die auf den NPC zeigt, heißt: Er handelt auch in den Daten.
            linked.UnionWith(await db.Conditions
                .Where(condition => condition.TargetEntityId != null
                    && npcIds.Contains(condition.TargetEntityId!.Value)
                    && condition.ConditionSet!.GameProjectId == projectId)
                .Select(condition => condition.TargetEntityId!.Value)
                .ToListAsync(ct));

            foreach (var (entry, mention) in mentions)
            {
                if (npcIds.Contains(mention.EntityId) && !linked.Contains(mention.EntityId))
                {
                    findings.Add(new StoryConsistencyFinding(
                        HealthCheckKeys.StoryUnlinkedNpcs,
                        entry.Id, entry.Name,
                        mention.EntityId, resolved[mention.EntityId].Name, ModuleKeys.Npcs,
                        MuteEntityId: mention.EntityId));
                }
            }
        }

        // ----------------------------------------------------------- Unverortete Karten
        List<Guid> mapIds =
        [
            .. mentionedIds.Where(id => resolved.TryGetValue(id, out var hit) && hit.ModuleKey == ModuleKeys.Maps)
        ];

        if (mapIds.Count > 0)
        {
            var marked = (await db.MapMarkers
                    .Where(marker => mapIds.Contains(marker.MapId))
                    .Select(marker => marker.MapId)
                    .Distinct()
                    .ToListAsync(ct))
                .ToHashSet();

            foreach (var (entry, mention) in mentions)
            {
                if (mapIds.Contains(mention.EntityId) && !marked.Contains(mention.EntityId))
                {
                    findings.Add(new StoryConsistencyFinding(
                        HealthCheckKeys.StoryEmptyMaps,
                        entry.Id, entry.Name,
                        mention.EntityId, resolved[mention.EntityId].Name, ModuleKeys.Maps,
                        MuteEntityId: mention.EntityId));
                }
            }
        }

        return findings;
    }
}
