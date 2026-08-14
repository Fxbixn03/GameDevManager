using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Das Welt-Modul: Tageszeiten, Wetterlagen und Biome. Geprüft wird, was die drei
/// unterscheidet — Eindeutigkeit je Ausprägung und die eigene Reihenfolge — und dass ein
/// Weltzustand im Export ankommt.
/// </summary>
public class WorldStateTests
{
    [Fact]
    public async Task Ein_Weltzustand_wird_gespeichert_und_wieder_geladen()
    {
        using var test = new TestDatabase();
        var world = test.GetService<WorldService>();

        var context = await world.LoadForEditAsync(test.ProjectId, null, WorldStateKind.Weather);
        Assert.NotNull(context);

        context.Entity.Name = "Sturm";
        context.Entity.Description = "Sicht unter 20 Metern.";
        context.Entity.Color = "#4477AA";
        await world.SaveStateAsync(context);

        var rows = await world.GetStatesAsync(test.ProjectId, WorldStateKind.Weather);
        var stored = Assert.Single(rows);

        Assert.Equal("Sturm", stored.Name);
        Assert.Equal(WorldStateKind.Weather, stored.Kind);
        Assert.Equal("#4477AA", stored.Color);
    }

    [Fact]
    public async Task Derselbe_Name_ist_je_Auspraegung_eindeutig_aber_ueber_sie_hinweg_erlaubt()
    {
        using var test = new TestDatabase();
        var world = test.GetService<WorldService>();

        await CreateAsync(world, test.ProjectId, "Klar", WorldStateKind.Weather);

        // Dieselbe Ausprägung: abgelehnt — zwei Wetterlagen „Klar“ wären in jeder Bedingung
        // dieselbe.
        var duplicate = await world.LoadForEditAsync(test.ProjectId, null, WorldStateKind.Weather);
        duplicate!.Entity.Name = "Klar";
        await Assert.ThrowsAsync<ContentValidationException>(() => world.SaveStateAsync(duplicate));

        // Andere Ausprägung: erlaubt.
        await CreateAsync(world, test.ProjectId, "Klar", WorldStateKind.Biome);

        Assert.Equal(2, (await world.GetStatesAsync(test.ProjectId)).Count);
    }

