using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Der Auswertungskern des Bedingungssystems: ein angenommener Spielzustand gegen einen Satz.
/// Reine Rechenlogik ohne Datenbank — genau deshalb liegt sie als eigene Klasse in der
/// Datenschicht, die Durchspiel-Ansicht und später die Zustands-Sicht leben davon.
/// </summary>
public class ConditionEvaluatorTests
{
    private static ConditionSet Set(ConditionLogic logic, params Condition[] conditions) =>
        new()
        {
            OwnerId = Guid.NewGuid(),
            OwnerModuleKey = ModuleKeys.Dialogs,
            Slot = ConditionSlots.Availability,
            Logic = logic,
            Conditions = [.. conditions]
        };

    [Fact]
    public void Ein_fehlender_oder_leerer_Satz_verbietet_nichts()
    {
        Assert.True(ConditionEvaluator.Evaluate((ConditionSet?)null, new GameStateAssumption()).Satisfied);
        Assert.True(ConditionEvaluator.Evaluate(Set(ConditionLogic.All), new GameStateAssumption()).Satisfied);
    }

    [Fact]
    public void Mengen_werden_ueber_den_Operator_verglichen()
    {
        var itemId = Guid.NewGuid();
        var state = new GameStateAssumption { Items = new Dictionary<Guid, double> { [itemId] = 3 } };

        ConditionResult Eval(ComparisonOperator op, double wanted) =>
            ConditionEvaluator.Evaluate(new Condition
            {
                Kind = ConditionKind.HasItem,
                TargetEntityId = itemId,
                Operator = op,
                NumberValue = wanted
            }, state);

        Assert.True(Eval(ComparisonOperator.AtLeast, 3).Satisfied);
        Assert.False(Eval(ComparisonOperator.GreaterThan, 3).Satisfied);
        Assert.True(Eval(ComparisonOperator.Equal, 3).Satisfied);
        Assert.True(Eval(ComparisonOperator.AtMost, 3).Satisfied);
        Assert.False(Eval(ComparisonOperator.LessThan, 3).Satisfied);
        Assert.True(Eval(ComparisonOperator.NotEqual, 5).Satisfied);

        // Was nicht im Beutel liegt, ist 0 — keine Ausnahme.
        Assert.False(ConditionEvaluator.Evaluate(new Condition
        {
            Kind = ConditionKind.HasItem,
            TargetEntityId = Guid.NewGuid()
        }, state).Satisfied);
    }

    [Fact]
    public void Schalter_gelten_auch_als_ausdruecklich_nicht()
    {
        var state = new GameStateAssumption
        {
            Flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Nacht" }
        };

        Assert.True(ConditionEvaluator.Evaluate(new Condition
        {
            Kind = ConditionKind.Flag, TextValue = "nacht", BooleanValue = true
        }, state).Satisfied);

        // „ausdrücklich nicht gesetzt“ — die ebenso häufige Richtung.
        Assert.False(ConditionEvaluator.Evaluate(new Condition
        {
            Kind = ConditionKind.Flag, TextValue = "nacht", BooleanValue = false
        }, state).Satisfied);
    }

    [Fact]
    public void Weltzustaende_und_Freischaltungen_pruefen_die_GUID()
    {
        var night = Guid.NewGuid();
        var skill = Guid.NewGuid();
        var state = new GameStateAssumption
        {
            ActiveWorldStates = new HashSet<Guid> { night },
            Unlocked = new HashSet<Guid> { skill }
        };

        Assert.True(ConditionEvaluator.Evaluate(new Condition
        {
            Kind = ConditionKind.TimeOfDay, TargetEntityId = night, BooleanValue = true
        }, state).Satisfied);

        Assert.False(ConditionEvaluator.Evaluate(new Condition
        {
            Kind = ConditionKind.Weather, TargetEntityId = Guid.NewGuid(), BooleanValue = true
        }, state).Satisfied);

        Assert.True(ConditionEvaluator.Evaluate(new Condition
        {
            Kind = ConditionKind.Unlocked, TargetEntityId = skill, BooleanValue = true
        }, state).Satisfied);
    }

