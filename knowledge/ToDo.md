# ToDo

Stand 13.08.2026 — alle 22 Module haben eine Oberfläche, Import und Export sind abgeschlossen, **Projektauswahl, konfigurierbares Dashboard, Marker-Farben und das erste Testprojekt sind umgesetzt** (siehe „Erledigt“). Was hier steht, ist der Rest des Konzepts plus gesammelte Ideen; Details zu den Anforderungen stehen in [Konzept.md](Konzept.md).

## offene Punkte aus dem Konzept

- [ ] **Benutzeranmeldung, dann Changelog.** Der Changelog („welcher angemeldete Benutzer hat was geändert“) setzt die Anmeldung voraus — zuerst Login, dann Änderungsprotokoll je Entität. Das ist das größte verbliebene Stück: Identity-Integration über alle vier Provider (Migrationen!), Login-Oberfläche, und ein Änderungsprotokoll, das in jedem Modul-Service beim Speichern und Löschen mitschreibt.

## Erledigt: Projektauswahl, Dashboard, Tests

- [x] **Projektauswahl.** Wie in der Architektur vorgesehen hat sich im Kern nur `ProjectContext` geändert: Das aktive Projekt hält die Singleton-`ProjectSelection` installationsweit fest und überlebt Neustarts über `Project:CurrentId` in `appsettings.Local.json`. Gewechselt wird über den `ProjectSwitcher` in der Appbar (kompletter Reload per `forceLoad` — jede Seite lädt je Projekt frisch); die Verwaltungsseite liegt unter `/projekte`. Projektnamen sind eindeutig; das aktive und das letzte Projekt lassen sich nicht löschen, und das Löschen nutzt denselben Wipe wie der ersetzende Import, damit Feldwerte, Bedingungen und Asset-Dateien nicht als Waisen zurückbleiben.
- [x] **Konfigurierbares Dashboard.** Cards lassen sich ein-/ausblenden und anordnen (Dialog „Dashboard anpassen“); die Import/Export-Card bleibt laut Konzept immer fest sichtbar. Gespeichert je Projekt in der neuen Tabelle `DashboardCards` — Werkzeug-Konfiguration wie die Moduleinstellungen, nicht im Export.
- [x] **Erstes Testprojekt** (`tests/GameDevManager.Tests`, läuft in der CI über `dotnet test`). Abgedeckt sind die ersten Kandidaten aus der Liste: Export-Serialisierung (Navigationsobjekte raus, GUIDs drin, Enums als Text, Round-Trip), `CraftingGraph` (Baumaufbau, Ausbeuten-Rechnung mit Aufrunden je Stufe, Zyklensuche) und die Health Checks (unerfüllbare Bedingungen, Dialog-Sackgassen, Loot über 100 % nur bei `SinglePick`). Harness ist `TestDatabase`: echte Dienste aus dem DI-Aufbau der Anwendung gegen SQLite im Speicher.
- [x] **Marker-Farben auf Karten einstellbar.** Der Farbwähler im Karten-Editor füllt `MapMarker.Color`; ohne Auswahl gilt weiter die Akzentfarbe. Bei Markern mit eigenem Symbol färbt die Farbe nur den Bereichsrand — das Symbol ersetzt den Punkt.

## Ideen / kleinere Verbesserungen

- [ ] **Unterkategorien für Arten bzw. Tags** — z. B. Item-Art „Waffe“ mit Unterarten Nahkampf / Fernkampf / Magie. Zu entscheiden: Hierarchie an den Arten (`ContentType` mit Eltern-Art) oder über das Tag-Modul abbilden. Tendenz: Eltern-Art am `ContentType` (eine nullable `ParentId`-Spalte, Migration über alle vier Provider) — Arten tragen bereits die Felder, und eine Unterart soll die Felder der Eltern-Art erben; über Tags ließe sich das nicht abbilden.
- [ ] **Engine-nativere Exporte** (später): z. B. ScriptableObject-Generierung für Unity, DataTable-taugliche Dateien für Unreal, Godot-Ressourcen; ggf. weitere Engines.