    [Fact]
    public async Task Tageszeiten_behalten_ihre_Abfolge_statt_alphabetisch_zu_stehen()
    {
        using var test = new TestDatabase();
        var world = test.GetService<WorldService>();

        // In der Reihenfolge der Abfolge angelegt — alphabetisch stünde „Abend“ vorn.
        await CreateAsync(world, test.ProjectId, "Morgen", WorldStateKind.TimeOfDay);
        await CreateAsync(world, test.ProjectId, "Mittag", WorldStateKind.TimeOfDay);
        await CreateAsync(world, test.ProjectId, "Abend", WorldStateKind.TimeOfDay);

        var rows = await world.GetStatesAsync(test.ProjectId, WorldStateKind.TimeOfDay);
        Assert.Equal(["Morgen", "Mittag", "Abend"], rows.Select(row => row.Name).ToArray());

        // Verschieben tauscht mit dem Nachbarn.
        await world.MoveAsync(rows[2].Id, -1);

        rows = await world.GetStatesAsync(test.ProjectId, WorldStateKind.TimeOfDay);
        Assert.Equal(["Morgen", "Abend", "Mittag"], rows.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Am_Rand_bleibt_das_Verschieben_wirkungslos()
    {
        using var test = new TestDatabase();
        var world = test.GetService<WorldService>();

        var first = await CreateAsync(world, test.ProjectId, "Nacht", WorldStateKind.TimeOfDay);
        await CreateAsync(world, test.ProjectId, "Tag", WorldStateKind.TimeOfDay);

        await world.MoveAsync(first, -1);

        var rows = await world.GetStatesAsync(test.ProjectId, WorldStateKind.TimeOfDay);
        Assert.Equal(["Nacht", "Tag"], rows.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Die_Uebersicht_zaehlt_wie_oft_ein_Zustand_in_Bedingungen_vorkommt()
    {
        using var test = new TestDatabase();
        var world = test.GetService<WorldService>();
        var conditions = test.GetService<ConditionService>();

        var night = await CreateAsync(world, test.ProjectId, "Nacht", WorldStateKind.TimeOfDay);
        var owner = Guid.NewGuid();

        await conditions.SaveAsync(new ConditionSet
        {
            GameProjectId = test.ProjectId,
            OwnerId = owner,
            OwnerModuleKey = ModuleKeys.Npcs,
            Slot = ConditionSlots.Availability,
            Conditions =
            [
                new Condition
                {
                    Kind = ConditionKind.TimeOfDay,
                    TargetModuleKey = ModuleKeys.World,
                    TargetEntityId = night
                }
            ]
        });

        var row = Assert.Single(await world.GetStatesAsync(test.ProjectId, WorldStateKind.TimeOfDay));
        Assert.Equal(1, row.ConditionUsageCount);
    }

    [Fact]
    public async Task Weltzustaende_ueberstehen_den_ersetzenden_Import()
    {
        using var test = new TestDatabase();
        var world = test.GetService<WorldService>();

        await CreateAsync(world, test.ProjectId, "Wüste", WorldStateKind.Biome);

        using var archive = new MemoryStream();
        await test.GetService<ExportService>()
            .WriteExportAsync(test.ProjectId, ExportTarget.Json, includeAssets: false, archive);

        // Ein zweiter Zustand, den es im Archiv nicht gibt — der ersetzende Import muss ihn
        // mitnehmen, sonst wäre der Wipe für dieses Modul nicht vollständig.
        await CreateAsync(world, test.ProjectId, "Sumpf", WorldStateKind.Biome);

        archive.Position = 0;
        await test.GetService<ImportService>().ImportAsync(test.ProjectId, archive, replaceExisting: true);

        var imported = Assert.Single(await world.GetStatesAsync(test.ProjectId, WorldStateKind.Biome));
        Assert.Equal("Wüste", imported.Name);
    }

    [Fact]
    public async Task Weltzustaende_kommen_beim_Duplizieren_eines_Projekts_mit()
    {
        using var test = new TestDatabase();
        var world = test.GetService<WorldService>();

        await CreateAsync(world, test.ProjectId, "Hochgebirge", WorldStateKind.Biome);

        var copy = await test.GetService<ProjectService>()
            .DuplicateProjectAsync(test.ProjectId, "Kopie", null);

        var copied = Assert.Single(await world.GetStatesAsync(copy.Id, WorldStateKind.Biome));
        Assert.Equal("Hochgebirge", copied.Name);
    }

    [Fact]
    public async Task Ein_geloeschter_Zustand_nimmt_seine_Feldwerte_mit()
    {
        using var test = new TestDatabase();
        var world = test.GetService<WorldService>();

        var id = await CreateAsync(world, test.ProjectId, "Nebel", WorldStateKind.Weather);

        await using (var db = test.CreateContext())
        {
            db.FieldDefinitions.Add(new FieldDefinition
            {
                ModuleKey = ModuleKeys.World,
                OwnerEntityId = id,
                Name = "Sichtweite"
            });
            await db.SaveChangesAsync();
        }

        await world.DeleteStateAsync(id);

        await using var check = test.CreateContext();
        Assert.False(await check.WorldStates.AnyAsync(state => state.Id == id));
        Assert.False(await check.FieldDefinitions.AnyAsync(field => field.OwnerEntityId == id));
    }

    private static async Task<Guid> CreateAsync(
        WorldService world, Guid projectId, string name, WorldStateKind kind)
    {
        var context = await world.LoadForEditAsync(projectId, null, kind);
        context!.Entity.Name = name;
        await world.SaveStateAsync(context);

        return context.Entity.Id;
    }
}
