# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Projektüberblick

GameDevManager ist ein selbst gehostetes Verwaltungstool für Indie-Spieleentwickler: ein strukturiertes Wiki für Spielinhalte (Items, NPCs, Quests, Dialoge, Karten, …) mit späterem Export in Game Engines (Unity, Unreal, Godot oder JSON/ZIP).

**Status: Kern steht, sechs Module umgesetzt.** Datenbankanbindung, Theme, Modul-Registry, das Arten-/Feldsystem, die globale Suche, Items, die Asset-/Sprite-Bibliothek, Crafting, Währungen, NPCs und Loot-Tables sind fertig; alle übrigen Module landen noch auf der Platzhalterseite `ModulePage.razor`. Template-Reste (`Class1.cs` in Domain/Data, Counter-/Weather-Seiten, leere `NavMenu.razor`) sind noch da und können weg. Testprojekte gibt es nicht. Die fachliche Quelle der Wahrheit ist [knowledge/Konzept.md](knowledge/Konzept.md) — dort sind alle Module und Anforderungen im Detail beschrieben; die README fasst sie zusammen.

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

### Ein Modul überall bekannt machen: `IModuleEntitySource`

Vier Dienste arbeiten modulübergreifend — die Referenzansicht („Find All References“), die Referenz-Auswahlfelder, die Verwendungszählung der Arten und die globale Suche. Sie fragen **nicht** jeweils einen eigenen `switch` ab, sondern alle registrierten `IModuleEntitySource`. Je Modul gibt es genau eine solche Klasse in [ModuleEntitySources.cs](src/GameDevManager.Data/Services/ModuleEntitySources.cs); für Entitäten auf `ContentEntity`-Basis erbt sie von `ModuleEntitySource<T>` und braucht nur den `DbSet`-Zugriff und die Abbildung auf Suchtreffer.

Verweist ein Modul über **eigene Spalten** auf fremde Entitäten (Rezept-Zutaten, später Händler-Angebote und Loot-Einträge), überschreibt es zusätzlich `FindReferencesAsync`. Diese Methode ist bewusst virtuell in der Basisklasse und keine Standardimplementierung an der Schnittstelle: Die Zuordnung zur Schnittstelle entsteht in der Basisklasse, eine gleichnamige Methode in einer abgeleiteten Klasse würde sie nicht ersetzen und stillschweigend nie laufen.

Ein neues Modul umsetzen heißt also:

1. Entität von `ContentEntity` ableiten und in `GameDevManagerDbContext.OnModelCreating` mit `ConfigureContentEntity<T>` registrieren.
2. Einen Service nach dem Muster von `ItemService`/`CurrencyService` schreiben. Die Feldmechanik kommt komplett aus `ContentFields` (laden, Pflichtfelder prüfen, Werte in denselben `SaveChanges` einreihen, beim Löschen aufräumen) — nicht neu bauen. Beim Löschen einer Entität `AssetService.DeleteForOwnerAsync` aufrufen, sonst bleiben Sprites und Dateien liegen.
3. Eine `ModuleEntitySource<T>` anlegen und in `AddGameDevManagerContentServices` registrieren. Damit ist das Modul in Referenzansicht, Auswahlfeldern, Arten-Zählung und Suche auf einmal da.
4. Seiten unter `Components/Pages/<Modul>/` anlegen. Die Arten-Verwaltung ist eine Zeile (`<ContentTypeManager ModuleKey="…" />`), die Feldabschnitte der Maske ebenso (`<ContentFieldsPanel TEntity="…" …/>`).
5. In `ModuleRegistry` `Implemented: true` setzen.

Das Währungsmodul ist nach genau diesem Ablauf entstanden und der kürzeste Beleg, dass er trägt.

