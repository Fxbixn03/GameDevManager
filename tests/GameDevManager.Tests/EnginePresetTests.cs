using System.IO.Compression;
using System.Text;
using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Engine-Presets und die daraus erzeugten Dateien: Aus einem Eintrag des Tools soll in
/// der Engine ein fertig gefülltes Objekt werden. Geprüft wird, dass die Werte dort landen,
/// wo das Preset sie hinschreibt — und dass ohne Preset nichts entsteht.
/// </summary>
public class EnginePresetTests
{
    private sealed record Fixture(Guid TypeId, Guid HealthField, Guid NpcId);

    private static async Task<Fixture> SeedAsync(TestDatabase database)
    {
        await using var db = database.CreateContext();

        var type = new ContentType
        {
            GameProjectId = database.ProjectId,
            ModuleKey = ModuleKeys.Npcs,
            Name = "Gegner"
        };

        var health = new FieldDefinition
        {
            ContentTypeId = type.Id,
            ModuleKey = ModuleKeys.Npcs,
            Name = "Leben",
            Type = ContentFieldType.Integer
        };

        var npc = new Npc
        {
            GameProjectId = database.ProjectId,
            ContentTypeId = type.Id,
            Name = "Ork",
            Description = "Grün und laut."
        };

        db.ContentTypes.Add(type);
        db.FieldDefinitions.Add(health);
        db.Npcs.Add(npc);
        db.FieldValues.Add(new FieldValue
        {
            OwnerEntityId = npc.Id,
            OwnerModuleKey = ModuleKeys.Npcs,
            FieldDefinitionId = health.Id,
            NumberValue = 30
        });

        await db.SaveChangesAsync();

        return new Fixture(type.Id, health.Id, npc.Id);
    }

    private static async Task<Guid> SavePresetAsync(
        TestDatabase database, Fixture seed, TargetEngine engine, string typeName)
    {
        var preset = new EnginePreset
        {
            Engine = engine,
            Name = $"NPC ({engine})",
            ModuleKey = ModuleKeys.Npcs,
            ContentTypeId = seed.TypeId,
            TypeName = typeName,
            Mappings =
            [
                new EnginePresetMapping { Target = "displayName", Source = PresetSource.Name },
                new EnginePresetMapping
                {
                    Target = "maxHealth",
                    Source = PresetSource.Field,
                    FieldDefinitionId = seed.HealthField
                },
                new EnginePresetMapping
                {
                    Target = "faction",
                    Source = PresetSource.Constant,
                    ConstantValue = "Horde"
                }
            ]
        };

        await database.GetService<EnginePresetService>().SavePresetAsync(database.ProjectId, preset);

        return preset.Id;
    }

