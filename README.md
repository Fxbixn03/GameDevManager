<p align="center">
  <img src="Assets/Images/Icons/MainIcon.png" alt="GameDevManager Logo" width="200" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Blazor%20Server-512BD4?logo=blazor&logoColor=white" alt="Blazor Server" />
  <img src="https://img.shields.io/badge/C%23%20%2F%20.NET-512BD4?logo=dotnet&logoColor=white" alt="C# / .NET" />
  <img src="https://img.shields.io/badge/EF%20Core-512BD4?logo=dotnet&logoColor=white" alt="EF Core" />
  <img src="https://img.shields.io/badge/PostgreSQL-4169E1?logo=postgresql&logoColor=white" alt="PostgreSQL" />
  <img src="https://img.shields.io/badge/Docker-2496ED?logo=docker&logoColor=white" alt="Docker" />
  <img src="https://img.shields.io/badge/Self--hosted-333333" alt="Self-hosted" />
</p>

# GameDevManager

Ein Verwaltungstool für die Spieleentwicklung. Damit baust du vor und während der Entwicklung ein strukturiertes Wiki für deine Spielwelt auf und exportierst die Inhalte später in deine Game Engine.

**Status: Konzeptphase.** Das Projekt ist noch in der Planung.

## Worum geht es?

GameDevManager richtet sich an Indie-Entwickler, die den fachlichen Teil ihres Spiels planen wollen: Items, NPCs, Quests, Dialoge, Karten, Fraktionen. Alles liegt an einem Ort und ist miteinander verknüpft, statt über verstreute Dokumente und Tabellen verteilt zu sein. Technische Dinge wie Code oder Engine-Konfiguration gehören bewusst nicht dazu.

## Die Idee

Man legt seine Daten strukturiert während Planungsphasen an und kann diese später und im laufe der Entwicklung Versioniert in seine Gameengine importieren und fertige Game Objekte erhalten, welche nur noch benutzt werden müssen.

## Geplante Module

| Modul | Beschreibung |
|---|---|
| Items | Items mit Name, Sprite und Werten definieren; Item-Arten mit eigenen Feldern |
| Crafting | Rezepte aus Ziel-Items, benötigten Items und einer Rezept-Art; filterbare Rezeptliste, Crafting-Trees als Graph |
| Währungen | Beliebig viele Ingame-Währungen, die Händler akzeptieren |
| NPCs | NPCs und Mobs mit Arten, Händler- und Quest-Rollen, Shop-Sortiment mit Preisen, Lagerbestand und Auffüllzeiten, Spawn-Orte auf der Karte |
| Fraktionen | Fraktionen mit Rollen und Rängen für zugeordnete NPCs |
| Diplomatie | Beziehungen zwischen Fraktionen (Allianzen, Feindschaften) als Graph |
| Karten | Welt-, Höhlen- und Gebäudekarten als Bilder; Marker für NPCs und Fraktionsgebiete; verlinkte Karten, etwa ein Hausinnenraum auf der Weltkarte |
| Dialoge | Dialoge zwischen NPCs und Spieler mit Antwortmöglichkeiten und Bedingungen; auch ambiente Sprechblasen |
| Story | Storyline auf einem Zeitstrahl, verknüpft mit NPCs, Fraktionen und Orten |
| Quests | Haupt- und Nebenmissionen sowie Events, an Story und Dialoge angelehnt, mit Bedingungen |
| Loot-Tables | Drop-Wahrscheinlichkeiten und Mengen, direkt mit Items und NPCs verknüpft |
| Effekte | Effekte und ihre Wirkung (etwa Verbrennung), zuweisbar an Items |
| Spieler | Spielerfigur und Skilltrees samt Freischaltkosten |
| Klassen | Klassen mit Fähigkeiten, mappbar auf Spieler und NPCs |
| Achievements | Erfolge, die der Spieler erreichen kann |
| Sammelobjekte | Sammelbare Objekte wie Statuen oder Notizen |
| Events | Zufallsereignisse mit Mob-Spawns, Belohnungs-Loot und Einschränkungen auf bestimmte Orte |
| Tags | Zentrale Tag-Verwaltung, pro Modul konfigurierbar |
| Asset-Bibliothek | Alle Sprites nach Modul gruppiert, mit Primär-Sprite pro Entität, Tags und Upload-Verwaltung |
| Statistik | Kennzahlen wie Anzahl der Items oder NPCs, dazu Health Checks: zyklische Rezepte, toter Content, Quests ohne Abschluss, Dialog-Sackgassen, Loot-Wahrscheinlichkeiten über 100 %, verwaiste Sprites, unerfüllbare Bedingungen |
| SFX / Audio | Ansammlung an Audio Files |
| Cutscenes | Sammlung von Video Files |

## Import & Export

Die Seite „Import &amp; Export“ deckt den kompletten Kreislauf ab:

- **Export** als JSON zusammen mit Bildern, Sounds und VFX als ZIP-Archiv — wahlweise im Ordner-Layout von Unity, Unreal Engine oder Godot, damit sich das Archiv direkt ins Engine-Projekt entpacken lässt
- **Import** des Export-ZIPs, um ein Projekt umzuziehen oder eine Sicherung wiederherzustellen (alle GUID-Referenzen bleiben erhalten)
- **Exportstände**: Stände lassen sich aufbewahren, herunterladen und paarweise — oder gegen den aktuellen Stand — vergleichen; der Diff zeigt je Modul, was dazukam, wegfiel und welche Eigenschaften sich geändert haben

## Roadmap

1. Konzept ausarbeiten - Ham wa drin 👌
2. Kern-Architektur: Entitätenmodell, eigene Arten und Felder, GUID-Referenzen, Bedingungssystem - Ham wa drin 👌
3. Erste Module: Dashboard, Items, Asset-Bibliothek - Ham wa drin 👌
4. Darauf aufbauend: Crafting, NPCs, Loot-Tables, Karten - Ham wa drin 👌
5. Story-Ebene: Dialoge, Story, Quests, Events - Ham wa drin 👌
6. Erweiterung und Härtung bestehende Module
7. Import/Export in JSON mit Sprites/Assets als ZIP, inkl. Exportständen mit Diff - Ham wa drin 👌
8. Import/Export mit Engine-Anbindung (Ordner-Layouts für Unity/Unreal/Godot - Ham wa drin 👌; engine-native Formate wie ScriptableObjects später)

## Lizenz

MIT, siehe [LICENSE](LICENSE).
