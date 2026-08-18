using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Der Konsistenz-Assistent Story vs. Daten: tote Erwähnungen, nur erzählte NPCs und
/// unverortete Karten — gemeldet statt verboten, stummschaltbar je Entität.
/// </summary>
public class StoryConsistencyTests
{
    private static async Task<Guid> AddEntryAsync(TestDatabase test, string name, string body)
    {
        await using var db = test.CreateContext();

        var entry = new StoryEntry { GameProjectId = test.ProjectId, Name = name, Body = body };
        db.StoryEntries.Add(entry);
        await db.SaveChangesAsync();

        return entry.Id;
    }

    [Fact]
    public async Task Eine_Erwaehnung_auf_Geloeschtes_ist_ein_Fund_mit_lesbarem_Namen()
    {
        using var test = new TestDatabase();

        var gone = Guid.NewGuid();
        var entryId = await AddEntryAsync(test, "Prolog",
            $"Es beginnt mit {ContentMentions.Format(ModuleKeys.Items, gone, "Eisenschwert")}.");

        var findings = await test.GetService<StoryConsistencyService>().FindProblemsAsync(test.ProjectId);

        var finding = Assert.Single(findings);
        Assert.Equal(HealthCheckKeys.StoryDeadMentions, finding.CheckKey);
        Assert.Equal(entryId, finding.StoryEntryId);
        Assert.Equal("Eisenschwert", finding.TargetName);
        Assert.Null(finding.TargetModuleKey);
        Assert.Equal(entryId, finding.MuteEntityId);
    }

    [Fact]
    public async Task Ein_nur_erzaehlter_NPC_faellt_auf_ein_angebundener_nicht()
    {
        using var test = new TestDatabase();

        Guid erzaehlt, angebunden;
        await using (var db = test.CreateContext())
        {
            var ghost = new Npc { GameProjectId = test.ProjectId, Name = "Der Erzählte" };
            var speaker = new Npc { GameProjectId = test.ProjectId, Name = "Alrik" };

            // Alrik spricht in einem Dialog — er handelt auch in den Daten.
            var dialogue = new Dialogue
            {
                GameProjectId = test.ProjectId, Name = "Begrüßung", Kind = DialogueKind.Conversation
            };
            var line = new DialogueLine
            {
                DialogueId = dialogue.Id, Text = "Seid gegrüßt!", SortOrder = 0, SpeakerNpcId = speaker.Id
            };

            db.Npcs.AddRange(ghost, speaker);
            db.Dialogues.Add(dialogue);
            db.DialogueLines.Add(line);
            await db.SaveChangesAsync();

            (erzaehlt, angebunden) = (ghost.Id, speaker.Id);
        }

        await AddEntryAsync(test, "Kapitel 1",
            $"{ContentMentions.Format(ModuleKeys.Npcs, erzaehlt, "Der Erzählte")} trifft "
            + $"{ContentMentions.Format(ModuleKeys.Npcs, angebunden, "Alrik")}.");

        var findings = await test.GetService<StoryConsistencyService>().FindProblemsAsync(test.ProjectId);

        var finding = Assert.Single(findings);
        Assert.Equal(HealthCheckKeys.StoryUnlinkedNpcs, finding.CheckKey);
        Assert.Equal(erzaehlt, finding.TargetEntityId);
        Assert.Equal(erzaehlt, finding.MuteEntityId);
    }

    [Fact]
    public async Task Eine_Bedingung_auf_den_NPC_gilt_als_Anbindung()
    {
        using var test = new TestDatabase();

        Guid npcId;
        await using (var db = test.CreateContext())
        {
            var npc = new Npc { GameProjectId = test.ProjectId, Name = "Alrik" };
            npcId = npc.Id;

            // „Sprich mit Alrik“ — die Quest bindet ihn über das Bedingungssystem an.
            db.Npcs.Add(npc);
            db.ConditionSets.Add(new ConditionSet
            {
                GameProjectId = test.ProjectId,
                OwnerId = Guid.NewGuid(),
                OwnerModuleKey = ModuleKeys.Quests,
                Slot = ConditionSlots.Completion,
                Conditions =
                [
                    new Condition
                    {
                        Kind = ConditionKind.NpcDefeated,
                        TargetModuleKey = ModuleKeys.Npcs,
                        TargetEntityId = npc.Id
                    }
                ]
            });
            await db.SaveChangesAsync();
        }

        await AddEntryAsync(test, "Kapitel 1",
            $"{ContentMentions.Format(ModuleKeys.Npcs, npcId, "Alrik")} wartet am Tor.");

        Assert.Empty(await test.GetService<StoryConsistencyService>().FindProblemsAsync(test.ProjectId));
    }

    [Fact]
    public async Task Eine_erwaehnte_Karte_ohne_Markierung_ist_ein_Fund()
    {
        using var test = new TestDatabase();

        Guid leer, verortet;
        await using (var db = test.CreateContext())
        {
            var empty = new GameMap { GameProjectId = test.ProjectId, Name = "Nebeltal" };
            var marked = new GameMap { GameProjectId = test.ProjectId, Name = "Hafen" };

            db.Maps.AddRange(empty, marked);
            db.MapMarkers.Add(new MapMarker { MapId = marked.Id, Label = "Anleger", X = 0.5, Y = 0.5 });
            await db.SaveChangesAsync();

            (leer, verortet) = (empty.Id, marked.Id);
        }

        await AddEntryAsync(test, "Kapitel 2",
            $"Vom {ContentMentions.Format(ModuleKeys.Maps, verortet, "Hafen")} ins "
            + $"{ContentMentions.Format(ModuleKeys.Maps, leer, "Nebeltal")}.");

        var findings = await test.GetService<StoryConsistencyService>().FindProblemsAsync(test.ProjectId);

        var finding = Assert.Single(findings);
        Assert.Equal(HealthCheckKeys.StoryEmptyMaps, finding.CheckKey);
        Assert.Equal(leer, finding.TargetEntityId);
    }

    [Fact]
    public async Task Stummgeschaltete_Funde_zaehlen_im_Zustandsband_nicht()
    {
        using var test = new TestDatabase();

        Guid npcId;
        await using (var db = test.CreateContext())
        {
            var npc = new Npc { GameProjectId = test.ProjectId, Name = "Der Erzählte" };
            npcId = npc.Id;
            db.Npcs.Add(npc);
            await db.SaveChangesAsync();
        }

        await AddEntryAsync(test, "Kapitel 1",
            $"{ContentMentions.Format(ModuleKeys.Npcs, npcId, "Der Erzählte")} bleibt Legende.");

        var overview = test.GetService<DashboardOverviewService>();

        var loud = await overview.GetHealthAsync(test.ProjectId);
        Assert.Equal(1, loud.Checks.Single(c => c.CheckKey == HealthCheckKeys.StoryUnlinkedNpcs).Findings);

        // „Bewusst nur erzählt“ — einmal je NPC stummgeschaltet, nicht je Abschnitt.
        await test.GetService<HealthCheckMuteService>()
            .MuteAsync(test.ProjectId, HealthCheckKeys.StoryUnlinkedNpcs, npcId, "Der Erzählte");

        var muted = await overview.GetHealthAsync(test.ProjectId);
        Assert.Equal(0, muted.Checks.Single(c => c.CheckKey == HealthCheckKeys.StoryUnlinkedNpcs).Findings);
    }
}
