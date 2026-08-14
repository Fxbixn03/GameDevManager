# ToDo

Stand 14.08.2026 — **die offenen Punkte aus dem Konzept sind abgearbeitet, ebenso die Feature-Welle vom 13.08.** (NPC-Beziehungen und Persönlichkeit, Karten-Ebenen, Story-Erweiterungen, die neuen Module ToDo/Whiteboard/Verbindungen, der Spieler-Umbau und die Shortcuts — siehe „Erledigt“ unten). Was hier bleibt, sind gesammelte Ideen; Details zu den Anforderungen stehen in [Konzept.md](Konzept.md).

## Ideen / kleinere Verbesserungen

### Inhaltspflege & Editor-Komfort

- [x] **Massenbearbeitung.** *(umgesetzt)* Eigenes Werkzeug-Modul: Modul wählen, Einträge markieren, dann Art zuweisen, Tags vergeben/entziehen oder einen Feldwert setzen bzw. leeren. Über die `IModuleEntitySource` deckt die eine Seite jedes Modul ab.
- [x] **CSV-Import/-Export je Modul.** *(umgesetzt)* Auf der Export-Seite: ein Modul als Tabelle herunterladen (Spalten `id`, `name`, `beschreibung`, `art` plus je Feld eine) und wieder einlesen. Der Import aktualisiert über `id`, sonst über `name`, legt Unbekanntes wahlweise an und lässt alles unangetastet, was in keiner Zeile steht.
- [x] **Lokalisierung der Spielinhalte.** *(umgesetzt)* Eigenes Modul: Sprachen je Projekt (eine davon Ausgangssprache), Übersetzungen zu Name, Beschreibung und Textfeldern jeder Entität, Arbeitsliste „nur Offenes“ samt Veraltet-Erkennung und Fortschritt je Sprache. Der Export trägt `content/localization.json` plus je Sprache eine fertige Zeichenketten-Tabelle unter `localization/`.
- [x] **Kurven vergleichen.** *(umgesetzt)* Das Diagramm zeichnet beliebig viele Kurven übereinander; im Kurvenfeld lädt „Vergleichen“ die gefüllten Kurven des Projekts (modulübergreifend über `CurveService`) und legt die gewählten mit eigener Farbe und eigenem Strichmuster darüber.

### Betrieb & Sicherheit

- [x] **Aufräumen alter Exportstände.** *(umgesetzt)* `Exports:MaxPerProject` (Vorgabe 20) und `Exports:MaxAgeDays` (Vorgabe aus) stehen neben `Exports:StoragePath`; aufgeräumt wird bei jedem neu angelegten Stand — dem von Hand wie dem Sicherheitsnetz — und über „Jetzt aufräumen“ auf der Export-Seite. Der jüngste Stand bleibt in jedem Fall stehen.
- [x] **Aufräumen des Änderungsprotokolls.** *(umgesetzt)* `ChangeLog:MaxAgeDays` (Vorgabe 365) und `ChangeLog:MaxPerProject` (Vorgabe aus); gekürzt wird von einem täglichen Wartungslauf im Hintergrund, und Verwalter können es auf der Protokollseite über „Jetzt aufräumen“ sofort anwenden.
- [~] **Englische Oberfläche.** *(Mechanik fertig, Übersetzung angefangen)* Die Sprachwahl steht unter „Einstellungen → Darstellung“ (`LanguageSelection`, gehalten über `Ui:Language`), und die Satelliten-Dateien werden gefunden — geprüft in `LanguageTests`. **Übersetzt sind 466 von 3128 Texten**: alle Meldungen der Datenschicht (`DataMessages`), die Modul-, Bedingungs-, Feldtyp- und Änderungs-Beschriftungen sowie Rahmen, Dashboard, Start- und Fehlerseiten. **Offen sind die rund 2660 Texte der Modulseiten** (Listen, Masken, Dialoge je Modul) — dort greift die Rückfallregel: Ohne `.en.resx` zeigt die Seite die deutsche Fassung. Es bleibt reine Übersetzungsarbeit ohne Codeänderung; die Werkzeuge dafür stehen unter „Englische Oberfläche“ in [CLAUDE.md](../CLAUDE.md).

### Richtung Engine-Anbindung

- [x] **Engine-nativere Exporte.** *(umgesetzt)* Aus den Presets entstehen beim Export in eine Engine Dateien unter `engine/`: Unity eine `ScriptableObject`-Klasse plus je Eintrag eine JSON, Unreal eine DataTable-taugliche CSV, Godot je Eintrag eine `.tres`-Ressource. Weitere Engines kämen als neuer Wert in `TargetEngine` plus ein Schreiber dazu.
- [x] **Read-only-HTTP-API.** *(umgesetzt)* `/api/v1/…` liefert Projekte, Module und einzelne Einträge als JSON mit denselben Regeln wie der Export, wahlweise samt Übersetzungen einer Sprache. Angemeldet wird über einen API-Schlüssel im Header `X-API-Key` (oder `Authorization: Bearer`); Schlüssel verwaltet ein Verwalter unter „API-Schlüssel“, gespeichert wird nur ihr Hash.

### Neues Game Engine Modul

- [x] **Engine-Presets.** *(umgesetzt)* Je Engine lassen sich Presets bauen (z. B. für einen NPC): Modul, optional eine Art, der Typname in der Engine und die Zuordnung Eigenschaft → Quelle (Name, Beschreibung, Feld, fester Wert, GUID, Art, Icon-Dateiname). Beim Export in diese Engine entsteht daraus je Eintrag ein fertig gefülltes Objekt.
