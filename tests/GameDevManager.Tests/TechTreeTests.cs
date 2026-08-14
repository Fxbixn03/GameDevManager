using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Der Freischaltungs-Graph. Er hat keine eigenen Daten — geprüft wird deshalb vor allem, dass
/// er aus dem Bedingungssystem das Richtige herausliest: was worauf wartet, in welcher Tiefe
/// es liegt, und wann ein Ring vorliegt.
/// </summary>
public class TechTreeTests
{
    [Fact]
    public async Task Eine_Freischalt_Bedingung_wird_zur_Kante_vom_Ziel_zum_Besitzer()
    {
        using var test = new TestDatabase();

        var basic = await CreateItemAsync(test, "Holzschwert");
        var advanced = await CreateItemAsync(test, "Eisenschwert");

        await RequireAsync(test, owner: advanced, requirement: basic);

        var graph = await test.GetService<TechTreeService>().GetGraphAsync(test.ProjectId);

        Assert.Equal(2, graph.Nodes.Count);
        var edge = Assert.Single(graph.Edges);
        Assert.Equal(basic, edge.FromEntityId);
        Assert.Equal(advanced, edge.ToEntityId);

        // Ohne Voraussetzung ganz links, das Freigeschaltete eine Spalte weiter.
        Assert.Equal(0, graph.Nodes.First(node => node.EntityId == basic).Depth);
        Assert.Equal(1, graph.Nodes.First(node => node.EntityId == advanced).Depth);
        Assert.True(graph.Nodes.First(node => node.EntityId == basic).IsRoot);
    }

    [Fact]
    public async Task Die_Tiefe_ist_der_laengste_Weg_und_nicht_der_kuerzeste()
    {
        using var test = new TestDatabase();

        var a = await CreateItemAsync(test, "A");
        var b = await CreateItemAsync(test, "B");
        var c = await CreateItemAsync(test, "C");

        // C braucht A (direkt) und B (das seinerseits A braucht). C gehört hinter B.
        await RequireAsync(test, owner: b, requirement: a);
        await RequireAsync(test, owner: c, requirement: a, second: b);

        var graph = await test.GetService<TechTreeService>().GetGraphAsync(test.ProjectId);

        Assert.Equal(0, graph.Nodes.First(node => node.EntityId == a).Depth);
        Assert.Equal(1, graph.Nodes.First(node => node.EntityId == b).Depth);
        Assert.Equal(2, graph.Nodes.First(node => node.EntityId == c).Depth);
        Assert.Equal(2, graph.MaxDepth);
    }

    [Fact]
    public async Task Eine_Sperre_ist_keine_Voraussetzung()
    {
        using var test = new TestDatabase();

        var owner = await CreateItemAsync(test, "Friedensvertrag");
        var blocker = await CreateItemAsync(test, "Kriegserklärung");

        // „darf nicht freigeschaltet sein“ — als Kante gelesen zeigte der Baum das Gegenteil.
        await SaveConditionsAsync(test, owner, ConditionSlots.Unlock, ConditionLogic.All,
        [
            new Condition
            {
                Kind = ConditionKind.Unlocked,
                TargetModuleKey = ModuleKeys.Items,
                TargetEntityId = blocker,
                BooleanValue = false
            }
        ]);

        var graph = await test.GetService<TechTreeService>().GetGraphAsync(test.ProjectId);

        Assert.Empty(graph.Edges);
    }

    [Fact]
    public async Task Bedingungen_ohne_Zielentitaet_bleiben_draussen()
    {
        using var test = new TestDatabase();

        var owner = await CreateItemAsync(test, "Meisterschwert");

        // „Spieler hat Stufe 20“ ist eine Voraussetzung, aber kein Knoten im Baum.
        await SaveConditionsAsync(test, owner, ConditionSlots.Unlock, ConditionLogic.All,
        [
            new Condition { Kind = ConditionKind.PlayerLevel, NumberValue = 20 }
        ]);

        var graph = await test.GetService<TechTreeService>().GetGraphAsync(test.ProjectId);

        Assert.Empty(graph.Edges);
        Assert.Single(graph.Nodes);
    }

