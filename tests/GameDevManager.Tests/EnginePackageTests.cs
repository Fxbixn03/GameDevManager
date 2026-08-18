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
        Assert.Contains("package/unity/Editor/GdmSyncWindow.cs", paths);

        // Das Sync-Fenster muss dieselbe Protokollversion sprechen wie der Endpunkt.
        var sync = files.Single(file => file.Path.EndsWith("GdmSyncWindow.cs"));
        Assert.Contains($"ProtocolVersion = {SyncProtocol.Version}", sync.Content);
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

        // Die Szenen-Anbindung: Einträge ziehen sich als kleine Drop-Szene in den Baum,
        // der Knoten trägt GUID und Modul als Metadaten.
        var panel = files.Single(file => file.Path.EndsWith("gdm_panel.gd"));
        Assert.Contains("set_drag_forwarding", panel.Content);
        Assert.Contains("gdm_id", panel.Content);
        Assert.Contains("gdm_module", panel.Content);
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
    public void Unreal_bekommt_ein_Import_Skript_aber_kein_Plugin()
    {
        // Die DataTable-CSV bleibt der eingebaute Weg — das Skript bündelt ihn nur (alle
        // Tabellen in einem Lauf, wiederholt ersetzen statt duplizieren) und ergänzt damit
        // die „kein Plugin“-Linie, statt sie zu ersetzen.
        var files = EnginePackageWriter.Build(TargetEngine.Unreal, "Beispiel", 22);

        var script = Assert.Single(files);
        Assert.Equal("package/unreal/import_gdm_tables.py", script.Path);
        Assert.Contains("replace_existing = True", script.Content);
        Assert.Contains("Beispiel", script.Content);
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
