using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Der Review-Workflow: Zur Abnahme geben, freigeben oder mit Pflicht-Anmerkung ablehnen —
/// und das Änderungsprotokoll hält fest, wer entschieden hat.
/// </summary>
public class ReviewTests
{
    private static async Task<(Guid ItemId, Guid ReviewerId)> SeedAsync(TestDatabase test)
    {
        await using var db = test.CreateContext();

        var item = new Item { GameProjectId = test.ProjectId, Name = "Eisenschwert" };
        var reviewer = new AppUser
        {
            UserName = "lena",
            DisplayName = "Lena",
            PasswordHash = "x"
        };

        db.Items.Add(item);
        db.AppUsers.Add(reviewer);
        await db.SaveChangesAsync();

        return (item.Id, reviewer.Id);
    }

    [Fact]
    public async Task Zur_Abnahme_geben_setzt_den_Stand_und_haelt_eine_offene_Anfrage()
    {
        using var test = new TestDatabase();
        var (itemId, reviewerId) = await SeedAsync(test);

        var reviews = test.GetService<ReviewService>();
        await reviews.RequestAsync(test.ProjectId, ModuleKeys.Items, itemId, reviewerId, "bitte Werte prüfen");

        await using var db = test.CreateContext();

        Assert.Equal(ContentStatus.InReview, (await db.Items.SingleAsync(i => i.Id == itemId)).Status);

        var request = Assert.Single(await db.ReviewRequests.ToListAsync());
        Assert.True(request.IsOpen);
        Assert.Equal(reviewerId, request.AssignedUserId);
        Assert.Equal("bitte Werte prüfen", request.Note);
        Assert.Equal("Testbenutzer", request.RequestedBy);

        // Eine zweite offene Abnahme für dieselbe Entität ist ein Widerspruch.
        await Assert.ThrowsAsync<ContentValidationException>(
            () => reviews.RequestAsync(test.ProjectId, ModuleKeys.Items, itemId, reviewerId, null));
    }

    [Fact]
    public async Task Freigabe_setzt_auf_Fertig_und_das_Protokoll_kennt_den_Entscheider()
    {
        using var test = new TestDatabase();
        var (itemId, reviewerId) = await SeedAsync(test);

        var reviews = test.GetService<ReviewService>();
        await reviews.RequestAsync(test.ProjectId, ModuleKeys.Items, itemId, reviewerId, null);

        // Die Empfängerin entscheidet — ab hier handelt sie.
        test.Author.Current = new ChangeAuthor(reviewerId, "Lena");
        var request = (await test.CreateContext().ReviewRequests.SingleAsync()).Id;
        await reviews.DecideAsync(request, approve: true, note: null);

        await using var db = test.CreateContext();

        Assert.Equal(ContentStatus.Done, (await db.Items.SingleAsync(i => i.Id == itemId)).Status);

        var decided = await db.ReviewRequests.SingleAsync();
        Assert.Equal(ReviewDecision.Approved, decided.Decision);
        Assert.Equal("Lena", decided.DecidedBy);

        // Das Änderungsprotokoll hält den Stand-Wechsel unter ihrem Namen fest.
        Assert.Contains(await db.ChangeLogEntries.ToListAsync(),
            entry => entry.EntityId == itemId && entry.UserName == "Lena");

        // Entschieden ist entschieden.
        await Assert.ThrowsAsync<ContentValidationException>(
            () => reviews.DecideAsync(request, approve: true, note: null));
    }

    [Fact]
    public async Task Ablehnung_braucht_eine_Anmerkung_und_setzt_zurueck_auf_In_Arbeit()
    {
        using var test = new TestDatabase();
        var (itemId, reviewerId) = await SeedAsync(test);

        var reviews = test.GetService<ReviewService>();
        await reviews.RequestAsync(test.ProjectId, ModuleKeys.Items, itemId, reviewerId, null);

        var request = (await test.CreateContext().ReviewRequests.SingleAsync()).Id;

        await Assert.ThrowsAsync<ContentValidationException>(
            () => reviews.DecideAsync(request, approve: false, note: "  "));

        await reviews.DecideAsync(request, approve: false, note: "Schaden ist zu hoch");

        await using var db = test.CreateContext();

        Assert.Equal(ContentStatus.InProgress, (await db.Items.SingleAsync(i => i.Id == itemId)).Status);

        var decided = await db.ReviewRequests.SingleAsync();
        Assert.Equal(ReviewDecision.Rejected, decided.Decision);
        Assert.Equal("Schaden ist zu hoch", decided.DecisionNote);
    }

    [Fact]
    public async Task Das_Band_zeigt_nur_die_eigenen_offenen_Abnahmen()
    {
        using var test = new TestDatabase();
        var (itemId, reviewerId) = await SeedAsync(test);

        var reviews = test.GetService<ReviewService>();
        await reviews.RequestAsync(test.ProjectId, ModuleKeys.Items, itemId, reviewerId, null);

        // Der Anforderer selbst hat nichts abzunehmen.
        Assert.Empty(await reviews.GetMyOpenAsync(test.ProjectId, 8));

        // Die Empfängerin schon — samt aufgelöstem Namen.
        test.Author.Current = new ChangeAuthor(reviewerId, "Lena");
        var open = Assert.Single(await reviews.GetMyOpenAsync(test.ProjectId, 8));
        Assert.Equal("Eisenschwert", open.EntityName);
        Assert.Equal(ModuleKeys.Items, open.OwnerModuleKey);

        // Ohne Anmeldung bleibt das Band leer.
        test.Author.Current = new ChangeAuthor(null, "System");
        Assert.Empty(await reviews.GetMyOpenAsync(test.ProjectId, 8));
    }

    [Fact]
    public async Task Loeschen_der_Entitaet_raeumt_ihre_Abnahmen_mit_ab()
    {
        using var test = new TestDatabase();
        var (itemId, reviewerId) = await SeedAsync(test);

        await test.GetService<ReviewService>()
            .RequestAsync(test.ProjectId, ModuleKeys.Items, itemId, reviewerId, null);

        await test.GetService<ItemService>().DeleteItemAsync(itemId);

        await using var db = test.CreateContext();
        Assert.Empty(await db.ReviewRequests.ToListAsync());
    }
}
