using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der Dialoge samt Beteiligten, Zeilen und Antwortmöglichkeiten.
/// </summary>
public class DialogueService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<DialogueListRow>> GetDialoguesAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Dialogues
            .AsNoTracking()
            .Where(d => d.GameProjectId == projectId)
            .OrderBy(d => d.Name)
            .Select(d => new DialogueListRow(
                d.Id,
                d.Name,
                d.Description,
                d.Kind,
                d.IncludesPlayer,
                d.ContentTypeId,
                d.ContentType!.Name,
                d.Participants.Count,
                d.Lines.Count,
                d.UpdatedAtUtc))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Die Dialoge, an denen ein NPC beteiligt ist oder in denen er spricht — für die NPC-Maske.
    /// </summary>
    public async Task<List<DialogueListRow>> GetDialoguesForNpcAsync(
        Guid projectId, Guid npcId, CancellationToken ct = default)
    {
        var all = await GetDialoguesAsync(projectId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        var involved = await db.DialogueParticipants
            .AsNoTracking()
            .Where(p => p.NpcId == npcId)
            .Select(p => p.DialogueId)
            .Distinct()
            .ToListAsync(ct);

        return [.. all.Where(row => involved.Contains(row.Id))];
    }

    public async Task<ContentEditContext<Dialogue>?> LoadForEditAsync(
        Guid projectId, Guid? dialogueId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Dialogs, ct);

        if (dialogueId is null)
        {
            return new ContentEditContext<Dialogue>
            {
                Entity = new Dialogue { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var dialogue = await db.Dialogues
            .AsNoTracking()
            .Include(d => d.Participants)
            .Include(d => d.Lines).ThenInclude(line => line.Choices)
            .FirstOrDefaultAsync(d => d.Id == dialogueId && d.GameProjectId == projectId, ct);

        if (dialogue is null)
        {
            return null;
        }

        dialogue.Participants = [.. dialogue.Participants.OrderBy(p => p.SortOrder)];
        dialogue.Lines = [.. dialogue.Lines.OrderBy(line => line.SortOrder)];

        foreach (var line in dialogue.Lines)
        {
            line.Choices = [.. line.Choices.OrderBy(choice => choice.SortOrder)];
        }

        return new ContentEditContext<Dialogue>
        {
            Entity = dialogue,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, dialogue.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, dialogue.Id, ct)
        };
    }

    public async Task SaveDialogueAsync(ContentEditContext<Dialogue> context, CancellationToken ct = default)
    {
        var dialogue = context.Entity;

        if (string.IsNullOrWhiteSpace(dialogue.Name))
        {
            throw new ContentValidationException(messages["DialogueNameRequired"]);
        }

        Validate(dialogue);
        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.Dialogues
            .Include(d => d.Participants)
            .Include(d => d.Lines).ThenInclude(line => line.Choices)
            .FirstOrDefaultAsync(d => d.Id == dialogue.Id, ct);

        if (stored is null)
        {
            stored = new Dialogue
            {
                Id = dialogue.Id,
                GameProjectId = dialogue.GameProjectId,
                Name = dialogue.Name.Trim(),
                CreatedAtUtc = now
            };

            db.Dialogues.Add(stored);
        }

        stored.ContentTypeId = dialogue.ContentTypeId;
        stored.Name = dialogue.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(dialogue.Description) ? null : dialogue.Description.Trim();
        stored.Kind = dialogue.Kind;
        stored.IncludesPlayer = dialogue.IncludesPlayer;
        stored.UpdatedAtUtc = now;

        SyncParticipants(db, stored, dialogue);
        var removedOwners = SyncLines(db, stored, dialogue);

        // Antwortmöglichkeiten haben eigene GUIDs und können später eigene Bedingungen tragen.
        await EntityCleanup.DeleteForEntitiesAsync(db, removedOwners, ct);

        await ContentFields.StageValuesAsync(db, context, ct);
        await db.SaveChangesAsync(ct);

        dialogue.CreatedAtUtc = stored.CreatedAtUtc;
        dialogue.UpdatedAtUtc = stored.UpdatedAtUtc;
        dialogue.Name = stored.Name;
        dialogue.Description = stored.Description;
    }

    private void Validate(Dialogue dialogue)
    {
        if (dialogue.Participants.Any(p => p.NpcId == Guid.Empty))
        {
            throw new ContentValidationException(messages["DialogueParticipantNpcRequired"]);
        }

        var duplicate = dialogue.Participants
            .GroupBy(p => p.NpcId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ContentValidationException(messages["DialogueParticipantDuplicate"]);
        }

        if (dialogue.Lines.Any(line => string.IsNullOrWhiteSpace(line.Text)))
        {
            throw new ContentValidationException(messages["DialogueLineTextRequired"]);
        }

        var participantIds = dialogue.Participants.Select(p => p.NpcId).ToHashSet();

        foreach (var line in dialogue.Lines)
        {
            if (line.SpeakerNpcId is { } speaker && !participantIds.Contains(speaker))
            {
                throw new ContentValidationException(messages["DialogueSpeakerNotParticipant"]);
            }

            if (line.SpeakerNpcId is null && !dialogue.IncludesPlayer)
            {
                throw new ContentValidationException(messages["DialoguePlayerNotParticipant"]);
            }

            if (dialogue.Kind == DialogueKind.Bark && line.Choices.Count > 0)
            {
                throw new ContentValidationException(messages["DialogueBarkHasNoChoices"]);
            }

            if (line.Choices.Any(choice => string.IsNullOrWhiteSpace(choice.Text)))
            {
                throw new ContentValidationException(messages["DialogueChoiceTextRequired"]);
            }
        }

        var lineIds = dialogue.Lines.Select(line => line.Id).ToHashSet();

        foreach (var choice in dialogue.Lines.SelectMany(line => line.Choices))
        {
            if (choice.NextLineId is { } next && !lineIds.Contains(next))
            {
                throw new ContentValidationException(messages["DialogueChoiceTargetMissing"]);
            }
        }
    }

    private static void SyncParticipants(GameDevManagerDbContext db, Dialogue stored, Dialogue incoming)
    {
        var wantedIds = incoming.Participants.Select(p => p.Id).ToHashSet();

        foreach (var obsolete in stored.Participants.Where(p => !wantedIds.Contains(p.Id)).ToList())
        {
            stored.Participants.Remove(obsolete);
        }

        for (var index = 0; index < incoming.Participants.Count; index++)
        {
            var participant = incoming.Participants[index];
            var target = stored.Participants.FirstOrDefault(p => p.Id == participant.Id);

            if (target is null)
            {
                // Ausdrücklich über das DbSet — die GUID ist schon vergeben, EF hielte den
                // Datensatz beim Anhängen sonst für einen vorhandenen.
                db.DialogueParticipants.Add(new DialogueParticipant
                {
                    Id = participant.Id,
                    DialogueId = stored.Id,
                    NpcId = participant.NpcId,
                    SortOrder = index
                });
            }
            else
            {
                target.NpcId = participant.NpcId;
                target.SortOrder = index;
            }
        }
    }

    /// <summary>
    /// Schreibt Zeilen und Antwortmöglichkeiten fort und liefert die GUIDs dessen zurück, was
    /// entfernt wurde — daran können Bedingungen hängen.
    /// </summary>
    private static List<Guid> SyncLines(GameDevManagerDbContext db, Dialogue stored, Dialogue incoming)
    {
        var removed = new List<Guid>();
        var wantedLineIds = incoming.Lines.Select(line => line.Id).ToHashSet();

        foreach (var obsolete in stored.Lines.Where(line => !wantedLineIds.Contains(line.Id)).ToList())
        {
            removed.Add(obsolete.Id);
            removed.AddRange(obsolete.Choices.Select(choice => choice.Id));
            stored.Lines.Remove(obsolete);
        }

        for (var index = 0; index < incoming.Lines.Count; index++)
        {
            var line = incoming.Lines[index];
            var target = stored.Lines.FirstOrDefault(l => l.Id == line.Id);

            if (target is null)
            {
                var created = new DialogueLine
                {
                    Id = line.Id,
                    DialogueId = stored.Id,
                    SpeakerNpcId = line.SpeakerNpcId,
                    Text = line.Text.Trim(),
                    SortOrder = index
                };

                db.DialogueLines.Add(created);

                foreach (var (choice, position) in line.Choices.Select((choice, position) => (choice, position)))
                {
                    db.DialogueChoices.Add(CreateChoice(choice, created.Id, position));
                }

                continue;
            }

            target.SpeakerNpcId = line.SpeakerNpcId;
            target.Text = line.Text.Trim();
            target.SortOrder = index;

            var wantedChoiceIds = line.Choices.Select(choice => choice.Id).ToHashSet();

            foreach (var obsolete in target.Choices.Where(c => !wantedChoiceIds.Contains(c.Id)).ToList())
            {
                removed.Add(obsolete.Id);
                target.Choices.Remove(obsolete);
            }

            for (var position = 0; position < line.Choices.Count; position++)
            {
                var choice = line.Choices[position];
                var storedChoice = target.Choices.FirstOrDefault(c => c.Id == choice.Id);

                if (storedChoice is null)
                {
                    db.DialogueChoices.Add(CreateChoice(choice, target.Id, position));
                }
                else
                {
                    storedChoice.Text = choice.Text.Trim();
                    storedChoice.NextLineId = choice.NextLineId;
                    storedChoice.SortOrder = position;
                }
            }
        }

        return removed;
    }

    private static DialogueChoice CreateChoice(DialogueChoice source, Guid lineId, int position) => new()
    {
        Id = source.Id,
        DialogueLineId = lineId,
        Text = source.Text.Trim(),
        NextLineId = source.NextLineId,
        SortOrder = position
    };

    /// <summary>Löscht einen Dialog samt allem, was daran hängt.</summary>
    public async Task DeleteDialogueAsync(Guid dialogueId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(dialogueId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Zeilen und Antworten haben eigene GUIDs und können Bedingungen tragen.
        var lineIds = await db.DialogueLines
            .Where(line => line.DialogueId == dialogueId)
            .Select(line => line.Id)
            .ToListAsync(ct);

        var choiceIds = await db.DialogueChoices
            .Where(choice => lineIds.Contains(choice.DialogueLineId))
            .Select(choice => choice.Id)
            .ToListAsync(ct);

        await EntityCleanup.DeleteForEntitiesAsync(db, [dialogueId, .. lineIds, .. choiceIds], ct);

        // Beteiligte, Zeilen und Antworten fallen über die Fremdschlüssel mit.
        await db.Dialogues
            .Where(d => d.Id == dialogueId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }

    // ------------------------------------------------------------------------------ Graph

    /// <summary>
    /// Ein Gespräch als Knoten-Graph: Zeilen als Knoten, Antwortmöglichkeiten als Kanten.
    /// <c>null</c>, wenn es den Dialog nicht (mehr) gibt.
    /// <para>
    /// Die <see cref="DialogueGraphNode.Depth"/> ist der Abstand von der Einstiegszeile —
    /// daraus baut die Ansicht ihre Spalten, und dieselbe Breitensuche beantwortet nebenbei
    /// die Frage des Health Checks: Was von der ersten Zeile aus nie erreicht wird, bekommt
    /// <c>-1</c> und steht damit sichtbar außerhalb des Verlaufs.
    /// </para>
    /// <para>
    /// Sprechblasen haben keinen Verlauf — ihre Zeilen stehen absichtlich unabhängig
    /// nebeneinander und liegen deshalb alle auf Tiefe 0.
    /// </para>
    /// </summary>
    public async Task<DialogueGraph?> GetGraphAsync(
        Guid projectId, Guid dialogueId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var dialogue = await db.Dialogues
            .AsNoTracking()
            .Include(d => d.Lines).ThenInclude(line => line.Choices)
            .FirstOrDefaultAsync(d => d.Id == dialogueId && d.GameProjectId == projectId, ct);

        if (dialogue is null)
        {
            return null;
        }

        var lines = dialogue.Lines.OrderBy(line => line.SortOrder).ToList();

        var speakerIds = lines
            .Where(line => line.SpeakerNpcId is not null)
            .Select(line => line.SpeakerNpcId!.Value)
            .Distinct()
            .ToList();

        var speakers = await db.Npcs
            .AsNoTracking()
            .Where(npc => speakerIds.Contains(npc.Id))
            .ToDictionaryAsync(npc => npc.Id, npc => npc.Name, ct);

        var player = messages["DialogueGraph_Player"].Value;
        var unknownSpeaker = messages["DialogueGraph_UnknownSpeaker"].Value;

        var depths = ComputeDepths(dialogue, lines);

        var nodes = lines
            .Select((line, position) => new DialogueGraphNode(
                line.Id,
                line.SpeakerNpcId is { } npcId
                    ? speakers.GetValueOrDefault(npcId, unknownSpeaker)
                    : player,
                line.Text,
                depths[line.Id],
                IsEntry: dialogue.Kind == DialogueKind.Bark || position == 0,
                EndsHere: line.Choices.Count == 0 || line.Choices.Any(choice => choice.NextLineId is null)))
            .ToList();

        var edges = lines
            .SelectMany(line => line.Choices
                .OrderBy(choice => choice.SortOrder)
                .Where(choice => choice.NextLineId is not null)
                .Select(choice => new DialogueGraphEdge(line.Id, choice.NextLineId!.Value, choice.Text)))
            .ToList();

        return new DialogueGraph(dialogue.Id, dialogue.Name, dialogue.Kind, nodes, edges);
    }

    /// <summary>
    /// Breitensuche von der Einstiegszeile aus. Sprechblasen haben keinen Verlauf, dort liegt
    /// alles auf Tiefe 0; in einem Gespräch bekommt Unerreichbares <c>-1</c>.
    /// </summary>
    private static Dictionary<Guid, int> ComputeDepths(Dialogue dialogue, List<DialogueLine> lines)
    {
        var depths = lines.ToDictionary(line => line.Id, _ => dialogue.Kind == DialogueKind.Bark ? 0 : -1);

        if (dialogue.Kind == DialogueKind.Bark || lines.Count == 0)
        {
            return depths;
        }

        var byId = lines.ToDictionary(line => line.Id);
        var queue = new Queue<DialogueLine>([lines[0]]);
        depths[lines[0].Id] = 0;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var choice in current.Choices)
            {
                if (choice.NextLineId is { } next
                    && byId.TryGetValue(next, out var line)
                    && depths[next] < 0)
                {
                    depths[next] = depths[current.Id] + 1;
                    queue.Enqueue(line);
                }
            }
        }

        return depths;
    }

    // ------------------------------------------------------------------------ Health Check

    /// <summary>
    /// Der Health Check „Dialog-Sackgassen“ aus dem Konzept. Gemeldet wird Inhalt, den der
    /// Spieler nie zu sehen bekommt: Zeilen, die von der ersten aus über keine Antwort
    /// erreichbar sind. Bei Sprechblasen greift die Prüfung nicht — dort stehen die Zeilen
    /// absichtlich unabhängig nebeneinander.
    /// <para>
    /// Eine Zeile ohne Antworten ist dagegen <b>kein</b> Fund: Das ist das normale Ende eines
    /// Gesprächs.
    /// </para>
    /// </summary>
    public async Task<List<DialogueProblem>> FindProblemsAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var dialogues = await db.Dialogues
            .AsNoTracking()
            .Where(d => d.GameProjectId == projectId && d.Kind == DialogueKind.Conversation)
            .Include(d => d.Lines).ThenInclude(line => line.Choices)
            .ToListAsync(ct);

        var problems = new List<DialogueProblem>();

        foreach (var dialogue in dialogues)
        {
            var lines = dialogue.Lines.OrderBy(line => line.SortOrder).ToList();
            if (lines.Count == 0)
            {
                continue;
            }

            var byId = lines.ToDictionary(line => line.Id);
            var reachable = new HashSet<Guid>();
            var queue = new Queue<DialogueLine>([lines[0]]);
            reachable.Add(lines[0].Id);

            while (queue.Count > 0)
            {
                foreach (var choice in queue.Dequeue().Choices)
                {
                    if (choice.NextLineId is { } next && byId.TryGetValue(next, out var line) && reachable.Add(next))
                    {
                        queue.Enqueue(line);
                    }
                }
            }

            foreach (var unreachable in lines.Where(line => !reachable.Contains(line.Id)))
            {
                problems.Add(new DialogueProblem(
                    dialogue.Id,
                    dialogue.Name,
                    unreachable.Id,
                    messages["DialogueLineUnreachable", Shorten(unreachable.Text)]));
            }
        }

        return problems;
    }

    private static string Shorten(string text) =>
        text.Length <= 50 ? text : string.Concat(text.AsSpan(0, 50), "…");
}