    [Fact]
    public async Task Ein_Ring_wird_gemeldet_statt_endlos_gerechnet()
    {
        using var test = new TestDatabase();

        var a = await CreateItemAsync(test, "Zirkel A");
        var b = await CreateItemAsync(test, "Zirkel B");

        await RequireAsync(test, owner: a, requirement: b);
        await RequireAsync(test, owner: b, requirement: a);

        var service = test.GetService<TechTreeService>();
        var graph = await service.GetGraphAsync(test.ProjectId);

        var cycle = Assert.Single(graph.Cycles);
        Assert.Equal(2, cycle.Nodes.Count);
        Assert.All(graph.Nodes, node => Assert.True(node.IsInCycle));

        // Derselbe Ring wird nur einmal gemeldet, egal von welchem Knoten aus er auffällt.
        Assert.Single(await service.FindCyclesAsync(test.ProjectId));
        Assert.Contains("→", service.DescribeCycle(cycle));
    }

    [Fact]
    public async Task Ein_ODER_Satz_markiert_seine_Kanten_als_einen_von_mehreren_Wegen()
    {
        using var test = new TestDatabase();

        var goal = await CreateItemAsync(test, "Ziel");
        var wayA = await CreateItemAsync(test, "Weg A");
        var wayB = await CreateItemAsync(test, "Weg B");

        await SaveConditionsAsync(test, goal, ConditionSlots.Unlock, ConditionLogic.Any,
        [
            new Condition { Kind = ConditionKind.Unlocked, TargetModuleKey = ModuleKeys.Items, TargetEntityId = wayA },
            new Condition { Kind = ConditionKind.Unlocked, TargetModuleKey = ModuleKeys.Items, TargetEntityId = wayB }
        ]);

        var graph = await test.GetService<TechTreeService>().GetGraphAsync(test.ProjectId);

        Assert.Equal(2, graph.Edges.Count);
        Assert.All(graph.Edges, edge => Assert.True(edge.IsOptional));
    }

    [Fact]
    public async Task Ein_geloeschtes_Ziel_faellt_samt_seiner_Kante_heraus()
    {
        using var test = new TestDatabase();

        var owner = await CreateItemAsync(test, "Belohnung");

        await SaveConditionsAsync(test, owner, ConditionSlots.Unlock, ConditionLogic.All,
        [
            new Condition
            {
                Kind = ConditionKind.Unlocked,
                TargetModuleKey = ModuleKeys.Items,
                TargetEntityId = Guid.NewGuid()
            }
        ]);

        var graph = await test.GetService<TechTreeService>().GetGraphAsync(test.ProjectId);

        // Dass das Ziel fehlt, meldet der Health Check „unerfüllbare Bedingungen“ — der Baum
        // zeigt dafür keine leere Kachel.
        Assert.Empty(graph.Edges);
        Assert.Single(graph.Nodes);
    }

    [Fact]
    public async Task Ohne_Freischaltungen_bleibt_der_Baum_leer()
    {
        using var test = new TestDatabase();

        var graph = await test.GetService<TechTreeService>().GetGraphAsync(test.ProjectId);

        Assert.Empty(graph.Nodes);
        Assert.Empty(graph.Cycles);
    }

    // ------------------------------------------------------------------------------ Hilfen

    private static async Task<Guid> CreateItemAsync(TestDatabase test, string name)
    {
        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);

        context!.Entity.Name = name;
        await items.SaveItemAsync(context);

        return context.Entity.Id;
    }

    private static Task RequireAsync(TestDatabase test, Guid owner, Guid requirement, Guid? second = null)
    {
        List<Condition> conditions =
        [
            new()
            {
                Kind = ConditionKind.Unlocked,
                TargetModuleKey = ModuleKeys.Items,
                TargetEntityId = requirement
            }
        ];

        if (second is { } other)
        {
            conditions.Add(new Condition
            {
                Kind = ConditionKind.Unlocked,
                TargetModuleKey = ModuleKeys.Items,
                TargetEntityId = other
            });
        }

        return SaveConditionsAsync(test, owner, ConditionSlots.Unlock, ConditionLogic.All, conditions);
    }

    private static Task SaveConditionsAsync(
        TestDatabase test, Guid owner, string slot, ConditionLogic logic, List<Condition> conditions) =>
        test.GetService<ConditionService>().SaveAsync(new ConditionSet
        {
            GameProjectId = test.ProjectId,
            OwnerId = owner,
            OwnerModuleKey = ModuleKeys.Items,
            Slot = slot,
            Logic = logic,
            Conditions = conditions
        });
}
