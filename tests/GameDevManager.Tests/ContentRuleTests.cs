using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Eigene Health-Check-Regeln (F18): Was die eingebauten Prüfungen nicht wissen können —
/// „jedes Item braucht ein Sprite“, „kein NPC ohne Art“. Regelarten statt Skriptsprache; eine
/// Handvoll deckt neunzig Prozent ab und lässt sich in einer Maske erfassen.
/// </summary>
public class ContentRuleTests
{
    private static async Task<Item> SaveItemAsync(
        TestDatabase test, string name, Guid? typeId = null, string? description = null)
    {
        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);

        context!.Entity.Name = name;
        context.Entity.ContentTypeId = typeId;
        context.Entity.Description = description;

        await items.SaveItemAsync(context);
        return context.Entity;
    }

    private static async Task<ContentRule> SaveRuleAsync(
        TestDatabase test, ContentRuleCheck check, Action<ContentRule>? adjust = null)
    {
        var rule = new ContentRule
        {
            GameProjectId = test.ProjectId,
            Name = "Regel",
            ModuleKey = ModuleKeys.Items,
            Check = check
        };

        adjust?.Invoke(rule);

        await test.GetService<ContentRuleService>().SaveRuleAsync(rule);
        return rule;
    }

    private static async Task<IReadOnlyList<ContentRuleFinding>> EvaluateAsync(TestDatabase test)
    {
        var results = await test.GetService<ContentRuleService>().EvaluateAsync(test.ProjectId);
        return Assert.Single(results).Findings;
    }

    [Fact]
    public async Task Kein_Sprite_meldet_was_kein_Icon_hat()
    {
        using var test = new TestDatabase();

        var withIcon = await SaveItemAsync(test, "Mit Icon");
        await SaveItemAsync(test, "Ohne Icon");

        await test.GetService<AssetService>().UploadAsync(
            test.ProjectId, "icon.png", "image/png", new MemoryStream([1, 2, 3]),
            ModuleKeys.Items, withIcon.Id);

        await SaveRuleAsync(test, ContentRuleCheck.NoPrimarySprite);

        Assert.Equal("Ohne Icon", Assert.Single(await EvaluateAsync(test)).EntityName);
    }

    [Fact]
    public async Task Keine_Beschreibung_meldet_leere_Texte()
    {
        using var test = new TestDatabase();

        await SaveItemAsync(test, "Beschrieben", description: "Ein Schwert.");
        await SaveItemAsync(test, "Stumm");

        // Auch Leerzeichen zählen als leer — sonst genügte ein Tastendruck, um die Regel
        // stillzustellen.
        await SaveItemAsync(test, "Fast stumm", description: "   ");

        await SaveRuleAsync(test, ContentRuleCheck.NoDescription);

        var findings = await EvaluateAsync(test);
        Assert.Equal(["Fast stumm", "Stumm"], findings.Select(f => f.EntityName).Order().ToArray());
    }

    [Fact]
    public async Task Keine_Art_meldet_was_ohne_Art_erfasst_wurde()
    {
        using var test = new TestDatabase();

        var type = new ContentType
        {
            GameProjectId = test.ProjectId, ModuleKey = ModuleKeys.Items, Name = "Waffe"
        };
        await test.GetService<ContentTypeService>().SaveTypeAsync(type);

        await SaveItemAsync(test, "Schwert", type.Id);
        await SaveItemAsync(test, "Irgendwas");

        await SaveRuleAsync(test, ContentRuleCheck.NoContentType);

        Assert.Equal("Irgendwas", Assert.Single(await EvaluateAsync(test)).EntityName);
    }

    [Fact]
    public async Task Ein_leeres_Feld_wird_gemeldet_und_die_Art_schraenkt_ein()
    {
        using var test = new TestDatabase();
        var types = test.GetService<ContentTypeService>();

        var weapon = new ContentType
        {
            GameProjectId = test.ProjectId, ModuleKey = ModuleKeys.Items, Name = "Waffe"
        };
        await types.SaveTypeAsync(weapon);

        var melee = new ContentType
        {
            GameProjectId = test.ProjectId, ModuleKey = ModuleKeys.Items, Name = "Nahkampf", ParentId = weapon.Id
        };
        await types.SaveTypeAsync(melee);

        var damage = new FieldDefinition
        {
            ModuleKey = ModuleKeys.Items,
            ContentTypeId = weapon.Id,
            Name = "Schaden",
            Type = ContentFieldType.Integer
        };
        await types.SaveFieldAsync(damage);

        var items = test.GetService<ItemService>();

        var filled = await items.LoadForEditAsync(test.ProjectId, null);
        filled!.Entity.Name = "Dolch";
        filled.Entity.ContentTypeId = melee.Id;
        filled.ValueFor(damage).NumberValue = 5;
        await items.SaveItemAsync(filled);

        await SaveItemAsync(test, "Stumpfes Schwert", melee.Id);

        // Ein Item außerhalb der Art bleibt draußen, obwohl auch dort nichts steht.
        await SaveItemAsync(test, "Trank");

        await SaveRuleAsync(test, ContentRuleCheck.FieldEmpty, rule =>
        {
            rule.ContentTypeId = weapon.Id;
            rule.FieldDefinitionId = damage.Id;
        });

        // Unterarten zählen mit: Der Dolch und das Schwert sind „Nahkampf“, geprüft ist „Waffe“.
        Assert.Equal("Stumpfes Schwert", Assert.Single(await EvaluateAsync(test)).EntityName);
    }

    [Fact]
    public async Task Kein_Bedingungssatz_meldet_den_leeren_Slot()
    {
        using var test = new TestDatabase();

        var withCondition = await SaveItemAsync(test, "Freigeschaltet");
        await SaveItemAsync(test, "Immer da");

        await test.GetService<ConditionService>().SaveAsync(new ConditionSet
        {
            GameProjectId = test.ProjectId,
            OwnerId = withCondition.Id,
            OwnerModuleKey = ModuleKeys.Items,
            Slot = ConditionSlots.Unlock,
            Conditions = [new Condition { Kind = ConditionKind.PlayerLevel, NumberValue = 5 }]
        });

        await SaveRuleAsync(test, ContentRuleCheck.NoConditions, rule => rule.Slot = ConditionSlots.Unlock);

        Assert.Equal("Immer da", Assert.Single(await EvaluateAsync(test)).EntityName);
    }

    [Fact]
    public async Task Eine_Regel_ohne_ihre_Angabe_wird_abgelehnt()
    {
        using var test = new TestDatabase();
        var rules = test.GetService<ContentRuleService>();

        // Eine Feldregel ohne Feld prüfte nichts und stünde trotzdem als Zeile da.
        await Assert.ThrowsAsync<ContentValidationException>(() => rules.SaveRuleAsync(new ContentRule
        {
            GameProjectId = test.ProjectId,
            Name = "Ohne Feld",
            ModuleKey = ModuleKeys.Items,
            Check = ContentRuleCheck.FieldEmpty
        }));

        await Assert.ThrowsAsync<ContentValidationException>(() => rules.SaveRuleAsync(new ContentRule
        {
            GameProjectId = test.ProjectId,
            Name = "Ohne Slot",
            ModuleKey = ModuleKeys.Items,
            Check = ContentRuleCheck.NoConditions
        }));
    }

    [Fact]
    public async Task Angaben_fremder_Regelarten_werden_beim_Speichern_geleert()
    {
        using var test = new TestDatabase();
        var rules = test.GetService<ContentRuleService>();

        var rule = await SaveRuleAsync(test, ContentRuleCheck.NoConditions, r => r.Slot = ConditionSlots.Unlock);

        // Umgestellt auf „kein Sprite“ — der Slot gehört nicht mehr dazu und wirkte beim
        // Zurückwechseln sonst unbemerkt weiter.
        rule.Check = ContentRuleCheck.NoPrimarySprite;
        await rules.SaveRuleAsync(rule);

        var stored = Assert.Single(await rules.GetRulesAsync(test.ProjectId));
        Assert.Null(stored.Slot);
    }

    [Fact]
    public async Task Eine_abgeschaltete_Regel_prueft_nicht()
    {
        using var test = new TestDatabase();
        await SaveItemAsync(test, "Ohne Icon");

        await SaveRuleAsync(test, ContentRuleCheck.NoPrimarySprite, rule => rule.IsEnabled = false);

        Assert.Empty(await test.GetService<ContentRuleService>().EvaluateAsync(test.ProjectId));
    }

    [Fact]
    public async Task Eine_Regel_ohne_Fund_bleibt_in_der_Liste()
    {
        using var test = new TestDatabase();

        var item = await SaveItemAsync(test, "Mit Icon");
        await test.GetService<AssetService>().UploadAsync(
            test.ProjectId, "icon.png", "image/png", new MemoryStream([1, 2, 3]),
            ModuleKeys.Items, item.Id);

        await SaveRuleAsync(test, ContentRuleCheck.NoPrimarySprite);

        // Ohne diese Zeile wäre nicht erkennbar, dass geprüft wurde — dieselbe Linie wie bei
        // den eingebauten Health Checks.
        var result = Assert.Single(await test.GetService<ContentRuleService>().EvaluateAsync(test.ProjectId));
        Assert.Empty(result.Findings);
    }
}
