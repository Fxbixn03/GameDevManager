# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Projektüberblick

GameDevManager ist ein selbst gehostetes Verwaltungstool für Indie-Spieleentwickler: ein strukturiertes Wiki für Spielinhalte (Items, NPCs, Quests, Dialoge, Karten, …) mit späterem Export in Game Engines (Unity, Unreal, Godot oder JSON/ZIP).

**Status: Kern steht, erstes Modul umgesetzt.** Datenbankanbindung, Theme, Modul-Registry, das Arten-/Feldsystem und das Items-Modul sind fertig; alle übrigen Module landen noch auf der Platzhalterseite `ModulePage.razor`. Template-Reste (`Class1.cs` in Domain/Data, Counter-/Weather-Seiten, leere `NavMenu.razor`) sind noch da und können weg. Testprojekte gibt es nicht. Die fachliche Quelle der Wahrheit ist [knowledge/Konzept.md](knowledge/Konzept.md) — dort sind alle Module und Anforderungen im Detail beschrieben; die README fasst sie zusammen.

**Sprache:** README, Konzept und Doku sind auf Deutsch. Neue Dokumentation und Commit-Messages ebenfalls auf Deutsch verfassen. Code (Bezeichner, Kommentare) auf Englisch.

## Befehle

```powershell
dotnet build                                     # baut die Solution (GameDevManager.slnx)
dotnet run --project src/GameDevManager.Web      # startet die Blazor-Server-App
dotnet tool restore                              # stellt dotnet-ef (lokales Tool-Manifest) her
```

EF-Core-Befehle laufen über das lokale Tool `dotnet ef` (Version im [dotnet-tools.json](dotnet-tools.json) gepinnt). Migrationen werden **pro Provider** in das jeweilige Migrations-Projekt erzeugt, mit Web als Startup-Projekt — für jede Schemaänderung alle vier Provider durchlaufen.

Wichtig: Der DbContext nimmt seinen Provider zur Entwurfszeit aus derselben Konfiguration wie zur Laufzeit. Ohne Umgebungsvariable erzeugt jeder Lauf SQLite-SQL (der Standard aus `appsettings.json`) — auch im SqlServer-Projekt. `Database__Provider` muss deshalb je Lauf mitgegeben werden:

```powershell
$env:Database__Provider="SqlServer"; dotnet ef migrations add <Name> --project src/GameDevManager.Data.Migrations.SqlServer --startup-project src/GameDevManager.Web
# ebenso mit PostgreSql, MySql, Sqlite
```

Testprojekte existieren noch nicht; sobald vorhanden: `dotnet test`.

## Architektur

Klassische Schichtung mit einer Besonderheit: Die EF-Migrationen sind pro Datenbank-Provider in eigene Assemblies ausgelagert, weil das Tool self-hosted ist und Nutzer ihre Datenbank frei wählen können (SQL Server, PostgreSQL, MySQL, SQLite).

```
GameDevManager.Domain    ← Entitäten, keine Abhängigkeiten
GameDevManager.Data      ← EF Core, DbContext; referenziert Domain, bündelt alle vier Provider-Pakete
GameDevManager.Data.Migrations.{SqlServer|PostgreSql|MySql|Sqlite}
                         ← je ein Projekt nur für die Migrationen des jeweiligen Providers
GameDevManager.Web       ← Blazor Server (net10.0) + MudBlazor; referenziert Data und alle Migrations-Projekte
```

Provider und Connection String kommen aus `appsettings.json` (`Database:Provider` + `ConnectionStrings:{Provider}`); die Verdrahtung inklusive `MigrationsAssembly` steht in [DatabaseServiceExtensions.cs](src/GameDevManager.Data/DatabaseServiceExtensions.cs). Der DbContext wird als **Factory** registriert (Blazor Server) — Dienste holen sich je Aufruf einen eigenen Kontext und halten keinen Zustand.

### Arten- und Feldsystem (der modulübergreifende Kern)

Das Konzept verlangt in fast jedem Modul benutzerdefinierte Arten mit eigenen Feldern plus individuelle Felder pro Entität. Das ist **einmal generisch** gebaut und wird von jedem Modul mitbenutzt — neue Module definieren keine eigene Feldmechanik:

- `ContentType` — eine Art innerhalb eines Moduls (Item-Art „Waffe“, später NPC-Art „Händler“). Gehalten je Projekt und `ModuleKey`.
- `FieldDefinition` — ein Feld. Entweder `ContentTypeId` gesetzt (gilt für alle Entitäten der Art) **oder** `OwnerEntityId` (gilt nur für diese eine Entität, für exotische Items). Nie beides.
- `FieldValue` — der Wert, adressiert über `OwnerEntityId` + `OwnerModuleKey`. Getrennte Wertspalten je Datentyp; `NumberValue` ist bewusst `double`, weil SQLite keinen Dezimaltyp kennt.
- `ContentEntity` — Basis der Modul-Entitäten (`Item` ist die erste). Jedes Modul bekommt seine **eigene Tabelle**, weil die Module später sehr verschiedene Beziehungen brauchen (Rezept-Zutaten, Händler-Angebote, Karten-Marker).

