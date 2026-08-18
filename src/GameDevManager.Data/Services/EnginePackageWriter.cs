using GameDevManager.Domain.Entities;

namespace GameDevManager.Data.Services;

/// <summary>
/// Erzeugt die Engine-seitigen Pakete (F43): ein Unity-Package und ein Godot-Addon, die den
/// exportierten Inhalt einlesen und im Editor ein Auswahlfenster zeigen — damit niemand GUIDs
/// abtippt.
/// <para>
/// Die Dateien werden <b>mit dem Export erzeugt</b> und nicht als fertiges Paket mitgeliefert.
/// Dieselbe Überlegung wie beim Beispielprojekt: Ein mitgeliefertes Paket veraltete bei jeder
/// <c>FormatVersion</c> still, und der Nutzer bekäme das erst zu spüren, wenn nichts mehr
/// zusammenpasst. Erzeugt trägt es die Version des Exports, neben dem es liegt.
/// </para>
/// <para>
/// Bewusst <b>klein</b>: Ein Importer, ein Attribut mit Zeichner und ein Auswahlfenster. Alles
/// darüber hinaus — Szenen-Werkzeuge, Prefab-Generierung — hinge an den Konventionen eines
/// bestimmten Projekts und wäre am nächsten schon falsch. Die Engine-Presets erzeugen die
/// passenden Typen; hier steht nur der Weg von der JSON-Datei zum Editor.
/// </para>
/// </summary>
public static class EnginePackageWriter
{
    /// <summary>Der Ordner im Archiv, unter dem das Paket liegt.</summary>
    public const string Folder = "package/";

    /// <summary>
    /// Die Dateien des Pakets für eine Engine. Für Unreal gibt es bewusst <b>kein Plugin</b> —
    /// die DataTable-CSV ist dort der eingebaute Weg. Was es gibt, ist ein Python-Skript,
    /// das genau diesen eingebauten Weg bündelt: alle CSVs eines Exports in einem Lauf
    /// anlegen bzw. aktualisieren. Das <b>ergänzt</b> die „kein Plugin“-Linie, es ersetzt
    /// sie nicht — ein Skript bringt keine zweite Laufzeit-Abhängigkeit ins Projekt.
    /// </summary>
    public static List<EngineFile> Build(TargetEngine engine, string projectName, int formatVersion) =>
        engine switch
        {
            TargetEngine.Unity => BuildUnity(projectName, formatVersion),
            TargetEngine.Godot => BuildGodot(projectName, formatVersion),
            TargetEngine.Unreal => BuildUnreal(projectName, formatVersion),
            _ => []
        };

    // ------------------------------------------------------------------------------ Unreal

