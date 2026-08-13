# ToDo

Stand 13.08.2026 — alle 22 Module haben eine Oberfläche, Import und Export sind abgeschlossen, **Projektauswahl, konfigurierbares Dashboard, Marker-Farben und das erste Testprojekt sind umgesetzt**; dazu kommen jetzt **Unterarten mit Feldvererbung, das Duplizieren von Projekten und Einträgen, die erweiterte Suche, der Dialog-Graph und das Sicherheitsnetz vor zerstörenden Aktionen** (siehe „Erledigt“). Was hier steht, ist der Rest des Konzepts plus gesammelte Ideen; Details zu den Anforderungen stehen in [Konzept.md](Konzept.md).

## offene Punkte aus dem Konzept

- [ ] **Benutzeranmeldung, dann Changelog.** Der Changelog („welcher angemeldete Benutzer hat was geändert“) setzt die Anmeldung voraus — zuerst Login, dann Änderungsprotokoll je Entität. Das ist das größte verbliebene Stück: Identity-Integration über alle vier Provider (Migrationen!), Login-Oberfläche, und ein Änderungsprotokoll, das in jedem Modul-Service beim Speichern und Löschen mitschreibt. Mit echtem Mehrbenutzerbetrieb braucht es dann auch eine Schreibkonflikt-Erkennung (RowVersion/Concurrency-Token) — heute gilt stillschweigend „der Letzte gewinnt“.
- [ ] **„Weiteres inhaltlich“ aus dem Konzept.** Stack-Größen, Haltbarkeit, Respawn-Zeiten und Crafting-Stationen sind bewusst über das Arten-/Feldsystem abgedeckt und brauchen kein eigenes Modul. Offen bleiben die Punkte, die mehr sind als ein Feld:
  - **Levelkurven und Stat-/Schadensformeln** — z. B. ein Feldtyp „Formel/Kurve“ (Ausdruck plus Wertetabelle) mit Vorschau-Diagramm am Spieler-, Klassen- und Effekt-Modul.
  - **Tech-Tree/Freischaltungen** — ein Freischaltungs-Graph auf Basis des Bedingungssystems („was schaltet was frei“), als Ansicht analog zum Diplomatie- und Dialog-Graphen; die Daten (Bedingungen je Entität) existieren schon.
  - **Tageszeit/Wetter/Biome** — kleines Definitionsmodul plus neue Bedingungsarten (`ConditionKind`), damit Spawns, Shops und Events daran hängen können.

## Erledigt: Projektauswahl, Dashboard, Tests

- [x] **Projektauswahl.** Wie in der Architektur vorgesehen hat sich im Kern nur `ProjectContext` geändert: Das aktive Projekt hält die Singleton-`ProjectSelection` installationsweit fest und überlebt Neustarts über `Project:CurrentId` in `appsettings.Local.json`. Gewechselt wird über den `ProjectSwitcher` in der Appbar (kompletter Reload per `forceLoad` — jede Seite lädt je Projekt frisch); die Verwaltungsseite liegt unter `/projekte`. Projektnamen sind eindeutig; das aktive und das letzte Projekt lassen sich nicht löschen, und das Löschen nutzt denselben Wipe wie der ersetzende Import, damit Feldwerte, Bedingungen und Asset-Dateien nicht als Waisen zurückbleiben.
- [x] **Konfigurierbares Dashboard.** Cards lassen sich ein-/ausblenden und anordnen (Dialog „Dashboard anpassen“); die Import/Export-Card bleibt laut Konzept immer fest sichtbar. Gespeichert je Projekt in der neuen Tabelle `DashboardCards` — Werkzeug-Konfiguration wie die Moduleinstellungen, nicht im Export.
- [x] **Erstes Testprojekt** (`tests/GameDevManager.Tests`, läuft in der CI über `dotnet test`). Abgedeckt sind die ersten Kandidaten aus der Liste: Export-Serialisierung (Navigationsobjekte raus, GUIDs drin, Enums als Text, Round-Trip), `CraftingGraph` (Baumaufbau, Ausbeuten-Rechnung mit Aufrunden je Stufe, Zyklensuche) und die Health Checks (unerfüllbare Bedingungen, Dialog-Sackgassen, Loot über 100 % nur bei `SinglePick`). Harness ist `TestDatabase`: echte Dienste aus dem DI-Aufbau der Anwendung gegen SQLite im Speicher.
- [x] **Marker-Farben auf Karten einstellbar.** Der Farbwähler im Karten-Editor füllt `MapMarker.Color`; ohne Auswahl gilt weiter die Akzentfarbe. Bei Markern mit eigenem Symbol färbt die Farbe nur den Bereichsrand — das Symbol ersetzt den Punkt.

