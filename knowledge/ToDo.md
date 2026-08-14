# ToDo

Stand 14.08.2026 — **die offenen Punkte aus dem Konzept sind abgearbeitet, ebenso die Feature-Welle vom 13.08.** (NPC-Beziehungen und Persönlichkeit, Karten-Ebenen, Story-Erweiterungen, die neuen Module ToDo/Whiteboard/Verbindungen, der Spieler-Umbau und die Shortcuts — siehe „Erledigt“ unten). Was hier bleibt, sind gesammelte Ideen; Details zu den Anforderungen stehen in [Konzept.md](Konzept.md).

## Ideen / kleinere Verbesserungen

### Inhaltspflege & Editor-Komfort

- [ ] **Massenbearbeitung.** Mehrere Entitäten markieren und gemeinsam Art zuweisen, Tags vergeben oder einen Feldwert setzen — bei hunderten Items lohnt das schnell.
- [ ] **CSV-Import/-Export je Modul.** Balancing wird oft in Tabellen gepflegt; ein Spalten-Mapping auf die Felder der Art (Import aktualisiert über die GUID- oder Namensspalte) würde den Weg Tabelle ↔ Tool schließen.
- [ ] **Lokalisierung der Spielinhalte.** Item-Namen, Beschreibungen, Dialog- und Quest-Texte in mehreren Sprachen pflegen und mit exportieren. Das größte der Ideen-Themen (eigene Übersetzungstabelle je Text, Sprachwahl im Export, Fortschrittsanzeige „was ist noch unübersetzt“) — für jedes Spiel mit mehr als einer Sprache aber zentral.
- [ ] **Änderungsprotokoll je Entität in der Maske.** `ChangeLogService.GetForEntityAsync` liefert die Geschichte einer einzelnen Entität bereits — es fehlt nur ein Abschnitt neben der Referenzansicht, der sie zeigt.
- [ ] **Kurven vergleichen.** Zwei Levelkurven übereinander zeichnen (Spieler gegen Gegner, Klasse A gegen B) — das Diagramm kann heute nur eine.

### Betrieb & Sicherheit

- [ ] **Aufräumen alter Exportstände.** Seit das Sicherheitsnetz bei jedem ersetzenden Import und jedem Projektlöschen einen Stand anlegt, wächst das Verzeichnis von allein. Sinnvoll wäre eine Obergrenze je Projekt (die ältesten fallen weg) oder ein Höchstalter, konfigurierbar neben `Exports:StoragePath`.
- [ ] **Aufräumen des Änderungsprotokolls.** Dasselbe eine Ebene tiefer: Nach einem Jahr Arbeit stehen dort sehr viele Zeilen. Ein Höchstalter oder eine Obergrenze je Projekt wäre die passende Antwort.
- [ ] **Englische Oberfläche.** Die resx-Struktur ist darauf vorbereitet (neutral = Deutsch, Satelliten-resx je Sprache plus Sprachwahl) — reine Übersetzungsarbeit, kein Umbau.

### Richtung Engine-Anbindung

- [ ] **Engine-nativere Exporte** (später): z. B. ScriptableObject-Generierung für Unity, DataTable-taugliche Dateien für Unreal, Godot-Ressourcen; ggf. weitere Engines.
- [ ] **Read-only-HTTP-API.** Die Vorstufe dazu: Inhalte je Projekt als JSON-Endpunkte (dieselben Serialisierungsregeln wie der Export), damit ein Unity-/Godot-Editor-Plugin den Stand direkt ziehen kann statt über das ZIP zu gehen. Mit der Benutzeranmeldung stünde jetzt auch die Grundlage für API-Schlüssel.


## Erledigt (14.08.2026)

### Erweiterung bestehender Features

