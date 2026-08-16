using System.IO.Compression;
using System.Text.Json;
using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Export-Profile und die Modulauswahl, die sie mitbringen. Der interessante Teil ist, was ein
/// abgewähltes Modul im Archiv hinterlässt: eine leere Liste, nicht eine fehlende Datei.
/// </summary>
public class ExportProfileTests
{
    private static async Task SeedAsync(TestDatabase test)
    {
        await using var db = test.CreateContext();

        db.Items.Add(new Item { GameProjectId = test.ProjectId, Name = "Schwert" });
        db.GameEffects.Add(new GameEffect { GameProjectId = test.ProjectId, Name = "Feuer" });

        await db.SaveChangesAsync();
    }

    private static async Task<Dictionary<string, int>> ExportedCountsAsync(
        TestDatabase test, IReadOnlySet<string>? modules)
    {
        using var zip = new MemoryStream();
        await test.GetService<ExportService>().WriteExportAsync(
            test.ProjectId, ExportTarget.Json, includeAssets: false, zip, moduleKeys: modules);

        zip.Position = 0;
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);

        async Task<int> CountAsync(string file, string property)
        {
            await using var content = archive.GetEntry(file)!.Open();
            using var document = await JsonDocument.ParseAsync(content);

            return document.RootElement.GetProperty(property).GetArrayLength();
        }

        return new Dictionary<string, int>
        {
            ["items"] = await CountAsync("content/items.json", "items"),
            ["effects"] = await CountAsync("content/effects.json", "effects")
        };
    }

    [Fact]
    public async Task Ohne_Auswahl_geht_alles_hinaus()
    {
        using var test = new TestDatabase();
        await SeedAsync(test);

        var counts = await ExportedCountsAsync(test, null);

        Assert.Equal(1, counts["items"]);
        Assert.Equal(1, counts["effects"]);
    }

    [Fact]
    public async Task Ein_abgewaehltes_Modul_steht_als_leere_Liste_im_Archiv()
    {
        using var test = new TestDatabase();
        await SeedAsync(test);

        var counts = await ExportedCountsAsync(test, new HashSet<string> { ModuleKeys.Items });

        Assert.Equal(1, counts["items"]);

        // Die Datei bleibt — der Aufbau des Archivs ist derselbe, nur ihr Inhalt ist leer.
        Assert.Equal(0, counts["effects"]);
    }

    [Fact]
    public async Task Ein_Profil_merkt_sich_die_Schalter_und_uebersteht_Export_und_Import()
    {
        using var test = new TestDatabase();
        var profiles = test.GetService<ExportProfileService>();

        await profiles.SaveProfileAsync(test.ProjectId, new ExportProfile
        {
            Name = "Unity, nur Fertiges",
            Target = "Unity",
            IncludeAssets = false,
            MinimumStatus = ContentStatus.Done,
            ModuleKeys = $"{ModuleKeys.Items},{ModuleKeys.Npcs}"
        });

        using var zip = new MemoryStream();
        await test.GetService<ExportService>()
            .WriteExportAsync(test.ProjectId, ExportTarget.Json, includeAssets: false, zip);
        zip.Position = 0;

        await test.GetService<ImportService>().ImportAsync(test.ProjectId, zip, replaceExisting: true);

        var stored = Assert.Single(await profiles.GetProfilesAsync(test.ProjectId));

        Assert.Equal("Unity, nur Fertiges", stored.Name);
        Assert.Equal("Unity", stored.Target);
        Assert.False(stored.IncludeAssets);
        Assert.Equal(ContentStatus.Done, stored.MinimumStatus);
        Assert.Equal($"{ModuleKeys.Items},{ModuleKeys.Npcs}", stored.ModuleKeys);
    }

    [Fact]
    public async Task Zwei_gleichnamige_Profile_werden_abgelehnt()
    {
        using var test = new TestDatabase();
        var profiles = test.GetService<ExportProfileService>();

        await profiles.SaveProfileAsync(test.ProjectId, new ExportProfile { Name = "Nightly" });

        await Assert.ThrowsAsync<ContentValidationException>(() =>
            profiles.SaveProfileAsync(test.ProjectId, new ExportProfile { Name = "Nightly" }));
    }

    [Fact]
    public async Task Ein_Profil_laesst_sich_wieder_loeschen()
    {
        using var test = new TestDatabase();
        var profiles = test.GetService<ExportProfileService>();

        var profile = new ExportProfile { Name = "Nightly" };
        await profiles.SaveProfileAsync(test.ProjectId, profile);

        await profiles.DeleteProfileAsync(profile.Id);

        Assert.Empty(await profiles.GetProfilesAsync(test.ProjectId));
    }
}
