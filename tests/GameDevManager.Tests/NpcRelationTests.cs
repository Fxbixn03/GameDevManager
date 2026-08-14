using Xunit;
using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Tests;

/// <summary>
/// Beziehungen zwischen NPCs samt Beziehungsarten sowie die neuen Spalten des NPCs
/// (Einzigartig, Vorlieben, Persönlichkeit, Wesenszüge).
/// </summary>
public sealed class NpcRelationTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private NpcService Npcs => _database.GetService<NpcService>();

    private async Task<Npc> CreateNpcAsync(string name)
    {
        var context = await Npcs.LoadForEditAsync(_database.ProjectId, null);
        context!.Entity.Name = name;
        await Npcs.SaveNpcAsync(context);
        return context.Entity;
    }

    private async Task<NpcRelationType> CreateTypeAsync(string name, string inverseName)
    {
        var type = new NpcRelationType { GameProjectId = _database.ProjectId, Name = name, InverseName = inverseName };
        await Npcs.SaveRelationTypeAsync(type);
        return type;
    }

    [Fact]
    public async Task SavesRelationAndShowsInverseOnOtherNpc()
    {
        var father = await CreateNpcAsync("Anton");
        var son = await CreateNpcAsync("Berta");
        var type = await CreateTypeAsync("Ist Vater von", "Ist Sohn von");

        var context = await Npcs.LoadForEditAsync(_database.ProjectId, father.Id);
        context!.Entity.Relations.Add(new NpcRelation
        {
            NpcId = father.Id,
            OtherNpcId = son.Id,
            RelationTypeId = type.Id,
            Stance = NpcRelationStance.Friendly
        });
        await Npcs.SaveNpcAsync(context);

        var reloaded = await Npcs.LoadForEditAsync(_database.ProjectId, father.Id);
        var relation = Assert.Single(reloaded!.Entity.Relations);
        Assert.Equal(son.Id, relation.OtherNpcId);
        Assert.Equal(NpcRelationStance.Friendly, relation.Stance);

        // Die Gegenseite sieht dieselbe Beziehung mit der Gegenrichtungs-Bezeichnung.
        var incoming = Assert.Single(await Npcs.GetIncomingRelationsAsync(son.Id));
        Assert.Equal(father.Id, incoming.OwnerNpcId);
        Assert.Equal("Ist Sohn von", incoming.Label);
    }

    [Fact]
    public async Task RejectsSelfRelationAndDuplicates()
    {
        var npc = await CreateNpcAsync("Anton");
        var other = await CreateNpcAsync("Berta");
        var type = await CreateTypeAsync("Kennt", "Kennt");

        var context = await Npcs.LoadForEditAsync(_database.ProjectId, npc.Id);
        context!.Entity.Relations.Add(new NpcRelation
        {
            NpcId = npc.Id, OtherNpcId = npc.Id, RelationTypeId = type.Id
        });

        await Assert.ThrowsAsync<ContentValidationException>(() => Npcs.SaveNpcAsync(context));

        context.Entity.Relations.Clear();
        context.Entity.Relations.Add(new NpcRelation { NpcId = npc.Id, OtherNpcId = other.Id, RelationTypeId = type.Id });
        context.Entity.Relations.Add(new NpcRelation { NpcId = npc.Id, OtherNpcId = other.Id, RelationTypeId = type.Id });

        await Assert.ThrowsAsync<ContentValidationException>(() => Npcs.SaveNpcAsync(context));
    }

    [Fact]
    public async Task DeletingTargetNpcRemovesIncomingRelations()
    {
        var owner = await CreateNpcAsync("Anton");
        var target = await CreateNpcAsync("Berta");
        var type = await CreateTypeAsync("Ist Verbündeter von", "Ist Verbündeter von");

        var context = await Npcs.LoadForEditAsync(_database.ProjectId, owner.Id);
        context!.Entity.Relations.Add(new NpcRelation
        {
            NpcId = owner.Id, OtherNpcId = target.Id, RelationTypeId = type.Id
        });
        await Npcs.SaveNpcAsync(context);

        await Npcs.DeleteNpcAsync(target.Id);

        await using var db = _database.CreateContext();
        Assert.Empty(await db.NpcRelations.ToListAsync());
    }

    [Fact]
    public async Task RelationTypeInUseCannotBeDeleted()
    {
        var owner = await CreateNpcAsync("Anton");
        var target = await CreateNpcAsync("Berta");
        var type = await CreateTypeAsync("Ist Vorgesetzter von", "Ist Untergebener von");

        var context = await Npcs.LoadForEditAsync(_database.ProjectId, owner.Id);
        context!.Entity.Relations.Add(new NpcRelation
        {
            NpcId = owner.Id, OtherNpcId = target.Id, RelationTypeId = type.Id
        });
        await Npcs.SaveNpcAsync(context);

        await Assert.ThrowsAsync<ContentValidationException>(() => Npcs.DeleteRelationTypeAsync(type.Id));

        // Ohne Verwendung geht es.
        var unused = await CreateTypeAsync("Ist Bruder von", "Ist Schwester von");
        await Npcs.DeleteRelationTypeAsync(unused.Id);

        Assert.DoesNotContain(await Npcs.GetRelationTypesAsync(_database.ProjectId), t => t.Id == unused.Id);
    }

    [Fact]
    public async Task NormalizesKeywordsTraitsAndUniqueFlag()
    {
        var context = await Npcs.LoadForEditAsync(_database.ProjectId, null);
        context!.Entity.Name = "Waschbär";
        context.Entity.IsUnique = true;
        context.Entity.Preferences = " Honig ,Angeln, honig ,";
        context.Entity.Personality = "  ";
        // Unbekannte Schlüssel werden übergangen, Werte auf 0 bis 10 begrenzt.
        context.Entity.Traits = "empathy:7;unknown:3;courage:99;loyalty:0";
        await Npcs.SaveNpcAsync(context);

        await using var db = _database.CreateContext();
        var stored = await db.Npcs.SingleAsync(n => n.Id == context.Entity.Id);

        Assert.True(stored.IsUnique);
        Assert.Equal("Honig, Angeln", stored.Preferences);
        Assert.Null(stored.Personality);
        Assert.Equal("empathy:7;courage:10", stored.Traits);
    }

    [Fact]
    public void TraitsRoundTripIsCanonical()
    {
        var values = NpcTraits.Parse("compassion:5;empathy:2");

        // Format schreibt in fester Schlüsselreihenfolge — derselbe Stand, derselbe Text.
        Assert.Equal("empathy:2;compassion:5", NpcTraits.Format(values));
        Assert.Null(NpcTraits.Format(NpcTraits.Parse(null)));
        Assert.Equal(0, NpcTraits.Parse("kaputt")["empathy"]);
    }
}