**EF-Fallstrick bei Kind-Sammlungen** (Rezept-Zutaten, später Händler-Angebote, Loot-Einträge): Neue Kinder an einem **bestehenden** Elterndatensatz immer über `db.Set<T>().Add(...)` einfügen, nie über die Navigationsliste. Die Entitäten bringen ihre GUID schon mit, und EF hält sie beim Anhängen sonst für vorhandene Datensätze und erzeugt ein `UPDATE` auf eine Zeile, die es noch nicht gibt. Entfernt wird umgekehrt nur über die Navigationsliste — der Fremdschlüssel ist pflicht, EF löscht die Waise dadurch von selbst; zusätzlich `Remove` aufzurufen erzeugt einen zweiten `DELETE`.

### Assets

Dateien liegen **nicht** in der Datenbank, sondern im Dateisystem unter dem in `Assets:StoragePath` konfigurierten Pfad (relativ zum Anwendungsverzeichnis, Standard `assets/`). In der Datenbank steht nur der `StorageKey`. Das hält das Verhalten über alle vier Provider gleich und die Datenbanksicherungen klein.

- Ein `Asset` hängt wie die Feldwerte über `OwnerEntityId` + `OwnerModuleKey` an einer Entität — ohne Fremdschlüssel. Ohne Besitzer ist es ein **Werkzeug-Asset** (Karten-Marker, Platzhalter) und zugleich der Zustand frisch hochgeladener, noch nicht zugeordneter Dateien.
- Je Entität ist höchstens ein Asset `IsPrimary` — das Icon, das die Modul-Listen zeigen. Der `AssetService` hält das nach: Wird das Icon gelöscht oder auf eine andere Entität umgehängt, rückt ein übrig gebliebenes Sprite nach.
- Ausgeliefert wird über den Endpunkt `/assets/{id}` in `Program.cs`, nicht über ein statisches Verzeichnis. Der Endpunkt setzt bewusst `nosniff` und eine enge CSP, weil hier vom Nutzer hochgeladene Dateien zurückgehen und SVG Skripte enthalten kann.
- `ImageDimensionReader` liest Breite und Höhe aus dem Dateikopf (PNG, GIF, BMP, JPEG, WebP) ohne Bildbibliothek — für unbekannte Formate wie SVG bleiben die Maße leer.
- `AssetTag` ist absichtlich auf Assets beschränkt. Das geplante Tag-Modul vergibt Tags modulübergreifend und wird diese Stichwörter voraussichtlich ablösen.

In den Bearbeitungsmasken der Module wird `<AssetSpritePanel ModuleKey="…" EntityId="…" Disabled="@istNeu" />` eingebunden; die Listen zeigen das Icon über `<AssetThumbnail AssetId="…" />`.

### Crafting

Das Rezept trägt nur, was fachlich unumgänglich ist: Ergebnis-Item, Ausbeute und Zutaten mit Mengen. Herstellungsdauer, Werkbank oder Mindestlevel definiert der Nutzer als Felder an der Rezept-Art — dieselbe Regel wie bei den Items. Ergebnis und Zutaten sind reine GUID-Referenzen auf Items, ohne Fremdschlüssel über die Modulgrenze.

`CraftingService` lädt für Bäume und Zyklenprüfung den gesamten Rezeptbestand eines Projekts einmal und löst ihn im Speicher auf (`CraftingGraph`) — bei der Größenordnung eines Spielprojekts deutlich billiger als eine Abfrage je Ebene. Zwei Dinge, die daran hängen:

- **Zyklen** („zyklische Rezepte“ aus der Health-Check-Liste des Konzepts) findet `FindCyclesAsync` per Tiefensuche; der Baumaufbau bricht an einem wiederkehrenden Item ab und markiert den Knoten, statt endlos zu laufen.
- **`SummarizeBaseCost`** rechnet einen Baum auf seine Grundstoffe herunter und verrechnet dabei die Rezeptausbeuten, je Stufe aufgerundet: Ein Rezept, das vier Stäbe liefert, wird für sechs Stäbe zweimal ausgeführt.

Gibt es mehrere Rezepte für dasselbe Item, klappt der Baum das erste auf und weist die übrigen als Anzahl aus.

### Währungen

