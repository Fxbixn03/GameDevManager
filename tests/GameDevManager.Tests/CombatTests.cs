using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Der Kampf-Simulator: die reine Rechnung (wiederholbar über den Startwert) und die
/// Feld-Zuordnung samt Werte-Auflösung inklusive Kurvenfeldern.
/// </summary>
public class CombatTests
{
    private static CombatantStats Fighter(
        string name, double health, double damage, double defense = 0, double speed = 0) =>
        new(name, health, damage, defense, speed);

    // ---------------------------------------------------------------------- Simulation

    [Fact]
    public void Derselbe_Startwert_ergibt_denselben_Lauf()
    {
        var a = Fighter("Ritter", 100, 12, 2, 3);
        var b = Fighter("Golem", 140, 9, 5, 1);

        var first = CombatSimulation.Run(a, b, seed: 7);
        var second = CombatSimulation.Run(a, b, seed: 7);

        Assert.Equal(first, second);

        // Ein anderer Startwert darf (und wird bei 1000 Kämpfen) anders ausgehen.
        Assert.NotEqual(first, CombatSimulation.Run(a, b, seed: 8));
    }

    [Fact]
    public void Der_deutlich_Staerkere_gewinnt_fast_immer()
    {
        var boss = Fighter("Boss", 300, 30, 5, 5);
        var ratte = Fighter("Ratte", 20, 3);

        var result = CombatSimulation.Run(boss, ratte, seed: 1);

        Assert.True(result.WinsA > result.Fights * 0.95);
        Assert.True(result.MedianRounds >= 1);
        Assert.True(result.MinRounds <= result.MaxRounds);
    }

    [Fact]
    public void Zwei_Unverwundbare_enden_im_Patt_statt_endlos_zu_laufen()
    {
        // Schaden 1 (Mindestschaden) gegen riesige Leben — die Rundenbremse greift.
        var turtleA = Fighter("Schildkröte A", 1_000_000, 1, 100);
        var turtleB = Fighter("Schildkröte B", 1_000_000, 1, 100);

        var result = CombatSimulation.Run(turtleA, turtleB, seed: 3, fights: 10);

        Assert.Equal(10, result.Draws);
        Assert.Equal(0, result.WinsA + result.WinsB);
    }

    [Fact]
    public void Die_Trefferchance_bleibt_zwischen_5_und_95_Prozent()
    {
        Assert.Equal(0.95, CombatSimulation.HitChance(Fighter("A", 1, 1, speed: 99), Fighter("B", 1, 1)));
        Assert.Equal(0.05, CombatSimulation.HitChance(Fighter("A", 1, 1), Fighter("B", 1, 1, speed: 99)));
        Assert.Equal(0.75, CombatSimulation.HitChance(Fighter("A", 1, 1), Fighter("B", 1, 1)));
    }

    // ------------------------------------------------------------------ Feld-Zuordnung

    [Fact]
    public async Task Die_Zuordnung_wird_je_Projekt_gespeichert_und_wieder_geladen()
    {
        using var test = new TestDatabase();
        var combat = test.GetService<CombatService>();

        // Ohne gespeicherte Zeile kommt eine leere Zuordnung — keine Ausnahme.
        var empty = await combat.GetMappingAsync(test.ProjectId);
        Assert.Null(empty.HealthFieldId);

        var healthField = Guid.NewGuid();
        empty.HealthFieldId = healthField;
        await combat.SaveMappingAsync(empty);

        Assert.Equal(healthField, (await combat.GetMappingAsync(test.ProjectId)).HealthFieldId);

        // Das zweite Speichern ändert dieselbe Zeile, statt eine zweite anzulegen.
        empty.HealthFieldId = null;
        await combat.SaveMappingAsync(empty);

        await using var db = test.CreateContext();
        Assert.Null(Assert.Single(db.CombatMappings.ToList()).HealthFieldId);
    }

    [Fact]
    public async Task Werte_kommen_aus_Zahlenfeldern_und_Kurven_auf_der_gewaehlten_Stufe()
    {
        using var test = new TestDatabase();

        Guid npcId, healthFieldId, damageFieldId;
        await using (var db = test.CreateContext())
        {
            var type = new ContentType { GameProjectId = test.ProjectId, ModuleKey = ModuleKeys.Npcs, Name = "Mob" };
            var health = new FieldDefinition
            {
                ContentTypeId = type.Id,
                ModuleKey = ModuleKeys.Npcs,
                Name = "Leben",
                Type = ContentFieldType.Curve
            };
            var damage = new FieldDefinition
            {
                ContentTypeId = type.Id,
                ModuleKey = ModuleKeys.Npcs,
                Name = "Schaden",
                Type = ContentFieldType.Integer
            };

            var npc = new Npc { GameProjectId = test.ProjectId, ContentTypeId = type.Id, Name = "Wolf" };

            db.ContentTypes.Add(type);
            db.FieldDefinitions.AddRange(health, damage);
            db.Npcs.Add(npc);
            db.FieldValues.AddRange(
                new FieldValue
                {
                    OwnerEntityId = npc.Id,
                    OwnerModuleKey = ModuleKeys.Npcs,
                    FieldDefinitionId = health.Id,
                    TextValue = """{"expression":"10 * x","from":1,"to":60}"""
                },
                new FieldValue
                {
                    OwnerEntityId = npc.Id,
                    OwnerModuleKey = ModuleKeys.Npcs,
                    FieldDefinitionId = damage.Id,
                    NumberValue = 7
                });

            await db.SaveChangesAsync();
            (npcId, healthFieldId, damageFieldId) = (npc.Id, health.Id, damage.Id);
        }

        var mapping = new CombatMapping
        {
            GameProjectId = test.ProjectId,
            HealthFieldId = healthFieldId,
            DamageFieldId = damageFieldId
        };

        var stats = await test.GetService<CombatService>()
            .ResolveStatsAsync(test.ProjectId, npcId, mapping, level: 5);

        Assert.NotNull(stats);
        Assert.Equal("Wolf", stats.Name);
        Assert.Equal(50, stats.Health);   // Kurve 10 * x auf Stufe 5
        Assert.Equal(7, stats.Damage);    // Zahlenfeld
        Assert.Equal(0, stats.Defense);   // nicht zugeordnet → 0
    }
}
