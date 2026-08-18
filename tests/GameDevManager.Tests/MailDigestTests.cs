using GameDevManager.Data;
using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Der Mail-Digest: gebündelt je Benutzer, gefiltert nach den eigenen Ereignisarten — und
/// ohne Adresse oder Versandweg passiert schlicht nichts. Die Schnittstelle wird durch eine
/// Attrappe ersetzt, wie in den MailTests angelegt.
/// </summary>
public class MailDigestTests
{
    private sealed class RecordingMailSender : IMailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];

        public bool IsConfigured => true;

        public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
        {
            Sent.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }

    private static MailNotificationService Service(TestDatabase test, IMailSender sender) =>
        new(
            test.GetService<IDbContextFactory<GameDevManagerDbContext>>(),
            sender,
            test.GetService<IStringLocalizer<DataMessages>>());

    private static async Task<AppUser> AddUserAsync(
        TestDatabase test, string name, string? email,
        bool onAssignment = true, bool onComment = true, bool onReview = true)
    {
        await using var db = test.CreateContext();

        var user = new AppUser
        {
            UserName = name.ToLowerInvariant(),
            DisplayName = name,
            PasswordHash = "x",
            Email = email,
            NotifyOnAssignment = onAssignment,
            NotifyOnComment = onComment,
            NotifyOnReview = onReview
        };

        db.AppUsers.Add(user);
        await db.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task Ereignisse_eines_Benutzers_kommen_als_eine_gebuendelte_Mail()
    {
        using var test = new TestDatabase();
        var lena = await AddUserAsync(test, "Lena", "lena@example.org");

        // Eine zugewiesene Aufgabe und eine angefragte Abnahme — zwei Zeilen, eine Mail.
        Guid itemId;
        await using (var db = test.CreateContext())
        {
            var board = new KanbanBoard { GameProjectId = test.ProjectId, Name = "Sprint" };
            var column = new KanbanColumn { BoardId = board.Id, Name = "Offen", SortOrder = 0 };
            var card = new KanbanCard
            {
                ColumnId = column.Id,
                Title = "Werte prüfen",
                AssignedUserId = lena.Id,
                AssignedAtUtc = DateTime.UtcNow
            };

            var item = new Item { GameProjectId = test.ProjectId, Name = "Eisenschwert" };
            itemId = item.Id;

            db.KanbanBoards.Add(board);
            db.KanbanColumns.Add(column);
            db.KanbanCards.Add(card);
            db.Items.Add(item);
            await db.SaveChangesAsync();
        }

        await test.GetService<ReviewService>()
            .RequestAsync(test.ProjectId, ModuleKeys.Items, itemId, lena.Id, null);

        var sender = new RecordingMailSender();
        var sent = await Service(test, sender).SendDigestAsync(DateTime.UtcNow.AddMinutes(-5));

        Assert.Equal(1, sent);
        var mail = Assert.Single(sender.Sent);
        Assert.Equal("lena@example.org", mail.To);
        Assert.Contains("Werte prüfen", mail.Body);
        Assert.Contains("Testbenutzer", mail.Body);   // der Anforderer der Abnahme
        Assert.Contains("2", mail.Subject);           // zwei Neuigkeiten, ein Betreff
    }

    [Fact]
    public async Task Abgeschaltete_Ereignisarten_und_fehlende_Adressen_bleiben_still()
    {
        using var test = new TestDatabase();

        // Ohne Adresse keine Mail; mit Adresse, aber abgeschalteter Ereignisart ebenso.
        var mute = await AddUserAsync(test, "Stumm", "stumm@example.org", onReview: false);
        await AddUserAsync(test, "Ohne", null);

        Guid itemId;
        await using (var db = test.CreateContext())
        {
            var item = new Item { GameProjectId = test.ProjectId, Name = "Fackel" };
            itemId = item.Id;
            db.Items.Add(item);
            await db.SaveChangesAsync();
        }

        // Die Abnahme entscheidet jemand anderes — das Ergebnis ginge an den Anforderer.
        test.Author.Current = new ChangeAuthor(mute.Id, "Stumm");
        var reviews = test.GetService<ReviewService>();
        await reviews.RequestAsync(test.ProjectId, ModuleKeys.Items, itemId, null, null);

        test.Author.Current = new ChangeAuthor(Guid.NewGuid(), "Entscheiderin");
        var request = (await test.CreateContext().ReviewRequests.SingleAsync()).Id;
        await reviews.DecideAsync(request, approve: true, note: null);

        var sender = new RecordingMailSender();

        Assert.Equal(0, await Service(test, sender).SendDigestAsync(DateTime.UtcNow.AddMinutes(-5)));
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task Anmerkungen_gehen_an_den_Urheber_der_Entitaet_aber_nicht_an_ihn_selbst()
    {
        using var test = new TestDatabase();
        var autor = await AddUserAsync(test, "Autor", "autor@example.org");

        // Die Entität legt „Autor“ an — der Anlege-Eintrag im Protokoll kennt ihn.
        test.Author.Current = new ChangeAuthor(autor.Id, "Autor");
        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Eisenschwert";
        await items.SaveItemAsync(context);

        var comments = test.GetService<ContentCommentService>();

        // Die eigene Anmerkung ist keine Neuigkeit …
        await comments.AddAsync(test.ProjectId, context.Entity.Id, ModuleKeys.Items, "Notiz an mich");

        var sender = new RecordingMailSender();
        Assert.Equal(0, await Service(test, sender).SendDigestAsync(DateTime.UtcNow.AddMinutes(-5)));

        // … die einer Kollegin schon.
        test.Author.Current = new ChangeAuthor(Guid.NewGuid(), "Lena");
        await comments.AddAsync(test.ProjectId, context.Entity.Id, ModuleKeys.Items, "Schaden zu hoch");

        Assert.Equal(1, await Service(test, sender).SendDigestAsync(DateTime.UtcNow.AddMinutes(-5)));
        var mail = sender.Sent.Single();
        Assert.Equal("autor@example.org", mail.To);
        Assert.Contains("Lena", mail.Body);
        Assert.Contains("Eisenschwert", mail.Body);
    }

    [Fact]
    public async Task Ohne_Versandweg_passiert_nichts()
    {
        using var test = new TestDatabase();
        await AddUserAsync(test, "Lena", "lena@example.org");

        Assert.Equal(0, await Service(test, new NullMailSender()).SendDigestAsync(DateTime.MinValue));
    }

    [Fact]
    public async Task Aeltere_Ereignisse_als_der_Schnitt_zaehlen_nicht()
    {
        using var test = new TestDatabase();
        var lena = await AddUserAsync(test, "Lena", "lena@example.org");

        Guid itemId;
        await using (var db = test.CreateContext())
        {
            var item = new Item { GameProjectId = test.ProjectId, Name = "Fackel" };
            itemId = item.Id;
            db.Items.Add(item);
            await db.SaveChangesAsync();
        }

        await test.GetService<ReviewService>()
            .RequestAsync(test.ProjectId, ModuleKeys.Items, itemId, lena.Id, null);

        var sender = new RecordingMailSender();

        // Der Schnitt liegt nach dem Ereignis — der nächste Lauf meldet es nicht noch einmal.
        Assert.Equal(0, await Service(test, sender)
            .SendDigestAsync(DateTime.UtcNow.AddMinutes(5)));
    }
}