Beliebig viele nebeneinander; Händler nehmen später eine davon entgegen. Strukturell trägt die Währung nur ihr `Symbol` — es steht dort und nicht in einem benutzerdefinierten Feld, weil jede Ansicht, die einen Preis zeigt, es zuverlässig finden muss. Wechselkurse und Ähnliches sind Felder der Währungs-Art. Namen sind je Projekt eindeutig, sonst wären zwei Währungen in einer Preisangabe nicht auseinanderzuhalten.

### NPCs

NPCs und Mobs liegen laut Konzept im selben Modul und unterscheiden sich über `NpcKind` — eine echte Spalte und keine Art, weil das Tool danach filtert und später Loot-Tables und Spawns daran hängen. Die Rollen sind zwei Schalter (`IsTrader`, `IsQuestGiver`), womit „Händler, Quest, beides oder gar nichts“ direkt abgebildet ist.

Das Warenangebot (`TraderOffer`) ist die zweite Kind-Sammlung nach den Rezept-Zutaten und folgt demselben Muster inklusive des EF-Fallstricks oben. Je Posten: Item, Währung, Verkaufs- und Ankaufspreis, Bestand (`null` = unbegrenzt) und Auffüllzeit. Ein Posten ohne Preis ist zulässig — ein Händler, der etwas führt, aber nicht handelt, ist ein gültiger Fall. Ein Preis **ohne** Währung wird abgelehnt, weil die Zahl dann nicht zu deuten wäre.

Zwei Konzept-Anforderungen dieses Moduls fehlen noch, weil ihre Grundlage nicht steht: **Spawn-Orte** (Karten-Modul) und die **Verfügbarkeitsbedingungen** von Shops (Bedingungssystem). Der NPC-Editor weist in der Seitenleiste darauf hin; bis dahin lassen sich solche Angaben als Felder an der NPC-Art erfassen. Beim Bau dieser Module hier nachziehen.

### Loot-Tables

Einträge sind Item, Wahrscheinlichkeit in Prozent und eine Mengenspanne. Dasselbe Item darf mehrfach vorkommen — „zu 50 % eine Münze, zu 5 % gleich zwanzig“ ist ein üblicher Fall und anders als bei Rezept-Zutaten oder Händler-Posten kein Versehen.

`LootRollMode` unterscheidet zwei Auswertungsverfahren, weil beide in Spielen üblich sind und die Prozentzahlen je nach Verfahren etwas anderes bedeuten:

- **`Independent`** — jeder Eintrag wird einzeln gewürfelt. Eine Summe über 100 % ist normal (drei Dinge zu je 80 % fallen oft gemeinsam).
- **`SinglePick`** — höchstens ein Eintrag fällt, alle teilen sich einen Wurf. Über 100 % hinaus wären die hinteren unerreichbar.

Der Health Check „Loot-Wahrscheinlichkeiten über 100 %“ aus dem Konzept gilt deshalb **nur** für `SinglePick` (`LootService.FindOverfullTablesAsync`, angezeigt auf der Listenseite). Er blockt bewusst nicht das Speichern: Im Konzept steht er unter den Health Checks, also unter „nachschauen“ und nicht unter „verboten“ — sonst ließe sich eine Tabelle beim Umbauen zwischendurch nicht sichern.

NPCs verweisen über `Npc.LootTableId` auf eine Tabelle. Beim Löschen einer Tabelle setzt `LootService` diese Verweise auf `null`, sonst zeigten NPCs auf etwas, das es nicht mehr gibt.

### Globale Suche

`SearchService` durchsucht über die `IModuleEntitySource` alle Module plus Assets und Arten. Gesucht wird über kleingeschriebene Namen (`ToLower().Contains(...)`) statt über `LIKE` — das übersetzt sich über alle vier Provider gleich und hängt nicht an der Sortierfolge der Datenbank. Eine eingefügte GUID wird direkt aufgelöst. Das Suchfeld sitzt rechts in der Appbar ([GlobalSearch.razor](src/GameDevManager.Web/Components/Content/GlobalSearch.razor)).

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
