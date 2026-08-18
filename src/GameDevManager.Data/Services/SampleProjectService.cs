using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Legt ein gefülltes Beispielprojekt an: zwei Item-Arten mit Feldvererbung, ein Rezeptbaum
/// über drei Stufen, ein Händler mit bedingtem Angebot, ein Dialog mit Verzweigung und eine
/// Karte mit Gebiet und Spawn-Regel.
/// <para>
/// Bewusst <b>kein</b> mitgeliefertes Export-ZIP, obwohl der Import eines lesen könnte: Ein
/// ZIP im Anwendungsverzeichnis veraltete bei jeder Erhöhung der <c>FormatVersion</c> still.
/// Der Seeder geht stattdessen durch die echten Modul-Dienste — damit gelten Validierung,
/// Schreibschutz und Feldmechanik von selbst, und was hier entsteht, ist zu jedem Stand des
/// Codes gültig.
/// </para>
/// <para>
/// Die Namen der Inhalte kommen aus <see cref="DataMessages"/> — dieselbe Regel wie bei den
/// Standard-Spalten eines Kanban-Boards: Was der Dienst anlegt und der Nutzer sieht, steht
/// nicht im Code.
/// </para>
/// </summary>
public class SampleProjectService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ProjectService projects,
    ContentTypeService contentTypes,
    CurrencyService currencies,
    ItemService items,
    CraftingService crafting,
    NpcService npcs,
    DialogueService dialogues,
    MapService maps,
    ConditionService conditions,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<GameProject> CreateAsync(CancellationToken ct = default)
    {
        // Projektnamen sind installationsweit eindeutig — beim zweiten Beispiel weicht der
        // Name aus, statt dass der Knopf mit einer Fehlermeldung endet.
        var taken = (await projects.GetProjectsAsync(ct: ct))
            .Select(project => project.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var baseName = messages["SampleProjectName"].Value;
        var name = baseName;

        for (var counter = 2; taken.Contains(name); counter++)
        {
            name = $"{baseName} {counter}";
        }

        var project = new GameProject { Name = name, Description = messages["SampleProjectDescription"] };
        await projects.SaveProjectAsync(project, ct);

        try
        {
            await SeedAsync(project.Id, ct);
        }
        catch
        {
            // Ein halbes Beispiel wäre schlimmer als keines — dieselbe Linie wie beim
            // Duplizieren: Scheitert etwas, wird das Gerüst wieder abgeräumt.
            await using var db = await factory.CreateDbContextAsync(CancellationToken.None);
            await ImportService.WipeProjectAsync(db, project.Id, CancellationToken.None);
            await db.GameProjects.Where(p => p.Id == project.Id).ExecuteDeleteAsync(CancellationToken.None);
            throw;
        }

        return project;
    }

    private async Task SeedAsync(Guid projectId, CancellationToken ct)
    {
        // ------------------------------------------------------------------ Währung
        var gold = await currencies.LoadForEditAsync(projectId, null, ct);
        gold!.Entity.Name = messages["SampleCurrencyGold"];
        gold.Entity.Symbol = "G";
        await currencies.SaveCurrencyAsync(gold, ct);

        // ------------------------------------------ Arten mit Feldvererbung (F. des Konzepts)
        var weapon = new ContentType
        {
            GameProjectId = projectId,
            ModuleKey = ModuleKeys.Items,
            Name = messages["SampleTypeWeapon"]
        };
        await contentTypes.SaveTypeAsync(weapon, ct);

        await contentTypes.SaveFieldAsync(new FieldDefinition
        {
            ContentTypeId = weapon.Id,
            ModuleKey = ModuleKeys.Items,
            Name = messages["SampleFieldDamage"],
            Type = ContentFieldType.Integer,
            MinValue = 0,
            SortOrder = 0
        }, ct);

        await contentTypes.SaveFieldAsync(new FieldDefinition
        {
            ContentTypeId = weapon.Id,
            ModuleKey = ModuleKeys.Items,
            Name = messages["SampleFieldValue"],
            Type = ContentFieldType.Integer,
            Unit = "G",
            SortOrder = 1
        }, ct);

        var melee = new ContentType
        {
            GameProjectId = projectId,
            ModuleKey = ModuleKeys.Items,
            ParentId = weapon.Id,
            Name = messages["SampleTypeMelee"]
        };
        await contentTypes.SaveTypeAsync(melee, ct);

        await contentTypes.SaveFieldAsync(new FieldDefinition
        {
            ContentTypeId = melee.Id,
            ModuleKey = ModuleKeys.Items,
            Name = messages["SampleFieldRange"],
            Type = ContentFieldType.Decimal,
            Unit = "m",
            SortOrder = 0
        }, ct);

        // -------------------------------------------------------------------- Items
        var ore = await CreateItemAsync(projectId, messages["SampleItemOre"], ct);
        var wood = await CreateItemAsync(projectId, messages["SampleItemWood"], ct);
        var ingot = await CreateItemAsync(projectId, messages["SampleItemIngot"], ct);
        var hilt = await CreateItemAsync(projectId, messages["SampleItemHilt"], ct);

        // Das Schwert trägt die Art samt geerbtem und eigenem Feld — der Beleg, dass die
        // Vererbung in der Maske ankommt.
        var sword = await items.LoadForEditAsync(projectId, null, ct);
        sword!.Entity.Name = messages["SampleItemSword"];
        sword.Entity.ContentTypeId = melee.Id;
        SetNumber(sword, messages["SampleFieldDamage"], 12);
        SetNumber(sword, messages["SampleFieldValue"], 80);
        SetNumber(sword, messages["SampleFieldRange"], 1.5);
        await items.SaveItemAsync(sword, ct);

        // ------------------------------------------------- Rezeptbaum über drei Stufen
        await CreateRecipeAsync(projectId, [(ore, 2)], [(ingot, 1)], ct);
        await CreateRecipeAsync(projectId, [(wood, 1)], [(hilt, 2)], ct);
        await CreateRecipeAsync(projectId, [(ingot, 1), (hilt, 1)], [(sword.Entity.Id, 1)], ct);

        // ------------------------------------------------ Händler mit bedingtem Angebot
        var smith = await npcs.LoadForEditAsync(projectId, null, ct);
        smith!.Entity.Name = messages["SampleNpcSmith"];
        smith.Entity.IsTrader = true;

        // Der Schmied kauft keine Schwerter an (BuyPrice leer): Mit Ankaufspreis am Ergebnis
        // und unbepreisten Zutaten meldete die Wirtschafts-Prüfung zu Recht eine vermutete
        // Gelddruckmaschine — ein Beispiel, das mit Funden startet, lehrte das Falsche.
        var swordOffer = new TraderOffer
        {
            NpcId = smith.Entity.Id,
            ItemId = sword.Entity.Id,
            CurrencyId = gold.Entity.Id,
            SellPrice = 120,
            SortOrder = 0
        };
        smith.Entity.Offers.Add(swordOffer);
        smith.Entity.Offers.Add(new TraderOffer
        {
            NpcId = smith.Entity.Id,
            ItemId = ore,
            CurrencyId = gold.Entity.Id,
            SellPrice = 3,
            BuyPrice = 2,
            Stock = 20,
            RestockSeconds = 600,
            SortOrder = 1
        });
        // Auch Holz hat eine Bezugsquelle — sonst stünde es als „toter Content“ im Zustand.
        smith.Entity.Offers.Add(new TraderOffer
        {
            NpcId = smith.Entity.Id,
            ItemId = wood,
            CurrencyId = gold.Entity.Id,
            SellPrice = 2,
            BuyPrice = 1,
            SortOrder = 2
        });
        await npcs.SaveNpcAsync(smith, ct);

        // Das Schwert gibt es erst ab Stufe 5 — die Bedingung hängt am einzelnen Posten,
        // genau der Fall „teilweise auch nur Items aus einem Shop“ aus dem Konzept.
        await conditions.SaveAsync(new ConditionSet
        {
            GameProjectId = projectId,
            OwnerId = swordOffer.Id,
            OwnerModuleKey = ModuleKeys.Npcs,
            Slot = ConditionSlots.Shop,
            Logic = ConditionLogic.All,
            Conditions =
            [
                new Condition
                {
                    Kind = ConditionKind.PlayerLevel,
                    Operator = ComparisonOperator.AtLeast,
                    NumberValue = 5
                }
            ]
        }, ct);

        // ---------------------------------------------------- Karte mit Gebiet und Marker
        var map = await maps.LoadForEditAsync(projectId, null, ct);
        map!.Entity.Name = messages["SampleMapName"];
        map.Entity.Markers.Add(new MapMarker
        {
            MapId = map.Entity.Id,
            X = 0.35,
            Y = 0.45,
            Label = messages["SampleMarkerSmithy"],
            TargetModuleKey = ModuleKeys.Npcs,
            TargetEntityId = smith.Entity.Id,
            SortOrder = 0
        });
        map.Entity.Markers.Add(new MapMarker
        {
            MapId = map.Entity.Id,
            X = 0.68,
            Y = 0.3,
            Label = messages["SampleMarkerDistrict"],
            Points = MapMarker.FormatPoints(
                [new MapPoint(0.55, 0.15), new MapPoint(0.85, 0.2), new MapPoint(0.8, 0.45), new MapPoint(0.6, 0.4)]),
            SortOrder = 1
        });
        await maps.SaveMapAsync(map, ct);

        // ------------------------------------------------------- Mob mit Spawn-Regel
        var wolf = await npcs.LoadForEditAsync(projectId, null, ct);
        wolf!.Entity.Name = messages["SampleNpcWolf"];
        wolf.Entity.Kind = NpcKind.Mob;
        wolf.Entity.SpawnRules.Add(new SpawnRule
        {
            NpcId = wolf.Entity.Id,
            TargetMapId = map.Entity.Id,
            MinCount = 2,
            MaxCount = 4,
            RespawnSeconds = 300,
            SortOrder = 0
        });
        await npcs.SaveNpcAsync(wolf, ct);

        // -------------------------------------------------- Dialog mit Verzweigung
        var dialogue = await dialogues.LoadForEditAsync(projectId, null, ct);
        var conversation = dialogue!.Entity;
        conversation.Name = messages["SampleDialogueName"];
        conversation.Kind = DialogueKind.Conversation;
        conversation.IncludesPlayer = true;
        conversation.Participants.Add(new DialogueParticipant
        {
            DialogueId = conversation.Id,
            NpcId = smith.Entity.Id,
            SortOrder = 0
        });

        var welcome = new DialogueLine
        {
            DialogueId = conversation.Id,
            SpeakerNpcId = smith.Entity.Id,
            Text = messages["SampleDialogueLineWelcome"],
            SortOrder = 0
        };
        var wares = new DialogueLine
        {
            DialogueId = conversation.Id,
            SpeakerNpcId = smith.Entity.Id,
            Text = messages["SampleDialogueLineWares"],
            SortOrder = 1
        };
        var farewell = new DialogueLine
        {
            DialogueId = conversation.Id,
            SpeakerNpcId = smith.Entity.Id,
            Text = messages["SampleDialogueLineFarewell"],
            SortOrder = 2
        };

        welcome.Choices.Add(new DialogueChoice
        {
            DialogueLineId = welcome.Id,
            Text = messages["SampleDialogueChoiceWares"],
            NextLineId = wares.Id,
            SortOrder = 0
        });
        welcome.Choices.Add(new DialogueChoice
        {
            DialogueLineId = welcome.Id,
            Text = messages["SampleDialogueChoicePassing"],
            NextLineId = farewell.Id,
            SortOrder = 1
        });

        conversation.Lines.AddRange([welcome, wares, farewell]);
        await dialogues.SaveDialogueAsync(dialogue, ct);
    }

    private async Task<Guid> CreateItemAsync(Guid projectId, string name, CancellationToken ct)
    {
        var context = await items.LoadForEditAsync(projectId, null, ct);
        context!.Entity.Name = name;
        await items.SaveItemAsync(context, ct);

        return context.Entity.Id;
    }

    private async Task CreateRecipeAsync(
        Guid projectId,
        (Guid ItemId, int Quantity)[] ingredients,
        (Guid ItemId, int Quantity)[] outputs,
        CancellationToken ct)
    {
        var context = await crafting.LoadForEditAsync(projectId, null, ct);
        var recipe = context!.Entity;

        for (var index = 0; index < ingredients.Length; index++)
        {
            recipe.Ingredients.Add(new RecipeIngredient
            {
                RecipeId = recipe.Id,
                ItemId = ingredients[index].ItemId,
                Quantity = ingredients[index].Quantity,
                SortOrder = index
            });
        }

        for (var index = 0; index < outputs.Length; index++)
        {
            recipe.Outputs.Add(new RecipeOutput
            {
                RecipeId = recipe.Id,
                ItemId = outputs[index].ItemId,
                Quantity = outputs[index].Quantity,
                SortOrder = index
            });
        }

        // Den Namen bildet der Dienst aus den Ziel-Items — wie bei jedem Rezept.
        await crafting.SaveRecipeAsync(context, ct);
    }

    /// <summary>Setzt einen Zahlenwert über den Feldnamen — geerbte Felder eingeschlossen.</summary>
    private static void SetNumber(ContentEditContext<Item> context, string fieldName, double value)
    {
        var field = context.ApplicableFields
            .First(f => string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));

        context.ValueFor(field).NumberValue = value;
    }
}
