using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Aufbewahrung des Änderungsprotokolls: Es wächst mit jeder Änderung, also muss es sich
/// auf ein Höchstalter oder eine Obergrenze je Projekt kürzen lassen. Geprüft wird, dass genau
/// das wegfällt, was über der Grenze liegt — und nichts aus einem fremden Projekt.
/// </summary>
public class ChangeLogRetentionTests
{
    /// <summary>
    /// Legt Protokollzeilen an, wie sie der Interceptor schreibt — nur mit gesetztem
    /// Zeitpunkt, damit ein Test „vor einem Jahr“ prüfen kann. Innerhalb eines Aufrufs steigt
    /// der Zeitstempel, die jüngste Zeile steht am Ende.
    /// </summary>
    private static async Task<List<Guid>> AddEntriesAsync(
        TestDatabase database, Guid projectId, int count, TimeSpan age)
    {
        await using var db = database.CreateContext();

        var ids = new List<Guid>();

        for (var index = 0; index < count; index++)
        {
            var entry = new ChangeLogEntry
            {
                GameProjectId = projectId,
                UserName = "Testbenutzer",
                ModuleKey = ModuleKeys.Items,
                EntityId = Guid.NewGuid(),
                EntityName = $"Eintrag {index}",
                Action = ChangeAction.Created,
                AtUtc = DateTime.UtcNow - age + TimeSpan.FromSeconds(index)
            };

            db.ChangeLogEntries.Add(entry);
            ids.Add(entry.Id);
        }

        await db.SaveChangesAsync();

        return ids;
    }

    private static async Task<List<Guid>> RemainingAsync(TestDatabase database, Guid projectId)
    {
        await using var db = database.CreateContext();

        return await db.ChangeLogEntries
            .Where(entry => entry.GameProjectId == projectId)
            .OrderBy(entry => entry.AtUtc)
            .Select(entry => entry.Id)
            .ToListAsync();
    }

    [Fact]
    public async Task Zu_alte_Eintraege_fallen_weg()
    {
        using var database = new TestDatabase();

        database.ChangeLogRetention.MaxAgeDays = 365;
        database.ChangeLogRetention.MaxPerProject = 0;

        await AddEntriesAsync(database, database.ProjectId, 3, TimeSpan.FromDays(400));
        var recent = await AddEntriesAsync(database, database.ProjectId, 2, TimeSpan.FromDays(10));

        var changeLog = database.GetService<ChangeLogService>();

        Assert.Equal(3, await changeLog.PruneAsync(database.ProjectId));
        Assert.Equal(recent, await RemainingAsync(database, database.ProjectId));
    }

    [Fact]
    public async Task Ueber_der_Obergrenze_bleiben_die_juengsten_stehen()
    {
        using var database = new TestDatabase();

        database.ChangeLogRetention.MaxAgeDays = 0;
        database.ChangeLogRetention.MaxPerProject = 2;

        var ids = await AddEntriesAsync(database, database.ProjectId, 5, TimeSpan.FromDays(1));

        var changeLog = database.GetService<ChangeLogService>();

        Assert.Equal(3, await changeLog.PruneAsync(database.ProjectId));
        Assert.Equal(ids[^2..], await RemainingAsync(database, database.ProjectId));
    }

    [Fact]
    public async Task Ohne_Grenzen_bleibt_alles_stehen()
    {
        using var database = new TestDatabase();

        database.ChangeLogRetention.MaxAgeDays = 0;
        database.ChangeLogRetention.MaxPerProject = 0;

        var ids = await AddEntriesAsync(database, database.ProjectId, 4, TimeSpan.FromDays(4000));

        var changeLog = database.GetService<ChangeLogService>();

        Assert.Equal(0, await changeLog.PruneAsync(database.ProjectId));
        Assert.Equal(ids, await RemainingAsync(database, database.ProjectId));
    }