## Erledigt: Unterarten, Duplizieren, Suche, Dialog-Graph, Sicherheitsnetz

- [x] **Unterkategorien für Arten.** Umgesetzt als Eltern-Art am `ContentType` (`ParentId`, Migration in allen vier Providern) — nicht über Tags, weil eine Unterart die Felder der Eltern-Art **erben** soll und Tags keine Felder tragen. Der `ContentTypeService` trägt die geerbten Felder in `ContentType.InheritedFields` zusammen (nicht persistiert, nicht im Export) und liefert die Arten in Hierarchie-Reihenfolge; `ContentEditContext.TypeFields` stellt geerbte vor eigene Felder, womit Masken, Pflichtfeldprüfung und Wertespeicherung ohne weitere Änderung stimmen. Abgelehnt werden Ringe, eine Eltern-Art aus fremdem Modul/Projekt und derselbe Feldname zweimal in einer Vererbungslinie; eine Art mit Unterarten lässt sich nicht löschen. **`FormatVersion` steht dadurch auf 2** — ältere Export-ZIPs passen nicht mehr.
- [x] **Entitäten duplizieren.** „Als Vorlage kopieren“ in jeder Modul-Liste (`EntityDuplicateButton`), umgesetzt über `IModuleEntitySource.DuplicateAsync` — ein neues Modul bekommt es damit automatisch. Kopiert werden Kind-Sammlungen, Feldwerte, individuelle Felder und Bedingungssätze; Verweise auf fremde Entitäten bleiben stehen, Sprites bewusst beim Original. Der Name wird auf den ersten freien „… (Kopie)“ gesetzt. Diplomatische Beziehungen sind ausgenommen (`CanDuplicate`) — ein zweiter Eintrag zu demselben Fraktionspaar wäre ein Widerspruch.
- [x] **Globale Suche über mehr als den Namen.** Beschreibungen liefen schon mit; dazu kommen die **Textwerte der benutzerdefinierten Felder** (`IModuleEntitySource.SearchFieldValuesAsync`, gesucht über die Entitäten des Moduls, damit der Treffer im Projekt bleibt) und die **gesprochenen Zeilen der Dialoge** (`DialogueEntitySource.SearchAsync`). Ein Namenstreffer verdrängt den Feldtreffer derselben Entität; Treffer abseits des Namens sind beschriftet.
- [x] **Dialog-Graph.** `/modules/dialogs/{id}/graph` zeichnet Zeilen als Knoten und Antworten als Kanten — als SVG ohne JavaScript wie der Diplomatie-Graph. Die Spalten sind die Tiefe im Verlauf; dieselbe Breitensuche markiert, was von der ersten Zeile aus nie erreicht wird (der Fund des Health Checks), und setzt es in eine eigene Spalte. Sprechblasen haben keinen Verlauf und bekommen deshalb keinen Graph-Einstieg.
- [x] **Automatischer Exportstand vor destruktiven Aktionen.** `ExportSnapshotService.CreateSafetyNetAsync` läuft vor dem ersetzenden Import und vor dem Löschen eines Projekts — in den Diensten und nicht in der Oberfläche, damit kein zweiter Aufrufer es vergessen kann. Immer mit Asset-Dateien, denn der Wipe nimmt die Dateien mit; ein leeres Projekt bekommt keinen Stand. Scheitert das Anlegen, scheitert die Aktion.
- [x] **Projekt duplizieren.** „Kopie anlegen“ auf der Projektseite über die vorhandene Export→Import-Strecke: flüchtiger Export, alle GUIDs neu vergeben (`ProjectDuplication`/`GuidRemap`), Einspielen in ein frisch angelegtes Projekt. Scheitert etwas, wird das leere Gerüst wieder abgeräumt. Nicht mitkopiert wird die Werkzeug-Konfiguration (Module an/aus, Dashboard-Bänder) — sie steht wie beim Export nicht im Archiv.
- [x] **Theme-Wahl merken.** Die Hell/Dunkel-Wahl hält die Singleton-`AppearanceSelection` installationsweit fest und schreibt sie über `Appearance:DarkMode` nach `appsettings.Local.json` — dasselbe Muster wie die Projektauswahl.
- [x] **Health Checks als Export-Hinweis.** Die Export-Seite lädt die Prüfungen nach dem ersten Rendern nach (wie das Dashboard) und zeigt offene Funde als Warnung über dem Download-Knopf, ohne ihn zu sperren.

