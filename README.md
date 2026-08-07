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

## Kernideen

Ein paar Prinzipien ziehen sich durch alle Module.

Das Dashboard zeigt die Module als Cards. Welche Cards zu sehen sind und wie sie angeordnet werden, stellt der Nutzer selbst ein. Eine Topbar listet alle Module und hebt das gerade aktive hervor.

In fast jedem Modul kann der Nutzer eigene Arten anlegen, etwa die Item-Art "Waffe", und selbst festlegen, welche Felder dazu ausgefüllt werden. Einzelne Entitäten können darüber hinaus eigene Felder bekommen, zum Beispiel ein exotisches Item mit einer einzigartigen Funktion.

Entitäten referenzieren einander über GUIDs. Zu jeder Entität gibt es eine Referenzansicht, die zeigt, wo sie überall verwendet wird: bei Händlern, in Quests, in Crafting-Rezepten, in Loot-Tables. Das funktioniert wie "Find All References" in Visual Studio.

Bedingungen, etwa für Quests, Dialoge oder Shop-Angebote, laufen über ein gemeinsames System, das in allen Modulen gleich funktioniert.

Exporte sind versioniert und diffbar. So bleibt nachvollziehbar, was ein Content-Update verändert hat. Änderungen im Tool selbst werden pro angemeldetem Benutzer protokolliert.

## Geplante Module

| Modul | Beschreibung |
|---|---|
| Items | Items mit Name, Sprite und Werten definieren; Item-Arten mit eigenen Feldern |
| Crafting | Rezepte aus Items zusammenstellen, filterbare Rezeptliste, Crafting-Trees als Graph |
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
| SFX / Audio | noch nicht ausgearbeitet |
| Cutscenes | noch nicht ausgearbeitet |

## Export in die Game Engine

Auf dem Dashboard wird es eine Import/Export-Card geben. Darüber sind zwei Wege geplant:

- JSON zusammen mit Bildern, Sounds und VFX als ZIP-Archiv
- ein direkter Export nach Unity, Unreal Engine oder Godot, später eventuell weitere Engines

## Roadmap

1. Konzept ausarbeiten - Done ✔️
2. Kern-Architektur: Entitätenmodell, eigene Arten und Felder, GUID-Referenzen, Bedingungssystem - Done ✔️
3. Erste Module: Dashboard, Items, Asset-Bibliothek - Done ✔️
4. Darauf aufbauend: Crafting, NPCs, Loot-Tables, Karten
5. Story-Ebene: Dialoge, Story, Quests, Events
6. Import/Export mit Engine-Anbindung

## Lizenz

MIT, siehe [LICENSE](LICENSE).