    [Fact]
    public async Task Aufraeumen_laesst_das_fremde_Projekt_unberuehrt()
    {
        using var database = new TestDatabase();

        database.ChangeLogRetention.MaxAgeDays = 365;

        var other = new GameProject { Name = "Zweitprojekt" };
        await database.GetService<ProjectService>().SaveProjectAsync(other);

        await AddEntriesAsync(database, database.ProjectId, 2, TimeSpan.FromDays(400));
        var foreign = await AddEntriesAsync(database, other.Id, 2, TimeSpan.FromDays(400));

        var changeLog = database.GetService<ChangeLogService>();

        Assert.Equal(2, await changeLog.PruneAsync(database.ProjectId));
        Assert.Empty(await RemainingAsync(database, database.ProjectId));
        Assert.Equal(foreign, await RemainingAsync(database, other.Id));
    }

    [Fact]
    public async Task Der_Wartungslauf_nimmt_alle_Projekte_mit()
    {
        using var database = new TestDatabase();

        database.ChangeLogRetention.MaxAgeDays = 365;

        var other = new GameProject { Name = "Zweitprojekt" };
        await database.GetService<ProjectService>().SaveProjectAsync(other);

        await AddEntriesAsync(database, database.ProjectId, 2, TimeSpan.FromDays(400));
        await AddEntriesAsync(database, other.Id, 3, TimeSpan.FromDays(400));
        var recent = await AddEntriesAsync(database, other.Id, 1, TimeSpan.FromDays(1));

        var changeLog = database.GetService<ChangeLogService>();

        Assert.Equal(5, await changeLog.PruneAllProjectsAsync());
        Assert.Empty(await RemainingAsync(database, database.ProjectId));
        Assert.Equal(recent, await RemainingAsync(database, other.Id));
    }

    [Fact]
    public async Task Eine_echte_Aenderung_wird_genauso_gekuerzt()
    {
        using var database = new TestDatabase();

        database.ChangeLogRetention.MaxAgeDays = 0;
        database.ChangeLogRetention.MaxPerProject = 1;

        var items = database.GetService<ItemService>();

        // Zwei Vorgänge, also mindestens zwei Zeilen vom Interceptor: anlegen und ändern.
        var context = await items.LoadForEditAsync(database.ProjectId, null);
        context!.Entity.Name = "Fackel";
        await items.SaveItemAsync(context);

        context.Entity.Description = "Brennt.";
        await items.SaveItemAsync(context);

        var before = await RemainingAsync(database, database.ProjectId);
        Assert.True(before.Count > 1);

        var changeLog = database.GetService<ChangeLogService>();
        var removed = await changeLog.PruneAsync(database.ProjectId);

        Assert.Equal(before.Count - 1, removed);

        // Übrig bleibt die jüngste Zeile — die Änderung, nicht das Anlegen.
        var page = await changeLog.GetEntriesAsync(database.ProjectId);
        var kept = Assert.Single(page.Rows);
        Assert.Equal(ChangeAction.Updated, kept.Action);
    }

    [Fact]
    public async Task Aufraeumen_braucht_das_Verwalterrecht()
    {
        using var database = new TestDatabase();

        database.ChangeLogRetention.MaxAgeDays = 365;
        var ids = await AddEntriesAsync(database, database.ProjectId, 2, TimeSpan.FromDays(400));

        // Schreiben darf er, das Protokoll kürzen nicht: Es ist die Auskunft darüber, wer was
        // getan hat — wer sie kappen darf, soll derselbe sein, der die Konten verwaltet.
        database.Permissions.Current = UserPermissions.Full with { IsAdministrator = false };

        var changeLog = database.GetService<ChangeLogService>();

        await Assert.ThrowsAsync<ContentValidationException>(
            () => changeLog.PruneAsync(database.ProjectId));
        await Assert.ThrowsAsync<ContentValidationException>(
            () => changeLog.PruneAllProjectsAsync());

        Assert.Equal(ids, await RemainingAsync(database, database.ProjectId));
    }
}