- [x] **Shortcuts.** Strg+K fokussiert die globale Suche, Strg+S speichert die geöffnete Maske (NPC-, Karten- und Story-Editor), Alt+H/I/N/Q/T/W navigiert zu Dashboard, Items, NPCs, Quests, ToDo und Whiteboard ([gdm-shortcuts.js](../src/GameDevManager.Web/wwwroot/js/gdm-shortcuts.js) + `KeyboardShortcuts`-Komponente).
- [x] **Karten-Ebenen.** Markierungen lassen sich Ebenen zuordnen (`MapLayer`, `MapMarker.LayerId`); Ebenen sind einzeln ein-/ausblendbar (persistiert), die im Editor aktivierte Ebene nimmt neu gesetzte Markierungen auf. Gelöschte Ebenen lassen ihre Markierungen auf die Grundebene zurückfallen.
- [x] **NPC-Beziehungen.** Frei definierbare Beziehungsarten als Bezeichnungspaar („Ist Vater von“ ↔ „Ist Sohn von“, symmetrisch mit gleichem Text) je Projekt; Beziehungen je NPC mit Haltung Freundlich/Neutral/Feindlich. Die Gegenseite zeigt dieselbe Beziehung mit der Gegenrichtungs-Bezeichnung; Referenzansicht, Export (`relationTypes` in `npcs.json`, FormatVersion 5) und Aufräumen beim Löschen sind angebunden.
- [x] **NPC einzigartig/wiederkehrend.** Schalter `IsUnique` — der Schmied im Dorf gegen den immer wieder spawnenden Waschbären.
- [x] **Vorlieben & Persönlichkeit.** Kommagetrennte Chips-Eingabe (Enter/Komma bestätigt, `ChipListInput`); gespeichert als normalisierte Textspalten.
- [x] **Wesenszüge.** Zehn feste Züge (Empathie bis Mitgefühl) mit Werten 0–10, als klickbare, von links gefüllte gestrichelte Balken (`TraitBarEditor`); kanonische Textspalte über `NpcTraits`.
- [x] **Story-Abschnitte erweitert.** Stimmung, Datum (Spielwelt, Freitext), Dauer und Ort; Schauplatz als Karte + Markierung; Sprites gab es schon; Verknüpfungen zu anderen Szenen mit freiem Etikett (`StoryLink`); Verschieben im Zeitstreifen per Drag & Drop (die Pfeile bleiben als tastaturfreundlicher Weg).

### Neue Module

- [x] **ToDo.** Beliebig viele Kanban-Boards je Projekt (`/modules/todo`), Spalten und Karten frei verwaltbar, Karten per Drag & Drop zwischen Spalten. Werkzeug-Daten: nicht im Export, überstehen den ersetzenden Import.
- [x] **Whiteboard.** Freihand zeichnen, Notizen anheften, verschieben und radieren (`/modules/whiteboard`); mehrere Nutzer sehen die gespeicherten Änderungen der anderen sofort (`WhiteboardNotifier`). Ebenfalls Werkzeug-Daten.
- [x] **Verbindungen.** NPC-Graph mit runden Profilbildern (primäres Sprite), Namen darunter, Kanten je Beziehung (Farbe = Haltung) und Farbring je Fraktion bzw. Grau für Fraktionslose (`/modules/connections`).

### Grundumbau des Spieler-Moduls

- [x] Der Spieler wird als NPC behandelt (einzigartiger NPC mit Klassen, Sprites, Feldern); das Modul heißt jetzt „Skilltrees“ und verwaltet nur noch Skilltrees und Skills. Bestehende Spielerfiguren lassen sich per Knopf in NPCs überführen — GUID und alle Anhänge (Sprites, Feldwerte, Bedingungen, Tags, Karten-Markierungen) bleiben dabei erhalten. 


### Neues Game Engine Modul

- Hier können Pro GameEngine verschiedene Presets gebaut werden zb für einen NPC, beim einem Export in eine Gameengine kann dann ein Preset der GameEngine ausgewählt werden sodass dann dieses GameObject nurnoch gefüllt und in die Game Engine Exportiert wird.