    [Fact]
    public void Queststand_vergleicht_den_Text_ohne_Gross_und_Kleinschreibung()
    {
        var questId = Guid.NewGuid();
        var state = new GameStateAssumption
        {
            QuestStates = new Dictionary<Guid, string> { [questId] = "Abgeschlossen" }
        };

        Assert.True(ConditionEvaluator.Evaluate(new Condition
        {
            Kind = ConditionKind.QuestState, TargetEntityId = questId, TextValue = " abgeschlossen "
        }, state).Satisfied);

        Assert.False(ConditionEvaluator.Evaluate(new Condition
        {
            Kind = ConditionKind.QuestState, TargetEntityId = questId, TextValue = "offen"
        }, state).Satisfied);
    }

    [Fact]
    public void Nicht_Rechenbares_gilt_als_erfuellte_Annahme()
    {
        var state = new GameStateAssumption();

        // Frei Beschriebenes kennt nur der Mensch.
        var custom = ConditionEvaluator.Evaluate(
            new Condition { Kind = ConditionKind.Custom, TextValue = "Nur sonntags" }, state);
        Assert.True(custom.Satisfied);
        Assert.True(custom.IsAssumption);

        // Ein zielbezogener Satz ohne Ziel ist nicht zu rechnen — Annahme statt Nein.
        var targetless = ConditionEvaluator.Evaluate(
            new Condition { Kind = ConditionKind.HasItem }, state);
        Assert.True(targetless.Satisfied);
        Assert.True(targetless.IsAssumption);
    }

    [Fact]
    public async Task Die_Durchspiel_Daten_tragen_die_Bedingungen_je_Zeile_und_Antwort()
    {
        using var test = new TestDatabase();
        var dialogues = test.GetService<DialogueService>();

        var context = await dialogues.LoadForEditAsync(test.ProjectId, null);
        var dialogue = context!.Entity;
        dialogue.Name = "Torwache";
        dialogue.Kind = DialogueKind.Conversation;

        var first = new DialogueLine { DialogueId = dialogue.Id, Text = "Halt!", SortOrder = 0 };
        var second = new DialogueLine { DialogueId = dialogue.Id, Text = "Na gut.", SortOrder = 1 };
        var choice = new DialogueChoice
        {
            DialogueLineId = first.Id,
            Text = "Ich habe den Schlüssel.",
            NextLineId = second.Id,
            SortOrder = 0
        };
        first.Choices.Add(choice);
        dialogue.Lines.AddRange([first, second]);

        await dialogues.SaveDialogueAsync(context);

        await test.GetService<ConditionService>().SaveAsync(new ConditionSet
        {
            GameProjectId = test.ProjectId,
            OwnerId = choice.Id,
            OwnerModuleKey = ModuleKeys.Dialogs,
            Slot = ConditionSlots.Availability,
            Logic = ConditionLogic.All,
            Conditions = [new Condition { Kind = ConditionKind.Flag, TextValue = "schluessel", BooleanValue = true }]
        });

        var data = await dialogues.GetPlayDataAsync(test.ProjectId, dialogue.Id);

        Assert.NotNull(data);
        Assert.Equal(2, data!.Dialogue.Lines.Count);

        var set = data.AvailabilityByOwner[choice.Id];
        var state = new GameStateAssumption
        {
            Flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Schluessel" }
        };

        // Genau die Frage der Durchspiel-Ansicht: Ist die Antwort in diesem Zustand offen?
        Assert.True(ConditionEvaluator.Evaluate(set, state).Satisfied);
        Assert.False(ConditionEvaluator.Evaluate(set, new GameStateAssumption()).Satisfied);
    }

    [Fact]
    public void Alle_und_mindestens_eine_rechnen_verschieden()
    {
        var state = new GameStateAssumption { PlayerLevel = 10 };

        Condition Level(double wanted) => new()
        {
            Kind = ConditionKind.PlayerLevel,
            Operator = ComparisonOperator.AtLeast,
            NumberValue = wanted
        };

        Assert.False(ConditionEvaluator.Evaluate(Set(ConditionLogic.All, Level(5), Level(20)), state).Satisfied);
        Assert.True(ConditionEvaluator.Evaluate(Set(ConditionLogic.Any, Level(5), Level(20)), state).Satisfied);

        // Und die Anzeige weiß, woran es lag.
        var failed = Assert.Single(
            ConditionEvaluator.Evaluate(Set(ConditionLogic.All, Level(5), Level(20)), state).Failed);
        Assert.Equal(20, failed.Condition.NumberValue);
    }
}
