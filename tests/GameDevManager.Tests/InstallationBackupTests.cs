using System.IO.Compression;
using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Sicherung der ganzen Installation (F44): alle Projekte plus das, was in keinem
/// Projekt-Export steht — Benutzer, Rollen, Boards, Protokoll. Je Projekt liegt im Archiv das
/// normale Export-ZIP; ein zweites Format für dieselbe Sache wäre eines zu viel.
/// </summary>
public class InstallationBackupTests
{
    private static async Task SeedAsync(TestDatabase test)
    {
        await using var db = test.CreateContext();

        db.Items.Add(new Item { GameProjectId = test.ProjectId, Name = "Eisenschwert" });

        db.AppUsers.Add(new AppUser
        {
            UserName = "alrik",
            DisplayName = "Alrik",
            PasswordHash = "hash-aus-der-sicherung",
            IsAdministrator = true
        });

        db.UserRoles.Add(new UserRole { Name = "Autor", CanWrite = true });

        var board = new KanbanBoard { GameProjectId = test.ProjectId, Name = "Sprint" };
        board.Columns.Add(new KanbanColumn { BoardId = board.Id, Name = "Offen", SortOrder = 0 });
        db.KanbanBoards.Add(board);

        db.Webhooks.Add(new Webhook
        {
            GameProjectId = test.ProjectId, Name = "Build", Url = "https://build.example/hook"
        });

        await db.SaveChangesAsync();
    }

    private static async Task<MemoryStream> BackupAsync(TestDatabase test)
    {
        var zip = new MemoryStream();
        await test.GetService<InstallationBackupService>().WriteBackupAsync(zip);

        zip.Position = 0;
        return zip;
    }

    [Fact]
    public async Task Die_Sicherung_enthaelt_je_Projekt_ein_Export_ZIP_und_die_Werkzeug_Daten()
    {
        using var test = new TestDatabase();
        await SeedAsync(test);

        using var zip = await BackupAsync(test);
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);

        Assert.NotNull(archive.GetEntry("installation.json"));
        Assert.NotNull(archive.GetEntry("installation/data.json"));

        // Je Projekt das normale Export-ZIP — erprobt, versioniert und diffbar.
        var project = Assert.Single(
            archive.Entries,
            entry => entry.FullName.StartsWith("projects/") && entry.FullName.EndsWith(".zip"));

        Assert.Equal($"projects/{test.ProjectId:D}.zip", project.FullName);

        // Und es ist wirklich ein lesbares Archiv, kein leerer Eintrag.
        await using var inner = project.Open();
        using var buffer = new MemoryStream();
        await inner.CopyToAsync(buffer);
        buffer.Position = 0;

        using var innerArchive = new ZipArchive(buffer, ZipArchiveMode.Read);
        Assert.NotNull(innerArchive.GetEntry("content/items.json"));
    }

    [Fact]
    public async Task Wiederherstellen_bringt_Projekte_Konten_und_Werkzeug_Daten_zurueck()
    {
        using var test = new TestDatabase();
        await SeedAsync(test);

        using var zip = await BackupAsync(test);

        // Alles wegräumen, was das Archiv zurückbringen soll.
        await using (var db = test.CreateContext())
        {
            await db.Items.ExecuteDeleteAsync();
            await db.AppUsers.ExecuteDeleteAsync();
            await db.UserRoles.ExecuteDeleteAsync();
            await db.KanbanBoards.ExecuteDeleteAsync();
            await db.Webhooks.ExecuteDeleteAsync();
        }

        zip.Position = 0;
        var result = await test.GetService<InstallationBackupService>().RestoreAsync(zip);

        Assert.Equal(1, result.Projects);
        Assert.Equal(1, result.Users);
        Assert.Equal(1, result.Roles);

        await using var check = test.CreateContext();

        Assert.Equal("Eisenschwert", (await check.Items.SingleAsync()).Name);

        // Der Passwort-Hash kommt mit — ohne ihn käme nach dem Umzug niemand mehr herein,
        // und das ist der Zweck der Übung.
        var user = await check.AppUsers.SingleAsync();
        Assert.Equal("alrik", user.UserName);
        Assert.Equal("hash-aus-der-sicherung", user.PasswordHash);
        Assert.True(user.IsAdministrator);

        var board = await check.KanbanBoards.Include(b => b.Columns).SingleAsync();
        Assert.Equal("Sprint", board.Name);
        Assert.Equal("Offen", Assert.Single(board.Columns).Name);

        Assert.Equal("Build", (await check.Webhooks.SingleAsync()).Name);
    }

    [Fact]
    public async Task Ein_Archiv_ohne_Manifest_wird_abgelehnt()
    {
        using var test = new TestDatabase();

        using var zip = new MemoryStream();

        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("irgendwas.txt");
        }

        zip.Position = 0;

        await Assert.ThrowsAsync<ContentValidationException>(
            () => test.GetService<InstallationBackupService>().RestoreAsync(zip));
    }

    [Fact]
    public async Task Eine_fremde_Sicherungs_Version_wird_abgelehnt()
    {
        using var test = new TestDatabase();

        using var zip = new MemoryStream();

        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("installation.json");
            await using var stream = entry.Open();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync("{\"backupVersion\": 99}");
        }

        zip.Position = 0;

        await Assert.ThrowsAsync<ContentValidationException>(
            () => test.GetService<InstallationBackupService>().RestoreAsync(zip));
    }
}