    /// <summary>Exportiert und gibt die Textdateien des Archivs zurück.</summary>
    private static async Task<Dictionary<string, string>> ExportAsync(
        TestDatabase database, ExportTarget target)
    {
        using var zip = new MemoryStream();
        await database.GetService<ExportService>()
            .WriteExportAsync(database.ProjectId, target, includeAssets: false, zip);

        zip.Position = 0;
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);

        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in archive.Entries)
        {
            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            files[entry.FullName] = await reader.ReadToEndAsync();
        }

        return files;
    }

    [Fact]
    public async Task Ohne_Preset_entsteht_keine_Engine_Datei()
    {
        using var database = new TestDatabase();
        await SeedAsync(database);

        var files = await ExportAsync(database, ExportTarget.Unity);

        Assert.DoesNotContain(files.Keys, path => path.Contains("/engine/", StringComparison.Ordinal));
        // Der neutrale Inhalt steht unabhängig davon in jedem Export.
        Assert.Contains(files.Keys, path => path.EndsWith("content/npcs.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Unity_bekommt_eine_ScriptableObject_Klasse_und_je_Eintrag_eine_JSON()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database);
        await SavePresetAsync(database, seed, TargetEngine.Unity, "NpcData");

        var files = await ExportAsync(database, ExportTarget.Unity);

        var code = Assert.Single(files, file => file.Key.EndsWith("engine/unity/NpcData.cs", StringComparison.Ordinal)).Value;

        Assert.Contains("public class NpcData : ScriptableObject", code);
        Assert.Contains("public string displayName;", code);
        // Der Feldtyp kommt aus der Felddefinition — eine ganze Zahl wird zu int, nicht zu string.
        Assert.Contains("public int maxHealth;", code);

        var data = Assert.Single(files, file =>
            file.Key.Contains("engine/unity/NpcData/", StringComparison.Ordinal)).Value;

        Assert.Contains("\"displayName\": \"Ork\"", data);
        Assert.Contains("\"maxHealth\": 30", data);
        Assert.Contains("\"faction\": \"Horde\"", data);
    }

    [Fact]
    public async Task Unreal_bekommt_eine_DataTable_taugliche_CSV()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database);
        await SavePresetAsync(database, seed, TargetEngine.Unreal, "NpcRow");

        var files = await ExportAsync(database, ExportTarget.Unreal);
        var csv = Assert.Single(files, file => file.Key.EndsWith("engine/unreal/NpcRow.csv", StringComparison.Ordinal)).Value;

        var rows = Csv.Parse(csv, ',');

        Assert.Equal(["Name", "displayName", "maxHealth", "faction"], rows[0]);

        var row = rows[1];
        Assert.Equal(seed.NpcId.ToString("N"), row[0]);
        Assert.Equal("Ork", row[1]);
        Assert.Equal("30", row[2]);
        Assert.Equal("Horde", row[3]);
    }

    [Fact]
    public async Task Godot_bekommt_je_Eintrag_eine_tres_Ressource()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database);
        await SavePresetAsync(database, seed, TargetEngine.Godot, "NpcResource");

        var files = await ExportAsync(database, ExportTarget.Godot);
        var resource = Assert.Single(files, file => file.Key.EndsWith(".tres", StringComparison.Ordinal)).Value;

        Assert.StartsWith("[gd_resource type=\"Resource\" format=3]", resource);
        Assert.Contains("resource_name = \"Ork\"", resource);
        Assert.Contains("displayName = \"Ork\"", resource);
        Assert.Contains("maxHealth = \"30\"", resource);
    }

    [Fact]
    public async Task Das_Preset_einer_anderen_Engine_bleibt_beim_Export_draussen()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database);
        await SavePresetAsync(database, seed, TargetEngine.Godot, "NpcResource");

        var files = await ExportAsync(database, ExportTarget.Unity);

        Assert.DoesNotContain(files.Keys, path => path.Contains("/engine/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Doppelt_zugeordnete_Eigenschaften_werden_abgelehnt()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database);

        var preset = new EnginePreset
        {
            Engine = TargetEngine.Unity,
            Name = "Kaputt",
            ModuleKey = ModuleKeys.Npcs,
            TypeName = "NpcData",
            Mappings =
            [
                new EnginePresetMapping { Target = "name", Source = PresetSource.Name },
                new EnginePresetMapping { Target = "Name", Source = PresetSource.Description }
            ]
        };

        await Assert.ThrowsAsync<ContentValidationException>(() =>
            database.GetService<EnginePresetService>().SavePresetAsync(database.ProjectId, preset));
    }

    [Fact]
    public async Task Presets_ueberstehen_Export_und_Import()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database);
        await SavePresetAsync(database, seed, TargetEngine.Unity, "NpcData");

        using var zip = new MemoryStream();
        await database.GetService<ExportService>()
            .WriteExportAsync(database.ProjectId, ExportTarget.Json, includeAssets: false, zip);
        zip.Position = 0;

        await database.GetService<ImportService>().ImportAsync(database.ProjectId, zip, replaceExisting: true);

        var restored = Assert.Single(await database.GetService<EnginePresetService>().GetPresetsAsync(database.ProjectId));

        Assert.Equal("NpcData", restored.TypeName);
        Assert.Equal(3, restored.Mappings.Count);
    }
}
