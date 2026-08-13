using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Der Dialog-Graph: Tiefe im Verlauf, Kanten aus den Antworten und die Zeilen, die von der
/// ersten aus nie erreicht werden.
/// </summary>
public class DialogueGraphTests
{
    [Fact]
    public async Task Tiefe_zaehlt_ab_der_Einstiegszeile_und_markiert_Unerreichbares()
    {
        using var test = new TestDatabase();

        Guid firstId;
        Guid secondId;
        Guid orphanId;
        Guid dialogueId;

        await using (var db = test.CreateContext())
        {
            var dialogue = new Dialogue
            {
                GameProjectId = test.ProjectId,
                Name = "Torwache",
                Kind = DialogueKind.Conversation,
                IncludesPlayer = true
            };

            var first = new DialogueLine { DialogueId = dialogue.Id, Text = "Halt!", SortOrder = 0 };
            var second = new DialogueLine { DialogueId = dialogue.Id, Text = "Zeig dein Siegel.", SortOrder = 1 };

            // Auf diese Zeile führt keine Antwort — der Fund des Health Checks.
            var orphan = new DialogueLine { DialogueId = dialogue.Id, Text = "Nie gesagt.", SortOrder = 2 };

            first.Choices.Add(new DialogueChoice
            {
                DialogueLineId = first.Id,
                Text = "Wer bist du?",
                NextLineId = second.Id
            });

            // Eine Antwort ohne Ziel beendet das Gespräch und ist keine Kante.
            second.Choices.Add(new DialogueChoice { DialogueLineId = second.Id, Text = "Hier bitte." });

            dialogue.Lines.AddRange([first, second, orphan]);

            db.Dialogues.Add(dialogue);
            await db.SaveChangesAsync();

            dialogueId = dialogue.Id;
            firstId = first.Id;
            secondId = second.Id;
            orphanId = orphan.Id;
        }

        var graph = await test.GetService<DialogueService>().GetGraphAsync(test.ProjectId, dialogueId);

        Assert.NotNull(graph);
        Assert.Equal(3, graph.Nodes.Count);

        var entry = graph.Nodes.Single(node => node.LineId == firstId);
        Assert.Equal(0, entry.Depth);
        Assert.True(entry.IsEntry);
        Assert.False(entry.EndsHere);

        var reached = graph.Nodes.Single(node => node.LineId == secondId);
        Assert.Equal(1, reached.Depth);
        Assert.True(reached.EndsHere);

        Assert.True(graph.Nodes.Single(node => node.LineId == orphanId).IsUnreachable);
        Assert.Equal(1, graph.UnreachableCount);

        // Nur die Antwort mit Ziel ist eine Kante.
        var edge = Assert.Single(graph.Edges);
        Assert.Equal(firstId, edge.FromLineId);
        Assert.Equal(secondId, edge.ToLineId);
    }

    [Fact]
    public async Task Sprechblasen_haben_keinen_Verlauf()
    {
        using var test = new TestDatabase();

        Guid dialogueId;
        await using (var db = test.CreateContext())
        {
            var dialogue = new Dialogue
            {
                GameProjectId = test.ProjectId,
                Name = "Marktgemurmel",
                Kind = DialogueKind.Bark,
                IncludesPlayer = true
            };
            dialogue.Lines.Add(new DialogueLine { DialogueId = dialogue.Id, Text = "Frischer Fisch!", SortOrder = 0 });
            dialogue.Lines.Add(new DialogueLine { DialogueId = dialogue.Id, Text = "Beste Preise!", SortOrder = 1 });

            db.Dialogues.Add(dialogue);
            await db.SaveChangesAsync();
            dialogueId = dialogue.Id;
        }

        var graph = await test.GetService<DialogueService>().GetGraphAsync(test.ProjectId, dialogueId);

        Assert.NotNull(graph);

        // Alle Zeilen stehen unabhängig nebeneinander: kein Fund, keine Kante.
        Assert.All(graph.Nodes, node => Assert.Equal(0, node.Depth));
        Assert.All(graph.Nodes, node => Assert.True(node.IsEntry));
        Assert.Equal(0, graph.UnreachableCount);
        Assert.Empty(graph.Edges);
    }
}
