using Xunit;
using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Tests;

/// <summary>
/// Der Umbau des Spieler-Moduls: Spielerfiguren werden in NPCs überführt — dieselbe GUID,
/// alle Anhänge (Bedingungen, Feldwerte, Sprites) wandern über den Modul-Schlüssel mit.
/// </summary>
public sealed class PlayerConversionTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private PlayerService Player => _database.GetService<PlayerService>();

    [Fact]
    public async Task ConvertsCharactersToUniqueNpcsKeepingGuidAndAttachments()
    {
        var character = new PlayerCharacter
        {
            GameProjectId = _database.ProjectId,
            Name = "Heldin",
            Description = "Die Spielfigur"
        };
        await Player.SaveCharacterAsync(character);

        // Ein Bedingungssatz, der an der Figur hängt — er muss die Überführung überstehen.
        await using (var db = _database.CreateContext())
        {
            db.ConditionSets.Add(new ConditionSet
            {
                GameProjectId = _database.ProjectId,
                OwnerId = character.Id,
                OwnerModuleKey = ModuleKeys.Player,
                Slot = ConditionSlots.Availability
            });
            await db.SaveChangesAsync();
        }

        var converted = await Player.ConvertCharactersToNpcsAsync(_database.ProjectId);
        Assert.Equal(1, converted);

        await using var check = _database.CreateContext();

        Assert.Empty(await check.PlayerCharacters.ToListAsync());

        var npc = await check.Npcs.SingleAsync();
        Assert.Equal(character.Id, npc.Id);
        Assert.Equal("Heldin", npc.Name);
        Assert.True(npc.IsUnique);
        Assert.Equal(NpcKind.Npc, npc.Kind);

        var conditionSet = await check.ConditionSets.SingleAsync();
        Assert.Equal(ModuleKeys.Npcs, conditionSet.OwnerModuleKey);

        // Ohne Figuren ist die Überführung ein stilles Nichts.
        Assert.Equal(0, await Player.ConvertCharactersToNpcsAsync(_database.ProjectId));
    }
}