## Ideen / kleinere Verbesserungen

### Inhaltspflege & Editor-Komfort

- [ ] **Massenbearbeitung.** Mehrere Entitäten markieren und gemeinsam Art zuweisen, Tags vergeben oder einen Feldwert setzen — bei hunderten Items lohnt das schnell.
- [ ] **CSV-Import/-Export je Modul.** Balancing wird oft in Tabellen gepflegt; ein Spalten-Mapping auf die Felder der Art (Import aktualisiert über die GUID- oder Namensspalte) würde den Weg Tabelle ↔ Tool schließen.
- [ ] **Lokalisierung der Spielinhalte.** Item-Namen, Beschreibungen, Dialog- und Quest-Texte in mehreren Sprachen pflegen und mit exportieren. Das größte der Ideen-Themen (eigene Übersetzungstabelle je Text, Sprachwahl im Export, Fortschrittsanzeige „was ist noch unübersetzt“) — für jedes Spiel mit mehr als einer Sprache aber zentral.
- [ ] **Fraktionsgebiete als Polygone.** Das Konzept nennt „Gebiete der Fraktionen einzeichnen“; der Kreis-Bereich der Marker ist dafür nur eine Näherung. Polygon-Zeichnen im Karten-Editor (Punktliste statt Radius am `MapMarker`).

### Betrieb & Sicherheit

- [ ] **Aufräumen alter Exportstände.** Seit das Sicherheitsnetz bei jedem ersetzenden Import und jedem Projektlöschen einen Stand anlegt, wächst das Verzeichnis von allein. Sinnvoll wäre eine Obergrenze je Projekt (die ältesten fallen weg) oder ein Höchstalter, konfigurierbar neben `Exports:StoragePath`.
- [ ] **Englische Oberfläche.** Die resx-Struktur ist darauf vorbereitet (neutral = Deutsch, Satelliten-resx je Sprache plus Sprachwahl) — reine Übersetzungsarbeit, kein Umbau.

### Richtung Engine-Anbindung

- [ ] **Engine-nativere Exporte** (später): z. B. ScriptableObject-Generierung für Unity, DataTable-taugliche Dateien für Unreal, Godot-Ressourcen; ggf. weitere Engines.
- [ ] **Read-only-HTTP-API.** Die Vorstufe dazu: Inhalte je Projekt als JSON-Endpunkte (dieselben Serialisierungsregeln wie der Export), damit ein Unity-/Godot-Editor-Plugin den Stand direkt ziehen kann statt über das ZIP zu gehen. Mit der Benutzeranmeldung bekäme sie API-Schlüssel.
