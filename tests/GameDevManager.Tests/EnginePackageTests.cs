using System.IO.Compression;
using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Engine-seitigen Pakete (F43): ein Unity-Package und ein Godot-Addon, die den
/// exportierten Inhalt einlesen und im Editor ein Auswahlfenster zeigen. Erzeugt und nicht
/// mitgeliefert — ein fertiges Paket veraltete bei jeder <c>FormatVersion</c> still.
/// </summary>
public class EnginePackageTests
{
    [Fact]
    public void Unity_bekommt_Importer_Attribut_Zeichner_und_Fenster()
    {
        var files = EnginePackageWriter.Build(TargetEngine.Unity, "Beispiel", 22);
        var paths = files.Select(file => file.Path).ToList();

        Assert.Contains("package/unity/package.json", paths);
        Assert.Contains("package/unity/Runtime/GdmReferenceAttribute.cs", paths);
        Assert.Contains("package/unity/Runtime/GdmContent.cs", paths);
        Assert.Contains("package/unity/Editor/GdmReferenceDrawer.cs", paths);
        Assert.Contains("package/unity/Editor/GdmContentWindow.cs", paths);
    }

    [Fact]
    public void Godot_bekommt_ein_vollstaendiges_Addon()
    {
        var files = EnginePackageWriter.Build(TargetEngine.Godot, "Beispiel", 22);
        var paths = files.Select(file => file.Path).ToList();

        // plugin.cfg und plugin.gd zusammen — ohne eines von beiden lädt Godot das Addon nicht.
        Assert.Contains("package/godot/addons/gamedevmanager/plugin.cfg", paths);
        Assert.Contains("package/godot/addons/gamedevmanager/plugin.gd", paths);
        Assert.Contains("package/godot/addons/gamedevmanager/gdm_content.gd", paths);
        Assert.Contains("package/godot/addons/gamedevmanager/gdm_panel.gd", paths);
        Assert.Contains("package/godot/addons/gamedevmanager/gdm_panel.tscn", paths);
    }

    [Fact]
    public void Das_Paket_traegt_die_Version_des_Exports()
    {
        var unity = EnginePackageWriter.Build(TargetEngine.Unity, "Beispiel", 22);
        var manifest = unity.Single(file => file.Path.EndsWith("package.json"));

        // Erzeugt statt mitgeliefert: So passt die Version zu dem Export, neben dem es liegt.
        Assert.Contains("\"version\": \"1.0.22\"", manifest.Content);
        Assert.Contains("Beispiel", manifest.Content);
    }

    [Fact]
    public void Unreal_bekommt_kein_Paket()
    {
        // Dort ist die DataTable-CSV der eingebaute Weg, und ein Plugin daneben wäre ein zweiter.
        Assert.Empty(EnginePackageWriter.Build(TargetEngine.Unreal, "Beispiel", 22));
    }

    [Fact]
    public async Task Der_Unity_Export_legt_das_Paket_mit_ins_Archiv()
    {
        using var test = new TestDatabase();

        using var zip = new MemoryStream();
        await test.GetService<ExportService>().WriteExportAsync(
            test.ProjectId, ExportTarget.Unity, includeAssets: false, zip);

        zip.Position = 0;
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);

        // Unter dem Engine-Präfix, wie alles andere auch.
        Assert.Contains(
            archive.Entries,
            entry => entry.FullName.EndsWith("package/unity/Runtime/GdmContent.cs"));
    }

    [Fact]
    public async Task Der_Json_Export_bleibt_ohne_Paket()
    {
        using var test = new TestDatabase();

        using var zip = new MemoryStream();
        await test.GetService<ExportService>().WriteExportAsync(
            test.ProjectId, ExportTarget.Json, includeAssets: false, zip);

        zip.Position = 0;
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);

        // Wer kein Engine-Ziel wählt, will den neutralen Inhalt — und keine C#-Dateien darin.
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("package/"));
    }
}
