# ToDo

Stand 12.08.2026 — alle 22 Module haben eine Oberfläche, der **Export** (JSON/ZIP inkl. Assets, Ordner-Layouts für Unity/Unreal/Godot) ist umgesetzt. Was hier steht, ist der Rest des Konzepts plus gesammelte Ideen; Details zu den Anforderungen stehen in [Konzept.md](Konzept.md).

## Als Nächstes: Import/Export abschließen

- [ ] **Import des Export-ZIPs.** Gegenstück zum Export, damit sich ein Projekt umziehen oder sichern lässt. Zu klären: Verhalten bei bereits vorhandenen GUIDs (überschreiben vs. ablehnen), Prüfung der `formatVersion` aus `project.json`, Wiederherstellen der Asset-Dateien in den Dateispeicher.
- [ ] **Versionierte, diffbare Exporte.** Kernforderung des Konzepts („nachvollziehen, was ein Content-Update verändert hat“). Die Vorarbeit steckt schon im Export — deterministische Sortierung, derselbe Stand ergibt dasselbe ZIP. Fehlt: Exportstände aufbewahren (Historie) und zwei Stände als Diff gegenüberstellen.

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