    private static List<EngineFile> BuildUnreal(string projectName, int formatVersion) =>
    [
        new($"{Folder}unreal/import_gdm_tables.py", $$""""
            """Importiert alle DataTable-CSVs dieses GameDevManager-Exports in einem Lauf.

            Erzeugt für „{{projectName}}“, Exportformat {{formatVersion}}. Kein Plugin — das
            Skript bündelt nur den eingebauten CSV-Import von Unreal (Automated Import mit
            replace_existing): Ein wiederholter Lauf aktualisiert die Tabellen, statt sie zu
            duplizieren.

            Aufruf im Unreal-Editor (Python-Plugin aktivieren):
                Tools → Execute Python Script → diese Datei wählen
            oder über die Konsole:
                py "<Pfad>/import_gdm_tables.py"

            Voraussetzung je Tabelle ist ein Row-Struct — CSVs ohne Zuordnung werden mit
            Hinweis übersprungen statt geraten. Zugeordnet wird über ROW_STRUCTS unten;
            ohne Eintrag gilt die Konvention /Game/GameDevManager/Structs/F<CsvName>.
            """

            import os
            import unreal

            # Wohin die Tabellen kommen — alles unter einem Ordner, nichts daneben.
            DESTINATION = "/Game/GameDevManager/Tables"

            # CSV-Name (ohne Endung) → Pfad des Row-Structs. Leere Vorgabe heißt Konvention.
            ROW_STRUCTS = {
                # "npc": "/Game/MeinSpiel/Structs/FNpcRow",
            }


            def _struct_for(name):
                path = ROW_STRUCTS.get(name, "/Game/GameDevManager/Structs/F%s" % name.capitalize())
                struct = unreal.EditorAssetLibrary.load_asset(path)

                if struct is None:
                    unreal.log_warning(
                        "GDM: Kein Row-Struct für '%s' unter %s — übersprungen. "
                        "Struct anlegen oder ROW_STRUCTS im Skript ergänzen." % (name, path))

                return struct


            def run():
                # Die CSVs liegen neben diesem Skript unter engine/ — dorthin schreibt der
                # Export seine DataTable-Dateien.
                root = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "..", "engine"))

                if not os.path.isdir(root):
                    unreal.log_error("GDM: Ordner %s fehlt — liegt das Skript noch im Export?" % root)
                    return

                tools = unreal.AssetToolsHelpers.get_asset_tools()
                imported = 0

                for file_name in sorted(os.listdir(root)):
                    if not file_name.lower().endswith(".csv"):
                        continue

                    name = os.path.splitext(file_name)[0]
                    struct = _struct_for(name)

                    if struct is None:
                        continue

                    factory = unreal.CSVImportFactory()
                    factory.automated_import_settings.import_row_struct = struct

                    task = unreal.AssetImportTask()
                    task.filename = os.path.join(root, file_name)
                    task.destination_path = DESTINATION
                    task.destination_name = name
                    task.factory = factory
                    task.automated = True
                    # Der Kern des wiederholten Laufs: ersetzen statt duplizieren.
                    task.replace_existing = True
                    task.save = True

                    tools.import_asset_tasks([task])
                    imported += 1

                unreal.log("GDM: %d Tabellen importiert bzw. aktualisiert." % imported)


            if __name__ == "__main__":
                run()
            """")
    ];

    // ------------------------------------------------------------------------------- Unity

    private static List<EngineFile> BuildUnity(string projectName, int formatVersion) =>
    [
        new($"{Folder}unity/package.json", $$"""
            {
              "name": "com.gamedevmanager.content",
              "displayName": "GameDevManager Content",
              "version": "1.0.{{formatVersion}}",
              "unity": "2022.3",
              "description": "Liest den GameDevManager-Export aus StreamingAssets und bietet im Editor ein Auswahlfenster für GUID-Referenzen. Erzeugt für „{{projectName}}“, Exportformat {{formatVersion}}."
            }
            """),

        new($"{Folder}unity/Runtime/GdmReferenceAttribute.cs", """
            using System;
            using UnityEngine;

            namespace GameDevManager
            {
                /// <summary>
                /// Markiert ein string-Feld als Verweis auf eine GameDevManager-Entität. Der
                /// Property-Drawer im Editor macht daraus ein Auswahlfeld statt eines
                /// Textfeldes — genau der Grund, warum es dieses Paket gibt.
                /// </summary>
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class GdmReferenceAttribute : PropertyAttribute
                {
                    /// <summary>Das Modul, aus dem gewählt wird — "items", "npcs", …</summary>
                    public string ModuleKey { get; }

                    public GdmReferenceAttribute(string moduleKey)
                    {
                        ModuleKey = moduleKey;
                    }
                }
            }
            """),

        new($"{Folder}unity/Runtime/GdmContent.cs", """
            using System;
            using System.Collections.Generic;
            using System.IO;
            using UnityEngine;

            namespace GameDevManager
            {
                /// <summary>
                /// Der Zugriff auf den exportierten Inhalt. Gelesen wird aus
                /// StreamingAssets/GameDevManager — dorthin entpackt der Export sein Archiv,
                /// wenn als Ziel Unity gewählt ist.
                /// </summary>
                public static class GdmContent
                {
                    /// <summary>Ein Eintrag, wie ihn das Auswahlfenster braucht.</summary>
                    [Serializable]
                    public struct Entry
                    {
                        public string id;
                        public string name;
                    }

                    [Serializable]
                    private struct Wrapper
                    {
                        public List<Entry> entries;
                    }

                    private static readonly Dictionary<string, List<Entry>> Cache =
                        new Dictionary<string, List<Entry>>();

                    /// <summary>Der Pfad, unter dem der Export liegt.</summary>
                    public static string RootPath =>
                        Path.Combine(Application.streamingAssetsPath, "GameDevManager");

                    /// <summary>
                    /// Die Einträge eines Moduls. Beim ersten Zugriff gelesen und danach
                    /// gehalten — im Editor wird das Fenster oft geöffnet, und die Datei
                    /// ändert sich nur beim nächsten Export.
                    /// </summary>
                    public static List<Entry> Load(string moduleKey)
                    {
                        List<Entry> cached;
                        if (Cache.TryGetValue(moduleKey, out cached))
                        {
                            return cached;
                        }

                        var path = Path.Combine(RootPath, "content", moduleKey + ".json");
                        var entries = new List<Entry>();

                        if (File.Exists(path))
                        {
                            // JsonUtility kann keine Wurzel-Arrays und keine Objekte mit
                            // wechselndem Wurzelnamen — deshalb der Umweg über einen
                            // Wrapper, dessen Feld hier hineingeschrieben wird.
                            var text = File.ReadAllText(path);
                            var wrapped = "{\"entries\":" + ExtractArray(text) + "}";

                            entries = JsonUtility.FromJson<Wrapper>(wrapped).entries ?? entries;
                        }

                        Cache[moduleKey] = entries;
                        return entries;
                    }

                    /// <summary>Vergisst alles Gelesene — nach einem neuen Export.</summary>
                    public static void Clear()
                    {
                        Cache.Clear();
                    }

                    /// <summary>
                    /// Schneidet das erste Array aus der Datei. Der Wurzelname wechselt je
                    /// Modul ("items", "npcs", …), und genau eine Liste steht darin.
                    /// </summary>
                    private static string ExtractArray(string json)
                    {
                        var start = json.IndexOf('[');
                        var end = json.LastIndexOf(']');

                        return start >= 0 && end > start ? json.Substring(start, end - start + 1) : "[]";
                    }
                }
            }
            """),

        new($"{Folder}unity/Editor/GdmReferenceDrawer.cs", """
            using UnityEditor;
            using UnityEngine;

            namespace GameDevManager.Editor
            {
                /// <summary>
                /// Macht aus einem [GdmReference]-Feld ein Auswahlfeld. Gespeichert wird
                /// weiterhin die GUID — angezeigt der Name, damit im Inspector nicht
                /// zweiunddreißig Hexziffern stehen.
                /// </summary>
                [CustomPropertyDrawer(typeof(GdmReferenceAttribute))]
                public sealed class GdmReferenceDrawer : PropertyDrawer
                {
                    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
                    {
                        if (property.propertyType != SerializedPropertyType.String)
                        {
                            EditorGUI.LabelField(position, label.text, "[GdmReference] gilt nur für string-Felder.");
                            return;
                        }

                        var reference = (GdmReferenceAttribute)attribute;
                        var entries = GdmContent.Load(reference.ModuleKey);

                        var names = new string[entries.Count + 1];
                        var ids = new string[entries.Count + 1];

                        names[0] = "(nichts)";
                        ids[0] = string.Empty;

                        for (var index = 0; index < entries.Count; index++)
                        {
                            names[index + 1] = entries[index].name;
                            ids[index + 1] = entries[index].id;
                        }

                        var current = System.Array.IndexOf(ids, property.stringValue);
                        if (current < 0)
                        {
                            current = 0;
                        }

                        var chosen = EditorGUI.Popup(position, label.text, current, names);
                        property.stringValue = ids[chosen];
                    }
                }
            }
            """),

        new($"{Folder}unity/Editor/GdmContentWindow.cs", """
            using UnityEditor;
            using UnityEngine;

            namespace GameDevManager.Editor
            {
                /// <summary>
                /// Ein Fenster, das den exportierten Inhalt eines Moduls auflistet — zum
                /// Nachschlagen und Kopieren einer GUID.
                /// </summary>
                public sealed class GdmContentWindow : EditorWindow
                {
                    private static readonly string[] Modules =
                    {
                        "items", "npcs", "quests", "dialogs", "loot", "crafting", "maps", "effects"
                    };

                    private int _module;
                    private string _search = string.Empty;
                    private Vector2 _scroll;

                    [MenuItem("Window/GameDevManager/Inhalte")]
                    private static void Open()
                    {
                        GetWindow<GdmContentWindow>("GameDevManager");
                    }

                    private void OnGUI()
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            _module = EditorGUILayout.Popup(_module, Modules);
                            _search = EditorGUILayout.TextField(_search);

                            if (GUILayout.Button("Neu laden", GUILayout.Width(90)))
                            {
                                GdmContent.Clear();
                            }
                        }

                        var entries = GdmContent.Load(Modules[_module]);

                        if (entries.Count == 0)
                        {
                            EditorGUILayout.HelpBox(
                                "Nichts gefunden. Liegt der Export unter " + GdmContent.RootPath + "?",
                                MessageType.Info);
                            return;
                        }

                        _scroll = EditorGUILayout.BeginScrollView(_scroll);

                        foreach (var entry in entries)
                        {
                            if (!string.IsNullOrEmpty(_search)
                                && entry.name.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                continue;
                            }

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.LabelField(entry.name);

                                if (GUILayout.Button("GUID kopieren", GUILayout.Width(120)))
                                {
                                    EditorGUIUtility.systemCopyBuffer = entry.id;
                                }
                            }
                        }

                        EditorGUILayout.EndScrollView();
                    }
                }
            }
            """),

        new($"{Folder}unity/Editor/GdmSyncWindow.cs", """
            using System;
            using System.Collections.Concurrent;
            using System.Collections.Generic;
            using System.IO;
            using System.Net.Http;
            using System.Text;
            using System.Threading;
            using System.Threading.Tasks;
            using UnityEditor;
            using UnityEngine;

            namespace GameDevManager.Editor
            {
                /// <summary>
                /// Der Live-Sync: verbindet den Editor mit dem laufenden Tool und lädt
                /// geänderte Module in die StreamingAssets-Struktur nach — Protokoll siehe
                /// knowledge/live-sync.md im Tool. Ohne Verbindung verhält sich das Paket
                /// wie immer: Dateien aus StreamingAssets, zuletzt exportiert oder geladen.
                ///
                /// Geschrieben wird ausschließlich unter GdmContent.RootPath/content — das
                /// Paket fasst nichts außerhalb seines Ordners an.
                /// </summary>
                public sealed class GdmSyncWindow : EditorWindow
                {
                    /// <summary>Muss zur SyncProtocol.Version des Tools passen.</summary>
                    private const int ProtocolVersion = 1;

                    // EditorPrefs überleben den Domain-Reload — je Projektpfad ein Satz.
                    private static string Prefix
                    {
                        get { return "GameDevManager.Sync." + Application.dataPath.GetHashCode() + "."; }
                    }

                    private static string Url
                    {
                        get { return EditorPrefs.GetString(Prefix + "Url", "http://localhost:5000"); }
                        set { EditorPrefs.SetString(Prefix + "Url", value); }
                    }

                    private static string ApiKey
                    {
                        get { return EditorPrefs.GetString(Prefix + "Key", string.Empty); }
                        set { EditorPrefs.SetString(Prefix + "Key", value); }
                    }

                    private static string ProjectId
                    {
                        get { return EditorPrefs.GetString(Prefix + "Project", string.Empty); }
                        set { EditorPrefs.SetString(Prefix + "Project", value); }
                    }

                    private static bool Enabled
                    {
                        get { return EditorPrefs.GetBool(Prefix + "Enabled", false); }
                        set { EditorPrefs.SetBool(Prefix + "Enabled", value); }
                    }

                    // "*" heißt Voll-Abgleich; sonst ein Modul-Schlüssel je Eintrag.
                    private static readonly ConcurrentQueue<string> Pending = new ConcurrentQueue<string>();
                    private static CancellationTokenSource _connection;
                    private static volatile string _status = string.Empty;
                    private static volatile bool _failed;

                    [Serializable]
                    private struct Hello
                    {
                        public int protocolVersion;
                    }

                    [Serializable]
                    private struct SyncEvent
                    {
                        public string moduleKey;
                    }

                    [Serializable]
                    private struct Changes
                    {
                        public int protocolVersion;
                        public List<SyncEvent> events;
                    }

                    /// <summary>
                    /// Nach jedem Domain-Reload neu verdrahten: Der Hintergrund-Task von
                    /// vorhin ist weg, die EditorPrefs sagen, ob er wiederkommen soll.
                    /// </summary>
                    [InitializeOnLoadMethod]
                    private static void Restore()
                    {
                        EditorApplication.update += Pump;

                        if (Enabled)
                        {
                            Connect();
                        }
                    }

                    [MenuItem("Window/GameDevManager/Live-Sync")]
                    private static void Open()
                    {
                        GetWindow<GdmSyncWindow>("GDM Live-Sync");
                    }

                    private void OnGUI()
                    {
                        using (new EditorGUI.DisabledScope(Enabled))
                        {
                            Url = EditorGUILayout.TextField("Tool-Adresse", Url);
                            ApiKey = EditorGUILayout.PasswordField("API-Schlüssel", ApiKey);
                            ProjectId = EditorGUILayout.TextField("Projekt-GUID", ProjectId);
                        }

                        EditorGUILayout.Space();

                        if (!Enabled)
                        {
                            EditorGUILayout.HelpBox(
                                "Die Projekt-GUID steht im Tool im Referenz-Panel des Projekts "
                                + "oder unter /api/v1/projects.", MessageType.None);

                            if (GUILayout.Button("Verbinden"))
                            {
                                Enabled = true;
                                Connect();
                            }
                        }
                        else
                        {
                            EditorGUILayout.HelpBox(
                                string.IsNullOrEmpty(_status) ? "Verbinde …" : _status,
                                _failed ? MessageType.Error : MessageType.Info);

                            if (GUILayout.Button("Trennen"))
                            {
                                Enabled = false;
                                Disconnect();
                                _status = string.Empty;
                                _failed = false;
                            }
                        }
                    }

                    private void OnInspectorUpdate()
                    {
                        Repaint();
                    }

                    private static void Connect()
                    {
                        Disconnect();

                        _connection = new CancellationTokenSource();
                        _status = "Verbinde …";
                        _failed = false;

                        var token = _connection.Token;
                        Task.Run(() => ListenAsync(token));
                    }

                    private static void Disconnect()
                    {
                        if (_connection != null)
                        {
                            _connection.Cancel();
                            _connection = null;
                        }
                    }

                    private static async Task ListenAsync(CancellationToken token)
                    {
                        while (!token.IsCancellationRequested)
                        {
                            try
                            {
                                using (var client = new HttpClient())
                                {
                                    client.Timeout = Timeout.InfiniteTimeSpan;

                                    var request = new HttpRequestMessage(
                                        HttpMethod.Get, Url.TrimEnd('/') + "/api/v1/sync/events");
                                    request.Headers.TryAddWithoutValidation("X-API-Key", ApiKey);

                                    using (var response = await client.SendAsync(
                                        request, HttpCompletionOption.ResponseHeadersRead, token))
                                    {
                                        if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
                                        {
                                            // Kein neuer Versuch: Ein falscher Schlüssel wird
                                            // durch Warten nicht richtig.
                                            _status = "Der API-Schlüssel wurde abgelehnt — im Tool "
                                                + "unter Konto → API-Schlüssel prüfen.";
                                            _failed = true;
                                            return;
                                        }

                                        response.EnsureSuccessStatusCode();

                                        using (var stream = await response.Content.ReadAsStreamAsync())
                                        using (var reader = new StreamReader(stream))
                                        {
                                            await ReadEventsAsync(reader, token);
                                        }
                                    }
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                return;
                            }
                            catch (Exception ex)
                            {
                                _status = "Tool nicht erreichbar (" + ex.Message + ") — neuer Versuch in 5 s.";
                                _failed = true;

                                try { await Task.Delay(5000, token); }
                                catch (OperationCanceledException) { return; }
                            }
                        }
                    }

                    private static async Task ReadEventsAsync(StreamReader reader, CancellationToken token)
                    {
                        var eventName = string.Empty;

                        while (!token.IsCancellationRequested)
                        {
                            var line = await reader.ReadLineAsync();

                            if (line == null)
                            {
                                // Das Tool hat getrennt — draußen neu verbinden; das hello
                                // der neuen Verbindung stößt den Voll-Abgleich an.
                                throw new IOException("Die Verbindung wurde beendet.");
                            }

                            if (line.StartsWith("event: "))
                            {
                                eventName = line.Substring("event: ".Length);
                            }
                            else if (line.StartsWith("data: "))
                            {
                                Handle(eventName, line.Substring("data: ".Length));
                            }
                        }
                    }

                    private static void Handle(string eventName, string json)
                    {
                        if (eventName == "hello")
                        {
                            var hello = JsonUtility.FromJson<Hello>(json);

                            if (hello.protocolVersion != ProtocolVersion)
                            {
                                // Nicht raten, was neue Felder bedeuten — das Paket neu
                                // erzeugen (Export mit Ziel Unity) und austauschen.
                                _status = "Das Tool spricht Protokoll " + hello.protocolVersion
                                    + ", dieses Paket Version " + ProtocolVersion
                                    + " — bitte das Paket über einen neuen Export aktualisieren.";
                                _failed = true;
                                Disconnect();
                                return;
                            }

                            _status = "Verbunden — gleiche vollständig ab …";
                            _failed = false;
                            Pending.Enqueue("*");
                            return;
                        }

                        if (eventName == "changes")
                        {
                            foreach (var entry in JsonUtility.FromJson<Changes>(json).events)
                            {
                                // Sammeleinträge (changelog) heißen: potenziell alles anders.
                                Pending.Enqueue(entry.moduleKey == "changelog" ? "*" : entry.moduleKey);
                            }
                        }
                    }

                    /// <summary>
                    /// Der Hauptfaden holt die Dateien: Schreiben und AssetDatabase gehören
                    /// nicht in den Hintergrund-Task.
                    /// </summary>
                    private static void Pump()
                    {
                        if (Pending.IsEmpty)
                        {
                            return;
                        }

                        var modules = new HashSet<string>();
                        string key;

                        while (Pending.TryDequeue(out key))
                        {
                            modules.Add(key);
                        }

                        var contentPath = Path.Combine(GdmContent.RootPath, "content");

                        if (modules.Contains("*"))
                        {
                            // Voll-Abgleich: alle Module, die der Export hier abgelegt hat —
                            // was es lokal nicht gibt, hat auch niemand verwendet.
                            modules.Clear();

                            if (Directory.Exists(contentPath))
                            {
                                foreach (var file in Directory.GetFiles(contentPath, "*.json"))
                                {
                                    modules.Add(Path.GetFileNameWithoutExtension(file));
                                }
                            }
                        }

                        var refreshed = 0;

                        foreach (var moduleKey in modules)
                        {
                            if (DownloadModule(moduleKey, contentPath))
                            {
                                refreshed++;
                            }
                        }

                        if (refreshed > 0)
                        {
                            GdmContent.Clear();
                            AssetDatabase.Refresh();
                            _status = "Aktualisiert: " + refreshed + " Module um "
                                + DateTime.Now.ToString("HH:mm:ss") + ".";
                            _failed = false;
                        }
                    }

                    private static bool DownloadModule(string moduleKey, string contentPath)
                    {
                        try
                        {
                            using (var client = new HttpClient())
                            {
                                client.Timeout = TimeSpan.FromSeconds(10);

                                var request = new HttpRequestMessage(HttpMethod.Get,
                                    Url.TrimEnd('/') + "/api/v1/projects/" + ProjectId
                                    + "/modules/" + moduleKey);
                                request.Headers.TryAddWithoutValidation("X-API-Key", ApiKey);

                                var response = client.SendAsync(request).GetAwaiter().GetResult();

                                using (response)
                                {
                                    if (!response.IsSuccessStatusCode)
                                    {
                                        return false;
                                    }

                                    var payload = response.Content.ReadAsStringAsync()
                                        .GetAwaiter().GetResult();

                                    // Die API liefert Metadaten drumherum; die Datei im
                                    // Exportformat trägt nur die Entitätenliste.
                                    var entities = ExtractNamedArray(payload, "entities");

                                    if (entities == null)
                                    {
                                        return false;
                                    }

                                    Directory.CreateDirectory(contentPath);
                                    File.WriteAllText(
                                        Path.Combine(contentPath, moduleKey + ".json"),
                                        "{\"" + moduleKey + "\":" + entities + "}",
                                        new UTF8Encoding(false));

                                    return true;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _status = "Nachladen von „" + moduleKey + "“ schlug fehl: " + ex.Message;
                            _failed = true;
                            return false;
                        }
                    }

                    /// <summary>
                    /// Schneidet das Array hinter "name": aus einem JSON-Text — mit Blick auf
                    /// Zeichenketten und Escapes, damit eine Klammer im Namen nichts verschiebt.
                    /// </summary>
                    private static string ExtractNamedArray(string json, string name)
                    {
                        var marker = "\"" + name + "\":";
                        var index = json.IndexOf(marker, StringComparison.Ordinal);

                        if (index < 0)
                        {
                            return null;
                        }

                        var start = json.IndexOf('[', index + marker.Length);

                        if (start < 0)
                        {
                            return null;
                        }

                        var depth = 0;
                        var inString = false;

                        for (var position = start; position < json.Length; position++)
                        {
                            var current = json[position];

                            if (inString)
                            {
                                if (current == '\\')
                                {
                                    position++;
                                }
                                else if (current == '"')
                                {
                                    inString = false;
                                }

                                continue;
                            }

                            if (current == '"')
                            {
                                inString = true;
                            }
                            else if (current == '[')
                            {
                                depth++;
                            }
                            else if (current == ']' && --depth == 0)
                            {
                                return json.Substring(start, position - start + 1);
                            }
                        }

                        return null;
                    }
                }
            }
            """)
    ];

    // ------------------------------------------------------------------------------- Godot

    private static List<EngineFile> BuildGodot(string projectName, int formatVersion) =>
    [
        new($"{Folder}godot/addons/gamedevmanager/plugin.cfg", $"""
            [plugin]

            name="GameDevManager"
            description="Liest den GameDevManager-Export und bietet im Editor ein Auswahlfenster für GUID-Referenzen. Erzeugt für „{projectName}“, Exportformat {formatVersion}."
            author="GameDevManager"
            version="1.0.{formatVersion}"
            script="plugin.gd"
            """),

        new($"{Folder}godot/addons/gamedevmanager/plugin.gd", """
            @tool
            extends EditorPlugin

            # Das Addon selbst tut wenig: Es hängt das Fenster in den Editor und räumt es
            # wieder ab. Alles Weitere steht in gdm_content.gd.

            const Panel := preload("res://addons/gamedevmanager/gdm_panel.tscn")

            var _panel: Control


            func _enter_tree() -> void:
                _panel = Panel.instantiate()
                add_control_to_bottom_panel(_panel, "GameDevManager")


            func _exit_tree() -> void:
                if _panel:
                    remove_control_from_bottom_panel(_panel)
                    _panel.queue_free()
                    _panel = null
            """),

        new($"{Folder}godot/addons/gamedevmanager/gdm_content.gd", """
            @tool
            extends RefCounted
            class_name GdmContent

            ## Der Zugriff auf den exportierten Inhalt.
            ##
            ## Gelesen wird aus res://gamedevmanager — dorthin entpackt der Export sein
            ## Archiv, wenn als Ziel Godot gewählt ist.

            const ROOT := "res://gamedevmanager"

            static var _cache: Dictionary = {}


            ## Die Einträge eines Moduls als Array aus { "id": …, "name": … }.
            ##
            ## Beim ersten Zugriff gelesen und danach gehalten — im Editor wird das Fenster oft
            ## geöffnet, und die Datei ändert sich nur beim nächsten Export.
            static func load_module(module_key: String) -> Array:
                if _cache.has(module_key):
                    return _cache[module_key]

                var path := "%s/content/%s.json" % [ROOT, module_key]
                var entries: Array = []

                if FileAccess.file_exists(path):
                    var text := FileAccess.get_file_as_string(path)
                    var parsed: Variant = JSON.parse_string(text)

                    if parsed is Dictionary:
                        # Der Wurzelname wechselt je Modul; genau eine Liste steht darin.
                        for value in parsed.values():
                            if value is Array:
                                entries = value
                                break

                _cache[module_key] = entries
                return entries


            ## Vergisst alles Gelesene — nach einem neuen Export.
            static func clear() -> void:
                _cache.clear()


            ## Der Name zu einer GUID, oder die GUID selbst, wenn es sie nicht mehr gibt.
            ##
            ## Die GUID stehen zu lassen ist Absicht: Ein leerer Text sähe aus, als wäre nichts
            ## gesetzt — dieselbe Überlegung wie beim Anzeigenamen einer Erwähnung im Tool.
            static func name_of(module_key: String, id: String) -> String:
                for entry in load_module(module_key):
                    if entry.get("id", "") == id:
                        return entry.get("name", id)

                return id
            """),

        new($"{Folder}godot/addons/gamedevmanager/gdm_panel.gd", """
            @tool
            extends Control

            ## Das Fenster im unteren Editor-Bereich: Inhalte eines Moduls nachschlagen, eine
            ## GUID in die Zwischenablage legen — und Einträge in den Szenenbaum ziehen.
            ##
            ## Das Ziehen läuft über den einen Weg, den der Szenenbaum von Haus aus annimmt:
            ## eine Szenen-Datei. Beim Anfassen eines Eintrags entsteht unter
            ## res://gamedevmanager/.drop/ eine kleine .tscn — ein nackter Node, dessen
            ## Metadaten gdm_module und gdm_id die Entität nennen. Bewusst kein Prefab und
            ## keine Projekt-Konvention: Was aus dem Knoten wird, entscheidet das Spiel.

            const MODULES := [
                "items", "npcs", "quests", "dialogs", "loot", "crafting", "maps", "effects"
            ]

            const DROP_DIR := "res://gamedevmanager/.drop"

            @onready var _module: OptionButton = $VBox/Head/Module
            @onready var _search: LineEdit = $VBox/Head/Search
            @onready var _list: ItemList = $VBox/List

            var _ids: Array = []


            func _ready() -> void:
                for key in MODULES:
                    _module.add_item(key)

                _module.item_selected.connect(func(_index: int) -> void: _refresh())
                _search.text_changed.connect(func(_text: String) -> void: _refresh())
                _list.item_activated.connect(_copy)

                # Das Ziehen übernimmt das Panel für die Liste — die Liste selbst kennt
                # nur Zeilen, das Panel kennt die Entität dahinter.
                _list.set_drag_forwarding(_drag_entry, Callable(), Callable())

                _refresh()


            func _refresh() -> void:
                _list.clear()
                _ids.clear()

                var needle := _search.text.to_lower()

                for entry in GdmContent.load_module(MODULES[_module.selected]):
                    var name: String = entry.get("name", "")

                    if needle != "" and not name.to_lower().contains(needle):
                        continue

                    _list.add_item(name)
                    _ids.append(entry.get("id", ""))


            func _copy(index: int) -> void:
                if index >= 0 and index < _ids.size():
                    DisplayServer.clipboard_set(_ids[index])


            ## Baut die Drop-Szene für den angefassten Eintrag und meldet sie als Datei-Zug —
            ## der Szenenbaum hängt sie als Kind unter den Knoten, über dem sie fällt.
            func _drag_entry(at_position: Vector2) -> Variant:
                var index := _list.get_item_at_position(at_position, true)

                if index < 0 or index >= _ids.size():
                    return null

                var id: String = _ids[index]
                var display_name: String = _list.get_item_text(index)

                var node := Node.new()
                node.name = display_name.validate_node_name()
                node.set_meta("gdm_module", MODULES[_module.selected])
                node.set_meta("gdm_id", id)

                var scene := PackedScene.new()
                scene.pack(node)
                node.free()

                DirAccess.make_dir_recursive_absolute(DROP_DIR)

                # Je Entität eine Datei, überschrieben statt gezählt: Der Ordner ist ein
                # Durchgangslager, kein Bestand.
                var path := "%s/%s.tscn" % [DROP_DIR, id]

                if ResourceSaver.save(scene, path) != OK:
                    return null

                var preview := Label.new()
                preview.text = display_name
                set_drag_preview(preview)

                return {"type": "files", "files": [path]}
            """),

        new($"{Folder}godot/addons/gamedevmanager/gdm_panel.tscn", """
            [gd_scene load_steps=2 format=3]

            [ext_resource type="Script" path="res://addons/gamedevmanager/gdm_panel.gd" id="1"]

            [node name="GdmPanel" type="Control"]
            custom_minimum_size = Vector2(0, 220)
            script = ExtResource("1")

            [node name="VBox" type="VBoxContainer" parent="."]
            anchors_preset = 15
            anchor_right = 1.0
            anchor_bottom = 1.0

            [node name="Head" type="HBoxContainer" parent="VBox"]

            [node name="Module" type="OptionButton" parent="VBox/Head"]

            [node name="Search" type="LineEdit" parent="VBox/Head"]
            size_flags_horizontal = 3
            placeholder_text = "Suchen …"

            [node name="List" type="ItemList" parent="VBox"]
            size_flags_vertical = 3
            """)
    ];
}
