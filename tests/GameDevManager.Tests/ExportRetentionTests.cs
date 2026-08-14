using System.IO.Compression;
using System.Text.Json.Nodes;
using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Aufbewahrung der Exportstände: Seit das Sicherheitsnetz vor jedem ersetzenden Import
/// und jedem Projektlöschen einen Stand anlegt, wächst das Verzeichnis von allein — geprüft
/// wird, dass es nach den eingestellten Grenzen auch wieder abnimmt und dabei nie den
/// jüngsten Stand oder fremde Dateien mitnimmt.
/// </summary>
public class ExportRetentionTests
{
    /// <summary>Ein Item, damit die Stände etwas zu tragen haben.</summary>
    private static async Task SeedAsync(TestDatabase database)
    {
        var items = database.GetService<ItemService>();

        var context = await items.LoadForEditAsync(database.ProjectId, null);
        context!.Entity.Name = "Fackel";
        await items.SaveItemAsync(context);
    }

    /// <summary>
    /// Datiert einen Stand zurück. Maßgeblich ist der Zeitpunkt aus dem Manifest — derselbe,
    /// den die Historie zeigt —, also wird genau der im Archiv umgeschrieben.
    /// </summary>
    private static void Backdate(TestDatabase database, ExportSnapshot snapshot, TimeSpan age)
    {
        var path = Path.Combine(database.ExportPath, snapshot.FileName);

        using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
        var entry = archive.Entries.Single(e =>
            e.FullName.EndsWith(ExportFormat.ManifestFileName, StringComparison.Ordinal));
        var name = entry.FullName;

        JsonNode manifest;
        using (var read = entry.Open())
        {
            manifest = JsonNode.Parse(read)!;
        }

        manifest["exportedAtUtc"] = DateTime.SpecifyKind(DateTime.UtcNow - age, DateTimeKind.Utc);

        entry.Delete();
        using var write = new StreamWriter(archive.CreateEntry(name).Open());
        write.Write(manifest.ToJsonString());
    }

    [Fact]
    public async Task Ueber_der_Obergrenze_faellt_der_aelteste_Stand_weg()
    {
        using var database = new TestDatabase();
        await SeedAsync(database);

        database.ExportOptions.MaxPerProject = 2;
        database.ExportOptions.MaxAgeDays = 0;

        var snapshots = database.GetService<ExportSnapshotService>();

        var first = await snapshots.CreateAsync(database.ProjectId, includeAssets: false);
        var second = await snapshots.CreateAsync(database.ProjectId, includeAssets: false);
        var third = await snapshots.CreateAsync(database.ProjectId, includeAssets: false);

        var kept = snapshots.List(database.ProjectId);

        Assert.Equal(
            new[] { third.FileName, second.FileName },
            kept.Select(snapshot => snapshot.FileName));
        Assert.False(File.Exists(Path.Combine(database.ExportPath, first.FileName)));
    }

    [Fact]
    public async Task Ohne_Grenzen_bleibt_jeder_Stand_liegen()
    {
        using var database = new TestDatabase();
        await SeedAsync(database);

        database.ExportOptions.MaxPerProject = 0;
        database.ExportOptions.MaxAgeDays = 0;

        var snapshots = database.GetService<ExportSnapshotService>();

        for (var round = 0; round < 3; round++)
        {
            await snapshots.CreateAsync(database.ProjectId, includeAssets: false);
        }

        Assert.Equal(3, snapshots.List(database.ProjectId).Count);
        Assert.Empty(await snapshots.PruneAsync(database.ProjectId));
    }

    [Fact]
    public async Task Zu_alte_Staende_fallen_weg_der_juengste_aber_nie()
    {
        using var database = new TestDatabase();
        await SeedAsync(database);

        database.ExportOptions.MaxPerProject = 0;
        database.ExportOptions.MaxAgeDays = 7;

        var snapshots = database.GetService<ExportSnapshotService>();

        var older = await snapshots.CreateAsync(database.ProjectId, includeAssets: false);
        var newer = await snapshots.CreateAsync(database.ProjectId, includeAssets: false);

        // Beide über dem Höchstalter — der jüngere muss trotzdem stehen bleiben.
        Backdate(database, older, TimeSpan.FromDays(200));
        Backdate(database, newer, TimeSpan.FromDays(100));

        var removed = await snapshots.PruneAsync(database.ProjectId);

        Assert.Equal(older.FileName, Assert.Single(removed).FileName);
        Assert.Equal(newer.FileName, Assert.Single(snapshots.List(database.ProjectId)).FileName);
    }

