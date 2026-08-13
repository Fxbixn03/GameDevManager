# ToDo

Stand 13.08.2026 — alle 22 Module haben eine Oberfläche, **Import und Export sind abgeschlossen**: Export als JSON/ZIP inkl. Assets (Ordner-Layouts für Unity/Unreal/Godot), Import des Export-ZIPs, aufbewahrte Exportstände mit Diff-Ansicht. Was hier steht, ist der Rest des Konzepts plus gesammelte Ideen; Details zu den Anforderungen stehen in [Konzept.md](Konzept.md).

## Erledigt: Import/Export abschließen

- [x] **Import des Export-ZIPs.** Umgesetzt als `ImportService` (Seite „Import & Export“). Entschiedenes Verhalten bei vorhandenen Inhalten: Der Import stellt immer einen kompletten Projektstand her — entweder in ein leeres Projekt (sonst Ablehnung) oder mit der Option „Vorhandene Inhalte ersetzen“ (kompletter Wipe + Neuaufbau, kein Teil-Merge). `formatVersion` aus `project.json` wird geprüft, Asset-Dateien werden in den Dateispeicher zurückgeschrieben, alle GUIDs bleiben erhalten.
- [x] **Versionierte, diffbare Exporte.** Umgesetzt als `ExportSnapshotService`: Stände liegen als normale Export-ZIPs im Dateisystem (`Exports:StoragePath`, Standard `exports/` — bewusst keine Datenbanktabelle), lassen sich herunterladen, löschen und wieder importieren. Der Diff vergleicht zwei Stände (oder einen Stand gegen den aktuellen) je Modul: neu, entfernt, geändert samt geänderten Eigenschaften.

## Danach: offene Punkte aus dem Konzept

- [ ] **Projektauswahl.** Mehrere Spielprojekte nebeneinander; laut Architektur muss sich dafür nur `ProjectContext` ändern, dazu eine Verwaltungs-/Auswahlseite.
- [ ] **Konfigurierbares Dashboard.** Cards ein-/ausblenden und anordnen; die Import/Export-Card bleibt laut Konzept immer fest sichtbar.
- [ ] **Benutzeranmeldung, dann Changelog.** Der Changelog („welcher angemeldete Benutzer hat was geändert“) setzt die Anmeldung voraus — zuerst Login, dann Änderungsprotokoll je Entität.

## Qualität

- [ ] **Erstes Testprojekt anlegen** (`dotnet test`). Lohnende erste Kandidaten: Export-Serialisierung (Navigationsobjekte raus, GUIDs drin, stabile Sortierung), `CraftingGraph` (Zyklen, Grundstoff-Rechnung), die Health Checks (Bedingungen, Dialog-Sackgassen, Loot über 100 %).

## Ideen / kleinere Verbesserungen

- [ ] **Unterkategorien für Arten bzw. Tags** — z. B. Item-Art „Waffe“ mit Unterarten Nahkampf / Fernkampf / Magie. Zu entscheiden: Hierarchie an den Arten (`ContentType` mit Eltern-Art) oder über das Tag-Modul abbilden.
- [ ] **Marker-Farben auf Karten einstellbar machen.** `MapMarker.Color` existiert im Modell und `MapCanvas` zeichnet die Farbe bereits — es fehlt nur ein Farbwähler im Karten-Editor.
- [ ] **Engine-nativere Exporte** (später): z. B. ScriptableObject-Generierung für Unity, DataTable-taugliche Dateien für Unreal, Godot-Ressourcen; ggf. weitere Engines.