`FieldValue` und individuelle `FieldDefinition`s haben bewusst **keinen** Fremdschlüssel auf die Entität — sie sind modulübergreifend und referenzieren über die GUID, so wie das Konzept es für alles vorsieht. Beim Löschen einer Entität muss der Modul-Service deshalb selbst aufräumen (siehe `ItemService.DeleteItemAsync`).

Die Referenzansicht („Find All References“) lebt in `ReferenceService`. Sie wertet heute die Referenz-Felder aus; Module mit eigenen Verknüpfungstabellen tragen ihre Abfrage dort in den `switch` ein — genauso wie `ContentTypeService.CountUsagesAsync` und `ReferenceService.GetEntitiesAsync`. **Diese drei `switch`-Blöcke sind die Stellen, die ein neues Modul anfassen muss.**

Ein neues Modul umsetzen heißt also: Entität von `ContentEntity` ableiten, in `GameDevManagerDbContext.OnModelCreating` mit `ConfigureContentEntity<T>` registrieren, einen Service nach dem Muster von `ItemService` schreiben, die drei `switch`-Blöcke ergänzen, Seiten unter `Components/Pages/<Modul>/` anlegen und in `ModuleRegistry` `Implemented: true` setzen.

Der Domain-Enum heißt `ContentFieldType` und nicht `FieldType` — letzteres kollidiert mit `MudBlazor.FieldType` und macht jede Razor-Datei mehrdeutig.

Alle Inhalte hängen an einem `GameProject`. Eine Projektauswahl gibt es noch nicht; bis dahin liefert `ProjectContext` das beim Start angelegte Standardprojekt. Wenn das Projekt-Modul kommt, ändert sich nur dieser Dienst.

## Design

Anthrazit als Grundton, Gelb (`#FFC300`) als einziger kräftiger Akzent, durchgehend eckige Kanten (Border-Radius 0), Inter als UI-Schrift und JetBrains Mono für GUIDs/IDs. Ebenen trennen sich über 1px-Linien, nicht über Schlagschatten — Cards deshalb mit `Elevation="0"`.

- **Alle Farben und Typo-Stufen stehen in [GameDevManagerTheme.cs](src/GameDevManager.Web/Theme/GameDevManagerTheme.cs)** und werden dem `MudThemeProvider` in `MainLayout.razor` übergeben. In Komponenten nie Hex-Werte schreiben, sondern `Color.Primary` bzw. `var(--mud-palette-…)` verwenden — sonst bricht die Hell/Dunkel-Umschaltung.
- `Color.Primary` ist das Akzentgelb, `Color.Warning` bewusst orange (sonst nicht vom Akzent unterscheidbar).
- [app.css](src/GameDevManager.Web/wwwroot/app.css) enthält nur, was das Theme nicht abdeckt: die `@font-face`-Regeln und Ausnahmen für MudBlazor-Komponenten, die fest abrunden (Icon-Buttons, Chips, Avatare, …).
- Die Schriften liegen lokal unter `wwwroot/fonts` — das Tool wird self-hosted betrieben und darf keine externen CDNs brauchen.

## Fachliche Leitplanken (aus dem Konzept)

Diese Prinzipien gelten modulübergreifend und müssen im Entitätenmodell von Anfang an mitgedacht werden:

- **Eigene Arten und Felder:** In fast jedem Modul kann der Nutzer eigene Arten (z. B. Item-Art „Waffe") mit selbst definierten Feldern anlegen; einzelne Entitäten können zusätzlich individuelle Felder bekommen. Das Schema ist also nutzerdefiniert, nicht fest kodiert.
- **GUID-Referenzen:** Entitäten referenzieren einander ausschließlich über GUIDs. Jede Entität hat eine Referenzansicht („Find All References"): wo wird sie in Händlern, Quests, Rezepten, Loot-Tables usw. verwendet.
- **Zentrales Bedingungssystem:** Bedingungen (für Quests, Dialoge, Shop-Angebote, …) laufen über ein gemeinsames System, das in allen Modulen identisch funktioniert.
- **Versionierte, diffbare Exporte:** Exporte müssen nachvollziehbar machen, was ein Content-Update verändert hat. Änderungen im Tool werden pro angemeldetem Benutzer protokolliert (Changelog).

Roadmap-Reihenfolge: Kern-Architektur (Entitätenmodell, Arten/Felder, GUID-Referenzen, Bedingungssystem) → Dashboard, Items, Asset-Bibliothek → Crafting, NPCs, Loot-Tables, Karten → Dialoge, Story, Quests, Events → Import/Export.