    [Fact]
    public async Task Ein_frischer_Stand_faellt_nicht_dem_Hoechstalter_zum_Opfer()
    {
        using var database = new TestDatabase();
        await SeedAsync(database);

        database.ExportOptions.MaxPerProject = 0;
        database.ExportOptions.MaxAgeDays = 7;

        var snapshots = database.GetService<ExportSnapshotService>();

        var old = await snapshots.CreateAsync(database.ProjectId, includeAssets: false);
        Backdate(database, old, TimeSpan.FromDays(30));

        // Das Anlegen räumt mit auf: der alte fällt weg, der neue bleibt.
        var fresh = await snapshots.CreateAsync(database.ProjectId, includeAssets: false);

        Assert.Equal(fresh.FileName, Assert.Single(snapshots.List(database.ProjectId)).FileName);
    }

    [Fact]
    public async Task Das_Sicherheitsnetz_raeumt_ebenfalls_auf()
    {
        using var database = new TestDatabase();
        await SeedAsync(database);

        database.ExportOptions.MaxPerProject = 1;
        database.ExportOptions.MaxAgeDays = 0;

        var snapshots = database.GetService<ExportSnapshotService>();
        await snapshots.CreateAsync(database.ProjectId, includeAssets: false);

        // Ein zweites Projekt, damit das zu löschende nicht das letzte ist.
        var projects = database.GetService<ProjectService>();
        await projects.SaveProjectAsync(new GameProject { Name = "Zweitprojekt" });

        // Das Löschen legt vorher ein Sicherheitsnetz an — auch dieser Weg räumt auf, sonst
        // wüchse das Verzeichnis genau an der Stelle weiter, die es füllt.
        await projects.DeleteProjectAsync(database.ProjectId);

        var kept = Assert.Single(snapshots.List(database.ProjectId));
        Assert.True(kept.IncludesAssetFiles);
    }

    [Fact]
    public async Task Aufgeraeumt_wird_nur_das_eigene_Projekt_und_nichts_Fremdes()
    {
        using var database = new TestDatabase();
        await SeedAsync(database);

        database.ExportOptions.MaxPerProject = 1;
        database.ExportOptions.MaxAgeDays = 0;

        var snapshots = database.GetService<ExportSnapshotService>();

        var projects = database.GetService<ProjectService>();
        var other = new GameProject { Name = "Zweitprojekt" };
        await projects.SaveProjectAsync(other);

        var foreign = await snapshots.CreateAsync(other.Id, includeAssets: false);

        // Eine Datei, die nicht von uns ist: Sie taucht in der Historie nicht auf und darf
        // deshalb auch nicht weggeräumt werden.
        var stranger = Path.Combine(database.ExportPath, "notizen.txt");
        await File.WriteAllTextAsync(stranger, "kein Exportstand");

        await snapshots.CreateAsync(database.ProjectId, includeAssets: false);
        await snapshots.CreateAsync(database.ProjectId, includeAssets: false);

        Assert.Single(snapshots.List(database.ProjectId));
        Assert.Equal(foreign.FileName, Assert.Single(snapshots.List(other.Id)).FileName);
        Assert.True(File.Exists(stranger));
    }

    [Fact]
    public async Task Aufraeumen_von_Hand_braucht_das_Exportrecht()
    {
        using var database = new TestDatabase();
        await SeedAsync(database);

        database.ExportOptions.MaxPerProject = 1;

        var snapshots = database.GetService<ExportSnapshotService>();
        await snapshots.CreateAsync(database.ProjectId, includeAssets: false);

        database.Permissions.Current = UserPermissions.Full with { IsAdministrator = false, CanExport = false };

        await Assert.ThrowsAsync<ContentValidationException>(
            () => snapshots.PruneAsync(database.ProjectId));
    }
}