/// <summary>Ein Fund der Sackgassen-Prüfung.</summary>
public sealed record DialogueProblem(Guid DialogueId, string DialogueName, Guid LineId, string Message);

/// <summary>
/// Ein Knoten des Dialog-Graphen: eine gesprochene Zeile. <paramref name="Depth"/> ist der
/// Abstand von der Einstiegszeile, <c>-1</c> heißt „von dort aus nicht erreichbar“.
/// </summary>
public sealed record DialogueGraphNode(
    Guid LineId,
    string Speaker,
    string Text,
    int Depth,
    bool IsEntry,
    bool EndsHere)
{
    /// <summary>Von der Einstiegszeile aus nie erreichbar — der Fund des Health Checks.</summary>
    public bool IsUnreachable => Depth < 0;
}

/// <summary>
/// Eine Kante: eine Antwortmöglichkeit, die zu einer anderen Zeile führt. Antworten ohne Ziel
/// beenden das Gespräch und sind keine Kante — sie stehen als Merkmal am Knoten.
/// </summary>
public sealed record DialogueGraphEdge(Guid FromLineId, Guid ToLineId, string Text);

/// <summary>Ein Gespräch als Graph — Zeilen als Knoten, Antworten als Kanten.</summary>
public sealed record DialogueGraph(
    Guid DialogueId,
    string Name,
    DialogueKind Kind,
    IReadOnlyList<DialogueGraphNode> Nodes,
    IReadOnlyList<DialogueGraphEdge> Edges)
{
    public int UnreachableCount => Nodes.Count(node => node.IsUnreachable);
}
