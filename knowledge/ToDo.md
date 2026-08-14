# ToDo

Stand 14.08.2026 — **die offenen Punkte aus dem Konzept sind abgearbeitet, ebenso die Feature-Welle vom 13.08.** (NPC-Beziehungen und Persönlichkeit, Karten-Ebenen, Story-Erweiterungen, die neuen Module ToDo/Whiteboard/Verbindungen, der Spieler-Umbau und die Shortcuts — siehe „Erledigt“ unten). Was hier bleibt, sind gesammelte Ideen; Details zu den Anforderungen stehen in [Konzept.md](Konzept.md).

## Ideen / kleinere Verbesserungen

### Inhaltspflege & Editor-Komfort

- [ ] **Massenbearbeitung.** Mehrere Entitäten markieren und gemeinsam Art zuweisen, Tags vergeben oder einen Feldwert setzen — bei hunderten Items lohnt das schnell.
- [ ] **CSV-Import/-Export je Modul.** Balancing wird oft in Tabellen gepflegt; ein Spalten-Mapping auf die Felder der Art (Import aktualisiert über die GUID- oder Namensspalte) würde den Weg Tabelle ↔ Tool schließen.
- [ ] **Lokalisierung der Spielinhalte.** Item-Namen, Beschreibungen, Dialog- und Quest-Texte in mehreren Sprachen pflegen und mit exportieren. Das größte der Ideen-Themen (eigene Übersetzungstabelle je Text, Sprachwahl im Export, Fortschrittsanzeige „was ist noch unübersetzt“) — für jedes Spiel mit mehr als einer Sprache aber zentral.
- [ ] **Kurven vergleichen.** Zwei Levelkurven übereinander zeichnen (Spieler gegen Gegner, Klasse A gegen B) — das Diagramm kann heute nur eine.

### Betrieb & Sicherheit

- [x] **Aufräumen alter Exportstände.** *(umgesetzt)* `Exports:MaxPerProject` (Vorgabe 20) und `Exports:MaxAgeDays` (Vorgabe aus) stehen neben `Exports:StoragePath`; aufgeräumt wird bei jedem neu angelegten Stand — dem von Hand wie dem Sicherheitsnetz — und über „Jetzt aufräumen“ auf der Export-Seite. Der jüngste Stand bleibt in jedem Fall stehen.
- [x] **Aufräumen des Änderungsprotokolls.** *(umgesetzt)* `ChangeLog:MaxAgeDays` (Vorgabe 365) und `ChangeLog:MaxPerProject` (Vorgabe aus); gekürzt wird von einem täglichen Wartungslauf im Hintergrund, und Verwalter können es auf der Protokollseite über „Jetzt aufräumen“ sofort anwenden.
- [ ] **Englische Oberfläche.** Die resx-Struktur ist darauf vorbereitet (neutral = Deutsch, Satelliten-resx je Sprache plus Sprachwahl) — reine Übersetzungsarbeit, kein Umbau.

### Richtung Engine-Anbindung

- [ ] **Engine-nativere Exporte** (später): z. B. ScriptableObject-Generierung für Unity, DataTable-taugliche Dateien für Unreal, Godot-Ressourcen; ggf. weitere Engines.
- [ ] **Read-only-HTTP-API.** Die Vorstufe dazu: Inhalte je Projekt als JSON-Endpunkte (dieselben Serialisierungsregeln wie der Export), damit ein Unity-/Godot-Editor-Plugin den Stand direkt ziehen kann statt über das ZIP zu gehen. Mit der Benutzeranmeldung stünde jetzt auch die Grundlage für API-Schlüssel.

### Neues Game Engine Modul

- Hier können Pro GameEngine verschiedene Presets gebaut werden zb für einen NPC, beim einem Export in eine Gameengine kann dann ein Preset der GameEngine ausgewählt werden sodass dann dieses GameObject nurnoch gefüllt und in die Game Engine Exportiert wird.
