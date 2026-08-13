# ToDo

Stand 13.08.2026 — alle 22 Module haben eine Oberfläche, Import und Export sind abgeschlossen, **Projektauswahl, konfigurierbares Dashboard, Marker-Farben und das erste Testprojekt sind umgesetzt** (siehe „Erledigt“). Was hier steht, ist der Rest des Konzepts plus gesammelte Ideen; Details zu den Anforderungen stehen in [Konzept.md](Konzept.md).

## offene Punkte aus dem Konzept

- [ ] **Benutzeranmeldung, dann Changelog.** Der Changelog („welcher angemeldete Benutzer hat was geändert“) setzt die Anmeldung voraus — zuerst Login, dann Änderungsprotokoll je Entität. Das ist das größte verbliebene Stück: Identity-Integration über alle vier Provider (Migrationen!), Login-Oberfläche, und ein Änderungsprotokoll, das in jedem Modul-Service beim Speichern und Löschen mitschreibt. Mit echtem Mehrbenutzerbetrieb braucht es dann auch eine Schreibkonflikt-Erkennung (RowVersion/Concurrency-Token) — heute gilt stillschweigend „der Letzte gewinnt“.
- [ ] **„Weiteres inhaltlich“ aus dem Konzept.** Stack-Größen, Haltbarkeit, Respawn-Zeiten und Crafting-Stationen sind bewusst über das Arten-/Feldsystem abgedeckt und brauchen kein eigenes Modul. Offen bleiben die Punkte, die mehr sind als ein Feld:
  - **Levelkurven und Stat-/Schadensformeln** — z. B. ein Feldtyp „Formel/Kurve“ (Ausdruck plus Wertetabelle) mit Vorschau-Diagramm am Spieler-, Klassen- und Effekt-Modul.
  - **Tech-Tree/Freischaltungen** — ein Freischaltungs-Graph auf Basis des Bedingungssystems („was schaltet was frei“), als Ansicht analog zum Diplomatie-Graphen; die Daten (Bedingungen je Entität) existieren schon.
  - **Tageszeit/Wetter/Biome** — kleines Definitionsmodul plus neue Bedingungsarten (`ConditionKind`), damit Spawns, Shops und Events daran hängen können.

## Erledigt: Projektauswahl, Dashboard, Tests

- [x] **Projektauswahl.** Wie in der Architektur vorgesehen hat sich im Kern nur `ProjectContext` geändert: Das aktive Projekt hält die Singleton-`ProjectSelection` installationsweit fest und überlebt Neustarts über `Project:CurrentId` in `appsettings.Local.json`. Gewechselt wird über den `ProjectSwitcher` in der Appbar (kompletter Reload per `forceLoad` — jede Seite lädt je Projekt frisch); die Verwaltungsseite liegt unter `/projekte`. Projektnamen sind eindeutig; das aktive und das letzte Projekt lassen sich nicht löschen, und das Löschen nutzt denselben Wipe wie der ersetzende Import, damit Feldwerte, Bedingungen und Asset-Dateien nicht als Waisen zurückbleiben.
- [x] **Konfigurierbares Dashboard.** Cards lassen sich ein-/ausblenden und anordnen (Dialog „Dashboard anpassen“); die Import/Export-Card bleibt laut Konzept immer fest sichtbar. Gespeichert je Projekt in der neuen Tabelle `DashboardCards` — Werkzeug-Konfiguration wie die Moduleinstellungen, nicht im Export.
- [x] **Erstes Testprojekt** (`tests/GameDevManager.Tests`, läuft in der CI über `dotnet test`). Abgedeckt sind die ersten Kandidaten aus der Liste: Export-Serialisierung (Navigationsobjekte raus, GUIDs drin, Enums als Text, Round-Trip), `CraftingGraph` (Baumaufbau, Ausbeuten-Rechnung mit Aufrunden je Stufe, Zyklensuche) und die Health Checks (unerfüllbare Bedingungen, Dialog-Sackgassen, Loot über 100 % nur bei `SinglePick`). Harness ist `TestDatabase`: echte Dienste aus dem DI-Aufbau der Anwendung gegen SQLite im Speicher.
- [x] **Marker-Farben auf Karten einstellbar.** Der Farbwähler im Karten-Editor füllt `MapMarker.Color`; ohne Auswahl gilt weiter die Akzentfarbe. Bei Markern mit eigenem Symbol färbt die Farbe nur den Bereichsrand — das Symbol ersetzt den Punkt.

## Ideen / kleinere Verbesserungen

### Inhaltspflege & Editor-Komfort

