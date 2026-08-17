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
    /// Die Dateien des Pakets für eine Engine. Für Unreal gibt es keines: Dort ist die
    /// DataTable-CSV der eingebaute Weg, und ein Plugin daneben wäre ein zweiter.
    /// </summary>
    public static List<EngineFile> Build(TargetEngine engine, string projectName, int formatVersion) =>
        engine switch
        {
            TargetEngine.Unity => BuildUnity(projectName, formatVersion),
            TargetEngine.Godot => BuildGodot(projectName, formatVersion),
            _ => []
        };

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

            ## Das Fenster im unteren Editor-Bereich: Inhalte eines Moduls nachschlagen und
            ## eine GUID in die Zwischenablage legen.

            const MODULES := [
                "items", "npcs", "quests", "dialogs", "loot", "crafting", "maps", "effects"
            ]

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