- [ ] **Unterkategorien für Arten bzw. Tags** — z. B. Item-Art „Waffe“ mit Unterarten Nahkampf / Fernkampf / Magie. Zu entscheiden: Hierarchie an den Arten (`ContentType` mit Eltern-Art) oder über das Tag-Modul abbilden. Tendenz: Eltern-Art am `ContentType` (eine nullable `ParentId`-Spalte, Migration über alle vier Provider) — Arten tragen bereits die Felder, und eine Unterart soll die Felder der Eltern-Art erben; über Tags ließe sich das nicht abbilden.
- [ ] **Entitäten duplizieren.** „Als Vorlage kopieren“ für Items, NPCs, Rezepte usw. — neue GUID, Kopie von Feldwerten, individuellen Feldern und Bedingungssätzen (die hängen alle nur an der Besitzer-GUID und sind billig mitzukopieren); Sprites bewusst nicht, die sind meist entitätsspezifisch. Gibt es bisher in keinem Modul.
- [ ] **Globale Suche über mehr als den Namen.** Entitäten werden heute nur über die Namensspalte gefunden (Assets bereits auch über die Beschreibung). Beschreibungen, Dialog-Zeilen und Textfeldwerte mitsuchen — gerade Dialogtexte sind ohne Suche praktisch nicht wiederzufinden.
- [ ] **Dialog-Graph.** Gespräche als Knoten-Graph anzeigen (Zeilen als Knoten, Antworten als Kanten) statt nur als Liste — Sackgassen und Verzweigungen wären auf einen Blick sichtbar. Diplomatie-Graph und Crafting-Baum liefern die Muster dafür.
- [ ] **Massenbearbeitung.** Mehrere Entitäten markieren und gemeinsam Art zuweisen, Tags vergeben oder einen Feldwert setzen — bei hunderten Items lohnt das schnell.
- [ ] **CSV-Import/-Export je Modul.** Balancing wird oft in Tabellen gepflegt; ein Spalten-Mapping auf die Felder der Art (Import aktualisiert über die GUID- oder Namensspalte) würde den Weg Tabelle ↔ Tool schließen.
- [ ] **Lokalisierung der Spielinhalte.** Item-Namen, Beschreibungen, Dialog- und Quest-Texte in mehreren Sprachen pflegen und mit exportieren. Das größte der Ideen-Themen (eigene Übersetzungstabelle je Text, Sprachwahl im Export, Fortschrittsanzeige „was ist noch unübersetzt“) — für jedes Spiel mit mehr als einer Sprache aber zentral.
- [ ] **Fraktionsgebiete als Polygone.** Das Konzept nennt „Gebiete der Fraktionen einzeichnen“; der Kreis-Bereich der Marker ist dafür nur eine Näherung. Polygon-Zeichnen im Karten-Editor (Punktliste statt Radius am `MapMarker`).

### Betrieb & Sicherheit

- [ ] **Automatischer Exportstand vor destruktiven Aktionen.** Vor einem ersetzenden Import und vor dem Löschen eines Projekts automatisch einen Exportstand anlegen — der `ExportSnapshotService` existiert, es fehlt nur der Aufruf. Kleines Sicherheitsnetz mit großem Wert.
- [ ] **Projekt duplizieren.** „Kopie anlegen“ auf der Projektseite über die vorhandene Export→Import-Pipeline (flüchtiger Export ins neue Projekt) — nützlich für Vorlagen und Experimente.
- [ ] **Theme-Wahl merken.** Der Hell/Dunkel-Schalter gilt nur für die laufende Verbindung (`MainLayout._isDarkMode`); beim nächsten Besuch ist er zurückgesetzt. Persistieren wie die Projektauswahl.
- [ ] **Englische Oberfläche.** Die resx-Struktur ist darauf vorbereitet (neutral = Deutsch, Satelliten-resx je Sprache plus Sprachwahl) — reine Übersetzungsarbeit, kein Umbau.

### Richtung Engine-Anbindung

- [ ] **Engine-nativere Exporte** (später): z. B. ScriptableObject-Generierung für Unity, DataTable-taugliche Dateien für Unreal, Godot-Ressourcen; ggf. weitere Engines.
- [ ] **Read-only-HTTP-API.** Die Vorstufe dazu: Inhalte je Projekt als JSON-Endpunkte (dieselben Serialisierungsregeln wie der Export), damit ein Unity-/Godot-Editor-Plugin den Stand direkt ziehen kann statt über das ZIP zu gehen. Mit der Benutzeranmeldung bekäme sie API-Schlüssel.
- [ ] **Health Checks als Export-Hinweis.** Beim Export die offenen Health-Check-Funde anzeigen („3 unerfüllbare Bedingungen, 1 Zyklus — trotzdem exportieren?“) — nicht blockieren, nur sichtbar machen, dieselbe Linie wie beim Loot-Check.
