# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Projektüberblick

GameDevManager ist ein selbst gehostetes Verwaltungstool für Indie-Spieleentwickler: ein strukturiertes Wiki für Spielinhalte (Items, NPCs, Quests, Dialoge, Karten, …) mit späterem Export in Game Engines (Unity, Unreal, Godot oder JSON/ZIP).

**Status: Alle Module umgesetzt, die offenen Konzept-Punkte abgearbeitet — inklusive der Feature-Welle vom 14.08.2026** (NPC-Beziehungen samt frei definierbaren Beziehungsarten, Einzigartig-Schalter, Vorlieben/Persönlichkeit/Wesenszüge; Karten-Ebenen; erweiterte Story-Abschnitte mit Szenen-Verknüpfungen und Drag&Drop-Reihenfolge; die Werkzeug-Module **ToDo** (Kanban), **Whiteboard** und **Verbindungen** (NPC-Graph); der Spieler-Umbau — das Modul `player` heißt in der Oberfläche „Skilltrees“, Spielerfiguren sind überführbare NPCs — und globale Tastatur-Shortcuts, siehe „Neuere Erweiterungen“ unten; dazu die einstellbare Modulleiste samt Überlauf, siehe „Topbar“). Die Kern-Architektur der Roadmap steht ganz: Entitätenmodell, Arten/Felder (inklusive **Unterarten mit Feldvererbung**), GUID-Referenzen **und das Bedingungssystem**. Sämtliche Module der Registry haben eine eigene Oberfläche — von Items über NPCs, Fraktionen, Diplomatie, Story, Quests und Events bis zu Spieler/Skilltrees, Klassen, Effekten, Achievements, Sammelobjekten, Tags, Audio, Cutscenes und der Statistik-Seite mit allen Health Checks; die Platzhalterseite `ModulePage.razor` wird nicht mehr angesteuert. **Import und Export sind abgeschlossen**: Export als JSON/ZIP inkl. Assets (wahlweise im Ordner-Layout von Unity, Unreal oder Godot), Import des Export-ZIPs sowie aufbewahrte Exportstände mit Diff-Ansicht — siehe Abschnitt „Import & Export“. **Projektauswahl und konfigurierbares Dashboard sind umgesetzt** (siehe Abschnitt „Projekte & Dashboard“), ebenso ein erstes Testprojekt unter `tests/GameDevManager.Tests` (`dotnet test`). Kopieren gibt es auf beiden Ebenen — ganze Projekte und einzelne Entitäten, siehe „Duplizieren“. Dazu kommen **Benutzeranmeldung mit Berechtigungen je Benutzer und Änderungsprotokoll** (siehe „Anmeldung & Änderungsprotokoll“), der **Freischaltungs-Graph**, das **Welt-Modul** (Tageszeit/Wetter/Biome) und der Feldtyp **Formel/Kurve**. Aus dem Konzept ist damit nichts mehr offen; was bleibt, sind die Ideen in [knowledge/ToDo.md](knowledge/ToDo.md). Die fachliche Quelle der Wahrheit ist [knowledge/Konzept.md](knowledge/Konzept.md) — dort sind alle Module und Anforderungen im Detail beschrieben; die README fasst sie zusammen.

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

`dotnet test` führt das Testprojekt [tests/GameDevManager.Tests](tests/GameDevManager.Tests) aus: echte Dienste aus demselben DI-Aufbau wie die Anwendung, gegen SQLite im Speicher (`TestDatabase`) — keine externen Abhängigkeiten. Getestet sind die JSON-Regeln des Exportformats, der Crafting-Graph (Zyklen, Grundstoff-Rechnung), die Health Checks (Bedingungen, Dialog-Sackgassen, Loot über 100 %), die Stichprobe des Startscreens, die Bänder des Dashboards (modulübergreifendes „Weiterarbeiten“, Zustandszusammenfassung, gespeicherte Anordnung), das Duplizieren von Projekten und Entitäten samt Sicherheitsnetz, die erweiterte Suche, der Dialog-Graph, die Feldvererbung der Unterarten und die Polygon-Gebiete der Karten (kanonische Punktliste, Validierung, Duplizieren). Dazu der Ausdrucksrechner der Kurven (Rechenreihenfolge, kaputte Formeln, Wertetabelle), das Welt-Modul, der Freischaltungs-Graph samt Ringen, das Änderungsprotokoll (Urheber, geänderte Eigenschaften, Sammeleintrag beim Import, Blättern in der Geschichte einer Entität), Passwörter, Passwortrichtlinien und Benutzerverwaltung, die Berechtigungen je Benutzer (Schreibschutz, Modul-Freigaben, Export/Import, Verwalter-Absicherung) sowie die Schreibkonflikt-Erkennung. Der Harness legt je Test auch ein Verzeichnis für Exportstände an (`ExportStorageOptions`, über `TestDatabase.ExportOptions` umstellbar) und räumt es wieder ab — der ersetzende Import und das Projektlöschen schreiben dorthin; die Aufbewahrung der Stände (Obergrenze, Höchstalter, der stets bleibende jüngste Stand, fremde Dateien) prüft `ExportRetentionTests`.

## Architektur

Klassische Schichtung mit einer Besonderheit: Die EF-Migrationen sind pro Datenbank-Provider in eigene Assemblies ausgelagert, weil das Tool self-hosted ist und Nutzer ihre Datenbank frei wählen können (SQL Server, PostgreSQL, MySQL, SQLite).

```
GameDevManager.Domain    ← Entitäten, keine Abhängigkeiten
GameDevManager.Data      ← EF Core, DbContext; referenziert Domain, bündelt alle vier Provider-Pakete
GameDevManager.Data.Migrations.{SqlServer|PostgreSql|MySql|Sqlite}
                         ← je ein Projekt nur für die Migrationen des jeweiligen Providers
GameDevManager.Web       ← Blazor Server (net10.0) + MudBlazor; referenziert Data und alle Migrations-Projekte
GameDevManager.Tests     ← xunit (unter tests/); echte Dienste gegen SQLite im Speicher
```

Provider und Connection String kommen aus `appsettings.json` (`Database:Provider` + `ConnectionStrings:{Provider}`); die Verdrahtung inklusive `MigrationsAssembly` steht in [DatabaseServiceExtensions.cs](src/GameDevManager.Data/DatabaseServiceExtensions.cs). Der DbContext wird als **Factory** registriert (Blazor Server) — Dienste holen sich je Aufruf einen eigenen Kontext und halten keinen Zustand. Die Factory selbst ist **scoped** und nicht, wie sonst üblich, Singleton: Sie zieht den `ChangeLogInterceptor`, und der muss wissen, wer gerade angemeldet ist. Wer sie beim Start aus dem Wurzel-Container holt, braucht dafür einen Scope (siehe `Program.cs`).

### Arten- und Feldsystem (der modulübergreifende Kern)

Das Konzept verlangt in fast jedem Modul benutzerdefinierte Arten mit eigenen Feldern plus individuelle Felder pro Entität. Das ist **einmal generisch** gebaut und wird von jedem Modul mitbenutzt — neue Module definieren keine eigene Feldmechanik:

- `ContentType` — eine Art innerhalb eines Moduls (Item-Art „Waffe“, später NPC-Art „Händler“). Gehalten je Projekt und `ModuleKey`. Über `ParentId` können Arten ineinander stecken: „Waffe“ mit den Unterarten „Nahkampf“, „Fernkampf“, „Magie“. Eine Unterart **erbt** die Felder ihrer Eltern-Arten — deshalb hängt die Hierarchie hier und nicht am Tag-Modul, Tags tragen keine Felder. Die geerbten Felder trägt `ContentTypeService.GetTypesAsync` in `InheritedFields` zusammen; die Eigenschaft ist **nicht persistiert und nicht im Export** (`entity.Ignore` plus Ausnahme im `JsonTypeInfo`-Modifier), die Felder stehen an ihrer Eltern-Art. `ContentEditContext.TypeFields` stellt geerbte vor eigene Felder, womit Masken, Pflichtfeldprüfung und Wertespeicherung von selbst stimmen. Abgelehnt werden Ringe, eine Eltern-Art aus fremdem Modul oder Projekt und derselbe Feldname zweimal in einer Vererbungslinie; eine Art mit Unterarten lässt sich nicht löschen.
- `FieldDefinition` — ein Feld. Entweder `ContentTypeId` gesetzt (gilt für alle Entitäten der Art) **oder** `OwnerEntityId` (gilt nur für diese eine Entität, für exotische Items). Nie beides. Neben dem Pflicht-Schalter trägt es **`IsTagList`** — siehe „Stichwortfelder“ unten.
- `FieldValue` — der Wert, adressiert über `OwnerEntityId` + `OwnerModuleKey`. Getrennte Wertspalten je Datentyp; `NumberValue` ist bewusst `double`, weil SQLite keinen Dezimaltyp kennt.
- `ContentEntity` — Basis der Modul-Entitäten (`Item` ist die erste). Jedes Modul bekommt seine **eigene Tabelle**, weil die Module später sehr verschiedene Beziehungen brauchen (Rezept-Zutaten, Händler-Angebote, Karten-Marker).

`FieldValue` und individuelle `FieldDefinition`s haben bewusst **keinen** Fremdschlüssel auf die Entität — sie sind modulübergreifend und referenzieren über die GUID, so wie das Konzept es für alles vorsieht. Beim Löschen einer Entität muss der Modul-Service deshalb selbst aufräumen (siehe `ItemService.DeleteItemAsync`).

#### Stichwortfelder

`FieldDefinition.IsTagList` macht aus einem Textfeld eine **Liste von Stichwörtern** — die Elemente eines Zaubers, die Schadensarten einer Waffe. Erfasst wird sie mit dem `ChipListInput` (tippen, mit Komma oder Enter bestätigen), gespeichert als kanonische, kommagetrennte Textspalte in `FieldValue.TextValue`; die Lese- und Schreibregeln stehen in [KeywordList.cs](src/GameDevManager.Domain/Entities/KeywordList.cs) und gelten ebenso für Vorlieben und Persönlichkeit eines NPCs.

Vier Entscheidungen dahinter:

- **Ein Schalter am Textfeld statt eines eigenen `ContentFieldType`.** Der Wert bleibt Text und damit über `SearchFieldValuesAsync` auffindbar, und ein bestehendes Textfeld lässt sich ohne Verlust umstellen — ein Typwechsel löscht die Werte (`SaveFieldAsync`). In der Maske steht der Schalter deshalb neben „Pflichtfeld“ und nicht in der Typ-Auswahl.
- **Nur `Text`.** Alle anderen Typen tragen je einen Wert. Der Typwechsel löscht den Schalter in der Datenschicht (`ResolveTagList`) und nicht bloß in der Maske, sonst wirkte er beim Zurückwechseln unbemerkt weiter. `FieldDefinition.IsKeywordField` ist die Frage, die jede Anzeige stellt — Schalter **und** Typ.
- **Kanonisiert wird vor der Leer-Prüfung** (`ContentFields.Canonicalize`, aufgerufen in `ValidateRequired` und `StageValuesAsync`): Eine Eingabe aus lauter Kommas trägt Text, aber kein Stichwort — sonst gälte ein Pflichtfeld als ausgefüllt und in der Datenbank bliebe eine Zeile ohne Inhalt stehen.
- **Keine eigene Tabelle.** Als Textspalte geht die Liste ohne Zutun durch Export, Import, Duplizieren und die Feldvererbung der Unterarten — dieselbe Überlegung wie beim Feldtyp „Formel/Kurve“.

### Ein Modul überall bekannt machen: `IModuleEntitySource`

Sieben Dienste arbeiten modulübergreifend — die Referenzansicht („Find All References“), die Referenz-Auswahlfelder, die Verwendungszählung der Arten, die globale Suche, das Duplizieren einzelner Entitäten (`EntityDuplicationService`), der Inhaltsregen des Startscreens (`StartScreenService`) und das „Weiterarbeiten“ des Dashboards (`DashboardOverviewService`). Sie fragen **nicht** jeweils einen eigenen `switch` ab, sondern alle registrierten `IModuleEntitySource`. Je Modul gibt es genau eine solche Klasse in [ModuleEntitySources.cs](src/GameDevManager.Data/Services/ModuleEntitySources.cs); für Entitäten auf `ContentEntity`-Basis erbt sie von `ModuleEntitySource<T>` und braucht nur den `DbSet`-Zugriff und die Abbildung auf Suchtreffer.

Verweist ein Modul über **eigene Spalten** auf fremde Entitäten (Rezept-Zutaten, später Händler-Angebote und Loot-Einträge), überschreibt es zusätzlich `FindReferencesAsync`. Dasselbe gilt für `GetEntitiesAsync` (Seltenheiten sortieren nach Rang), `SearchAsync` (Dialoge suchen zusätzlich in ihren Zeilen) und `CanDuplicate` (Diplomatie erlaubt kein Kopieren). Diese Mitglieder sind bewusst virtuell in der Basisklasse und keine Standardimplementierung an der Schnittstelle: Die Zuordnung zur Schnittstelle entsteht in der Basisklasse, eine gleichnamige Methode in einer abgeleiteten Klasse würde sie nicht ersetzen und stillschweigend nie laufen.

Ein neues Modul umsetzen heißt also:

1. Entität von `ContentEntity` ableiten und in `GameDevManagerDbContext.OnModelCreating` mit `ConfigureContentEntity<T>` registrieren.
2. Einen Service nach dem Muster von `ItemService`/`CurrencyService` schreiben. Die Feldmechanik kommt komplett aus `ContentFields` (laden, Pflichtfelder prüfen, Werte in denselben `SaveChanges` einreihen, beim Löschen aufräumen) — nicht neu bauen. Beim Löschen einer Entität `AssetService.DeleteForOwnerAsync` aufrufen, sonst bleiben Sprites und Dateien liegen. Ebenso `ChangeLog.RecordDeletionAsync` unmittelbar vor dem `ExecuteDeleteAsync` — das ist die einzige Stelle, an der das Änderungsprotokoll je Modul etwas braucht; alles Übrige schreibt der `ChangeLogInterceptor` von selbst.
3. Eine `ModuleEntitySource<T>` anlegen und in `AddGameDevManagerContentServices` registrieren. Damit ist das Modul in Referenzansicht, Auswahlfeldern, Arten-Zählung, Suche, Duplizieren und dem Startscreen auf einmal da.
4. Seiten unter `Components/Pages/<Modul>/` anlegen. Die Arten-Verwaltung ist eine Zeile (`<ContentTypeManager ModuleKey="…" />`), die Feldabschnitte der Maske ebenso (`<ContentFieldsPanel TEntity="…" …/>`), das Kopieren in der Liste ebenfalls (`<EntityDuplicateButton ModuleKey="@Module.Id" …/>`).
5. In `ModuleRegistry` `Implemented: true` setzen — und Name plus Beschreibung als `<key>_Name` / `<key>_Description` in [ModuleLabels.resx](src/GameDevManager.Web/Services/ModuleLabels.resx) eintragen.
6. Je Seite eine `.resx` daneben legen (siehe „Texte“).

Das Währungsmodul ist nach genau diesem Ablauf entstanden und der kürzeste Beleg, dass er trägt.

### Texte

**Kein sichtbarer Text steht im Code.** Jede Seite und jede Komponente hat eine gleichnamige `.resx` direkt daneben (kein `ResourcesPath`) und holt sich ihre Texte über `@inject IStringLocalizer<DieseKomponente> L` als `@L["Schlüssel"]`; Platzhalter als `L["Schlüssel", wert]`. Das gilt auch für das, was nicht im Markup steht: Löschdialoge, Snackbar-Meldungen und `PageHeading`.

Drei Stellen brauchen einen Umweg, weil ein Localizer dort nicht direkt hinkommt:

- **Statische Klassen und Feldinitialisierer.** `ModuleDefinition` trägt deshalb **weder Name noch Beschreibung** — die Registry ist statisch. Beides kommt aus [ModuleLabels.cs](src/GameDevManager.Web/Services/ModuleLabels.cs) (`Modules.Name(module)`), ebenso wie `ConditionLabels` und `FieldTypeLabels` Dienste statt statischer Klassen sind. Ihre Icon-Methoden bleiben `static`, Icons sind sprachunabhängig.
- **Generische Komponenten.** `IStringLocalizer<Komponente<T>>` sucht unter dem gemangelten Typnamen (`` Komponente`1 ``) und findet die `.resx` nicht — die Schlüssel stünden roh in der Oberfläche. `ContentFieldsPanel` hängt seinen Localizer deshalb an den nicht-generischen Marker `ContentFieldsPanelText`.
- **Parameter mit Vorgabetext.** Ein Feldinitialisierer (`public string Hint { get; set; } = "…"`) kennt den Localizer noch nicht. Solche Parameter sind `string?` und werden über eine `…Text`-Eigenschaft aufgelöst (siehe `ContentTypeManager.EmptyHintText`).

**Die Datenschicht hat eigene Texte**, weil sie selbst prüft und die Oberfläche die Meldung nur durchreicht (`Snackbar.Add(ex.Message, …)`): [DataMessages.resx](src/GameDevManager.Data/DataMessages.resx), bezogen über `IStringLocalizer<DataMessages> messages` im Primärkonstruktor. In einer LINQ-Abfrage den Text **vorher in eine lokale Variable ziehen** — einen Indexer-Aufruf kann EF nicht übersetzen. Ausgenommen sind Start- und Konfigurationsfehler (`InvalidOperationException` in `DatabaseServiceExtensions` und `FileSystemAssetStorage`): Die landen im Log, nicht in der Oberfläche.

**EF-Fallstrick bei Kind-Sammlungen** (Rezept-Zutaten, später Händler-Angebote, Loot-Einträge): Neue Kinder an einem **bestehenden** Elterndatensatz immer über `db.Set<T>().Add(...)` einfügen, nie über die Navigationsliste. Die Entitäten bringen ihre GUID schon mit, und EF hält sie beim Anhängen sonst für vorhandene Datensätze und erzeugt ein `UPDATE` auf eine Zeile, die es noch nicht gibt. Entfernt wird umgekehrt nur über die Navigationsliste — der Fremdschlüssel ist pflicht, EF löscht die Waise dadurch von selbst; zusätzlich `Remove` aufzurufen erzeugt einen zweiten `DELETE`.

### Bedingungssystem

Das Konzept verlangt „ein einheitliches System, welches über alle Module hinweg verknüpfbar ist“. Es funktioniert nach demselben Prinzip wie die Feldwerte: Ein `ConditionSet` hängt über eine GUID an seinem Besitzer, nicht über einen Fremdschlüssel.

- **Besitzer** kann eine ganze Entität sein (ein NPC) **oder ein Teilobjekt mit eigener GUID** (ein einzelner Händler-Posten). Genau das deckt „manche Shops und teilweise auch nur Items aus einem Shop“ ab.
- **`Slot`** unterscheidet mehrere Bedingungssätze am selben Besitzer — siehe `ConditionSlots`. Ein NPC hat heute `Shop`; kommt später „erscheint, wenn …“ dazu, braucht das keine Umstellung.
- **`ConditionKind`** legt fest, welche Spalten tragen: mengenbezogene Arten nutzen Operator und Zahl, Ja/Nein-Arten den booleschen Wert, Bezüge auf andere Entitäten eine GUID. `Custom` fängt alles ab, was das Tool noch nicht kennt.
- **Das Zielmodul steht meist an der Art** (`ExpectedTargetModule`) — „hat Item“ zeigt auf Items, „ist Tageszeit“ auf die Weltzustände. Nur `Unlocked` lässt es den Nutzer wählen (`ChoosesTargetModule`), weil sich alles freischalten lässt; siehe „Freischaltungen“.
- Ein **leerer Satz wird gelöscht statt gespeichert** — „keine Bedingung“ soll keine Zeile hinterlassen.

`FindProblemsAsync` ist der Health Check „unerfüllbare Bedingungen“. Er meldet nur, was sich ohne Kenntnis des laufenden Spiels sicher feststellen lässt: Ziele, die es nicht mehr gibt, und Widersprüche in einem „alle müssen zutreffen“-Satz (eine Menge gleichzeitig über und unter einer Grenze, ein Schalter gleichzeitig gesetzt und nicht gesetzt). Ziele in noch nicht umgesetzten Modulen gelten ausdrücklich **nicht** als fehlend.

In der Oberfläche ist `<ConditionEditor OwnerId="…" OwnerModuleKey="…" Slot="…" />` überall einbindbar; `ConditionDialog` ist der Rahmen dafür, wo kein Platz für einen eigenen Abschnitt ist.

**Beim Löschen einer Entität** räumt `EntityCleanup.DeleteForEntityAsync` Feldwerte, individuelle Felder **und** Bedingungssätze zusammen ab — an einer Stelle gebündelt, damit kein Modul eine Art davon vergisst. Hat eine Entität Teilobjekte mit eigenen GUIDs (Händler-Posten, später Dialogknoten), muss ihr Service `DeleteForEntitiesAsync` mit **allen** GUIDs aufrufen; sonst bleiben deren Bedingungen als Waisen zurück.

### Assets

Dateien liegen **nicht** in der Datenbank, sondern im Dateisystem unter dem in `Assets:StoragePath` konfigurierten Pfad (relativ zum Anwendungsverzeichnis, Standard `assets/`). In der Datenbank steht nur der `StorageKey`. Das hält das Verhalten über alle vier Provider gleich und die Datenbanksicherungen klein.

- Ein `Asset` hängt wie die Feldwerte über `OwnerEntityId` + `OwnerModuleKey` an einer Entität — ohne Fremdschlüssel. Ohne Besitzer ist es ein **Werkzeug-Asset** (Karten-Marker, Platzhalter) und zugleich der Zustand frisch hochgeladener, noch nicht zugeordneter Dateien.
- Je Entität ist höchstens ein Asset `IsPrimary` — das Icon, das die Modul-Listen zeigen. Der `AssetService` hält das nach: Wird das Icon gelöscht oder auf eine andere Entität umgehängt, rückt ein übrig gebliebenes Sprite nach.
- Ausgeliefert wird über den Endpunkt `/assets/{id}` in `Program.cs`, nicht über ein statisches Verzeichnis. Der Endpunkt setzt bewusst `nosniff` und eine enge CSP, weil hier vom Nutzer hochgeladene Dateien zurückgehen und SVG Skripte enthalten kann.
- `ImageDimensionReader` liest Breite und Höhe aus dem Dateikopf (PNG, GIF, BMP, JPEG, WebP) ohne Bildbibliothek — für unbekannte Formate wie SVG bleiben die Maße leer.
- `AssetTag` ist absichtlich auf Assets beschränkt. Das geplante Tag-Modul vergibt Tags modulübergreifend und wird diese Stichwörter voraussichtlich ablösen.

In den Bearbeitungsmasken der Module wird `<AssetSpritePanel ModuleKey="…" EntityId="…" Disabled="@istNeu" />` eingebunden; die Listen zeigen das Icon über `<AssetThumbnail AssetId="…" />`.

### Crafting

Ein Rezept hat genau **drei** Angaben, und die Maske zeigt auch nur diese drei: **Ziel-Items**, **benötigte Items** und die **Rezept-Art**. Herstellungsdauer, Werkbank oder Mindestlevel definiert der Nutzer als Felder an der Rezept-Art — dieselbe Regel wie bei den Items. Ziele und benötigte Items sind reine GUID-Referenzen auf Items, ohne Fremdschlüssel über die Modulgrenze.

Beides sind Kind-Sammlungen (`RecipeOutput`, `RecipeIngredient`) mit demselben Aufbau — Item, Menge, Position. Sie teilen sich `IRecipeLine`, damit `CraftingService.SyncLines` beide mit einem Abgleich speichert (samt EF-Fallstrick, siehe oben) und `ValidateLines` beide gleich prüft: Menge mindestens 1, kein Item doppelt. Mehrere Ziele sind der Normalfall für Nebenprodukte („1× Barren + 2× Schlacke“); ein Rezept ganz ohne Ziel ist erlaubt und in der Übersicht als Hinweis markiert.

**Einen eigenen Namen trägt das Rezept nicht.** `Name` steht weiter in der Datenbank, weil Suche, Referenzansicht und Auswahlfelder aller Module über diese Spalte gehen — er wird aber beim Speichern aus den Ziel-Items gebildet (`FormatOutputs`, „2× Fackel + 1× Asche“). Wo der Crafting-Bereich selbst anzeigt (Übersicht, Überschrift der Maske, Baum), wird der Name aus den **aktuellen** Item-Namen gebildet; die gespeicherte Spalte kann nach dem Umbenennen eines Items bis zum nächsten Speichern des Rezepts veralten.

`CraftingService` lädt für Bäume und Zyklenprüfung den gesamten Rezeptbestand eines Projekts einmal und löst ihn im Speicher auf (`CraftingGraph`) — bei der Größenordnung eines Spielprojekts deutlich billiger als eine Abfrage je Ebene. Drei Dinge, die daran hängen:

- **Zyklen** („zyklische Rezepte“ aus der Health-Check-Liste des Konzepts) findet `FindCyclesAsync` per Tiefensuche; der Baumaufbau bricht an einem wiederkehrenden Item ab und markiert den Knoten, statt endlos zu laufen.
- **`SummarizeBaseCost`** rechnet einen Baum auf seine Grundstoffe herunter und verrechnet dabei die Rezeptausbeuten, je Stufe aufgerundet: Ein Rezept, das vier Stäbe liefert, wird für sechs Stäbe zweimal ausgeführt.
- **Die Ausbeute ist die des jeweiligen Ziels**, nicht die des Rezepts: `_byOutput` führt ein Rezept unter jedem seiner Ziel-Items, je mit der Menge genau dieses Items. Wer den Barren herstellt, braucht das Rezept einmal je vier Barren — an der Schlacke daneben hängt eine andere Zahl.

Gibt es mehrere Rezepte für dasselbe Item, klappt der Baum das erste auf und weist die übrigen als Anzahl aus. Sprites hat das Rezept keine — sein Icon ist das des ersten Ziel-Items.

### Währungen

Beliebig viele nebeneinander; Händler nehmen später eine davon entgegen. Strukturell trägt die Währung nur ihr `Symbol` — es steht dort und nicht in einem benutzerdefinierten Feld, weil jede Ansicht, die einen Preis zeigt, es zuverlässig finden muss. Wechselkurse und Ähnliches sind Felder der Währungs-Art. Namen sind je Projekt eindeutig, sonst wären zwei Währungen in einer Preisangabe nicht auseinanderzuhalten.

### NPCs

NPCs und Mobs liegen laut Konzept im selben Modul und unterscheiden sich über `NpcKind` — eine echte Spalte und keine Art, weil das Tool danach filtert und später Loot-Tables und Spawns daran hängen. Die Rollen sind zwei Schalter (`IsTrader`, `IsQuestGiver`), womit „Händler, Quest, beides oder gar nichts“ direkt abgebildet ist.

Das Warenangebot (`TraderOffer`) ist die zweite Kind-Sammlung nach den Rezept-Zutaten und folgt demselben Muster inklusive des EF-Fallstricks oben. Je Posten: Item, Währung, Verkaufs- und Ankaufspreis, Bestand (`null` = unbegrenzt) und Auffüllzeit. Ein Posten ohne Preis ist zulässig — ein Händler, der etwas führt, aber nicht handelt, ist ein gültiger Fall. Ein Preis **ohne** Währung wird abgelehnt, weil die Zahl dann nicht zu deuten wäre.

Alle Konzept-Anforderungen dieses Moduls sind abgedeckt: Spawn-Orte kommen aus dem Karten-Modul, Loot-Tables aus dem Loot-Modul, und die Verfügbarkeit von Shop und einzelnen Posten läuft über das Bedingungssystem.

### Loot-Tables

Einträge sind Item, Wahrscheinlichkeit in Prozent und eine Mengenspanne. Dasselbe Item darf mehrfach vorkommen — „zu 50 % eine Münze, zu 5 % gleich zwanzig“ ist ein üblicher Fall und anders als bei Rezept-Zutaten oder Händler-Posten kein Versehen.

`LootRollMode` unterscheidet zwei Auswertungsverfahren, weil beide in Spielen üblich sind und die Prozentzahlen je nach Verfahren etwas anderes bedeuten:

- **`Independent`** — jeder Eintrag wird einzeln gewürfelt. Eine Summe über 100 % ist normal (drei Dinge zu je 80 % fallen oft gemeinsam).
- **`SinglePick`** — höchstens ein Eintrag fällt, alle teilen sich einen Wurf. Über 100 % hinaus wären die hinteren unerreichbar.

Der Health Check „Loot-Wahrscheinlichkeiten über 100 %“ aus dem Konzept gilt deshalb **nur** für `SinglePick` (`LootService.FindOverfullTablesAsync`, angezeigt auf der Listenseite). Er blockt bewusst nicht das Speichern: Im Konzept steht er unter den Health Checks, also unter „nachschauen“ und nicht unter „verboten“ — sonst ließe sich eine Tabelle beim Umbauen zwischendurch nicht sichern.

NPCs verweisen über `Npc.LootTableId` auf eine Tabelle. Beim Löschen einer Tabelle setzt `LootService` diese Verweise auf `null`, sonst zeigten NPCs auf etwas, das es nicht mehr gibt.

### Dialoge

Die Klasse heißt `Dialogue` und nicht `Dialog`, weil der zugehörige Dienst sonst `DialogService` hieße und mit `MudBlazor.DialogService` kollidierte — in Razor-Dateien sind beide Namensräume importiert. In der Oberfläche heißt es weiterhin „Dialog“.

`DialogueKind` trennt die beiden Formen aus dem Konzept: **`Bark`** sind Sprechblasen, deren Zeilen unabhängig nebeneinander stehen und zufällig erscheinen (Antworten sind dort verboten); **`Conversation`** ist ein Gespräch mit Verlauf, dessen erste Zeile der Einstieg ist und dessen `DialogueChoice`-Einträge weiterführen. `NextLineId = null` beendet das Gespräch.

Beteiligte sind eine Liste von NPCs, der Spieler ein eigener Schalter — damit sind alle drei Fälle des Konzepts abgedeckt (NPC + Spieler, mehrere NPCs, mehrere NPCs + Spieler). Eine Zeile ohne `SpeakerNpcId` spricht der Spieler; er ist keine Entität und bekommt deshalb keine GUID.

`FindProblemsAsync` ist der Health Check „Dialog-Sackgassen“: gemeldet werden Zeilen, die von der ersten aus über keine Antwort erreichbar sind — Inhalt, den der Spieler nie zu sehen bekommt. Eine Zeile **ohne** Antworten ist ausdrücklich kein Fund, das ist das normale Ende. Sprechblasen werden gar nicht geprüft.

`NextLineId` hat bewusst **keinen** Fremdschlüssel: Er liefe im Kreis auf dieselbe Tabelle zurück, und die Löschregeln wären über die vier Provider hinweg nicht einheitlich zu bekommen. Stattdessen prüft der Service beim Speichern, dass das Ziel im selben Dialog existiert, und die Maske setzt Verweise auf gelöschte Zeilen auf `null`.

`GetGraphAsync` liefert dasselbe Gespräch als Knoten-Graph für die Seite `/modules/dialogs/{id}/graph` — Zeilen als Knoten, Antworten mit Ziel als Kanten. `DialogueGraphNode.Depth` ist der Abstand von der Einstiegszeile und trägt die Spalten der Ansicht; **dieselbe Breitensuche** beantwortet nebenbei die Frage des Health Checks, unerreichbare Zeilen bekommen `-1` und landen in einer eigenen Spalte. Sprechblasen haben keinen Verlauf (alles auf Tiefe 0) und bekommen deshalb in der Liste keinen Graph-Einstieg. Gezeichnet wird als SVG ohne JavaScript wie beim Diplomatie-Graphen.

**Razor-Fallstrick beim Zeichnen:** Ein `<text>`-Element als erstes Element eines Razor-Blocks (`@foreach { … }`) liest der Parser als sein eigenes Steuer-Tag und lehnt Attribute ab. Mehrzeiliger SVG-Text wird deshalb als **ein** `<text>` mit `<tspan>`-Kindern gebaut — was ohnehin richtig ist, SVG bricht Text nicht selbst um. Ebenso darf keine Hilfsmethode `Wrap` heißen: Sie verdeckte `MudBlazor.Wrap` und machte jedes `Wrap="Wrap.Wrap"` mehrdeutig.

### Karten

Das Kartenbild ist das **primäre Sprite der Karte** und kommt damit aus der Asset-Bibliothek — eine eigene Bildspalte gäbe es sonst zweimal. Über weitere Sprites lassen sich Varianten hinterlegen und per Icon-Wahl umschalten.

`MapMarker` speichert Lage und Radius **relativ** (0 bis 1), nicht in Pixeln. Damit sitzen Markierungen in jeder Darstellungsgröße richtig und bleiben es auch, wenn dasselbe Bild später in höherer Auflösung neu hochgeladen wird. Ein Marker mit `Radius` ist ein Bereich (Kreis), ohne einer ein Punkt — und mit `Points` ein **Polygon-Gebiet** (das „Gebiete der Fraktionen einzeichnen“ aus dem Konzept, für das der Kreis nur eine Näherung war). Die Eckpunkte stehen als Text `"x,y;x,y;…"` in fester Kultur an der Markierung, relativ wie X und Y: So gehen sie ohne eigene Tabelle durch Export, Import und Duplizieren. Der `MapService` speichert die Liste **kanonisch** (feste Rundung über `MapMarker.FormatPoints` — derselbe Stand ergibt denselben Export), verlangt mindestens drei lesbare Punkte im Bild und lässt das Polygon über einen etwaigen `Radius` gewinnen; X/Y bleiben der Anker für Beschriftung und Symbol. Gezeichnet wird im Karten-Editor Klick für Klick (Entwurf als gestrichelte Linie, erst „Gebiet übernehmen“ macht daraus einen Marker), dargestellt als SVG-Overlay über dem Kartenbild, dessen `viewBox 0–100` die relativen Koordinaten direkt trägt; nur die Polygone selbst fangen Klicks, im Setz-Modus nicht einmal die. Verschieben übersetzt alle Eckpunkte mit dem Anker.

Worauf ein Marker zeigt, steht als `TargetModuleKey` + `TargetEntityId` daran. Ein Modell deckt damit alle Fälle des Konzepts ab: Spawn-Ort eines NPCs, Verknüpfung auf eine andere Karte (`IsMapLink`) und später das Gebiet einer Fraktion. Beim Löschen einer Karte werden Verknüpfungen **anderer** Karten hierher auf `null` gesetzt, sonst zeigten sie ins Leere.

Klicks auf das Kartenbild werden über [gdm-map.js](src/GameDevManager.Web/wwwroot/js/gdm-map.js) in relative Koordinaten zurückgerechnet — die dargestellte Bildgröße kennt nur der Browser, Blazor liefert im `MouseEventArgs` bloß Bildschirmkoordinaten. Das war das erste eigene JavaScript im Projekt; dazu kamen `gdm-shortcuts.js` und `gdm-topbar.js`, beide nach derselben Linie: Der Browser meldet, was nur er weiß, entschieden wird in C#.

### Globale Suche

`SearchService` durchsucht über die `IModuleEntitySource` alle Module plus Assets und Arten. Gesucht wird über kleingeschriebene Namen (`ToLower().Contains(...)`) statt über `LIKE` — das übersetzt sich über alle vier Provider gleich und hängt nicht an der Sortierfolge der Datenbank. Eine eingefügte GUID wird direkt aufgelöst. Das Suchfeld sitzt rechts in der Appbar ([GlobalSearch.razor](src/GameDevManager.Web/Components/Content/GlobalSearch.razor)).

Gesucht wird über **mehr als den Namen**, denn ein Dialogtext steht nirgends im Namen:

- **Name und Beschreibung** — die Standardumsetzung in `ModuleEntitySource<T>.SearchAsync`.
- **Textwerte der benutzerdefinierten Felder** — `SearchFieldValuesAsync`, gesucht wird über die **Entitäten** des Moduls mit einem `Any` auf die Feldwerte, nicht über die Feldwerte selbst: Die tragen keine Projekt-Spalte, und über den Umweg bleibt der Treffer sicher im aktuellen Projekt.
- **Gesprochene Zeilen** — `DialogueEntitySource` überschreibt dafür `SearchAsync`.

Ein Namenstreffer verdrängt den Feldwerttreffer derselben Entität, sonst stünde sie doppelt da. Treffer abseits des Namens tragen einen eigenen Untertitel — sonst wäre nicht zu erkennen, warum ein Eintrag in der Liste steht.

Der Domain-Enum heißt `ContentFieldType` und nicht `FieldType` — letzteres kollidiert mit `MudBlazor.FieldType` und macht jede Razor-Datei mehrdeutig.

### Welt: Tageszeit, Wetter, Biome

Alle drei liegen in **einem** Modul und einer Tabelle (`WorldState`), unterschieden über `WorldStateKind`. Strukturell sind sie dasselbe — ein benannter Zustand, an dem Bedingungen hängen —, und man pflegt sie zusammen; drei Module wären dreimal dieselbe Liste. Wie bei `NpcKind` ist die Ausprägung eine echte Spalte und keine Art: Das Tool filtert danach, und das Bedingungssystem hat je Ausprägung eine eigene `ConditionKind`, die eine benutzerdefinierte Art nicht kennen könnte.

`SortOrder` und `Color` stehen als Spalten da und nicht in benutzerdefinierten Feldern: Tageszeiten haben eine Abfolge, die alphabetisch verloren ginge („Abend, Mittag, Morgen, Nacht“ ist keine Tageszeitliste), und eine Anzeigefarbe muss jede Ansicht zuverlässig finden — dieselbe Überlegung wie bei den Seltenheiten. Alles Weitere (Dauer einer Tageszeit, Sichtweite bei Nebel) definiert der Nutzer als Felder der Art.

Namen sind **je Ausprägung** eindeutig, nicht projektweit: „Klar“ kann eine Wetterlage und ein Biom-Merkmal sein, aber zwei Wetterlagen „Klar“ wären in jeder Bedingung dieselbe.

Die Bedingungsarten `TimeOfDay`, `Weather` und `Biome` zeigen auf dieses Modul; `Condition.ExpectedWorldStateKind` filtert die Auswahl in der Maske, damit unter „Wetter“ keine Biome stehen. Sie sind zugleich Ja/Nein-Fragen — „nicht bei Regen“ ist der ebenso häufige Fall.

### Freischaltungen (Tech-Tree)

Ein Werkzeug-Modul **ohne eigene Daten**: Was etwas freischaltet, steht längst im Bedingungssystem — ein `ConditionSet` im Slot `Unlock` („wird freigeschaltet, wenn …“) oder `Availability`, dessen Bedingungen auf andere Entitäten zeigen. `TechTreeService` liest genau das als gerichteten Graphen (Voraussetzung → Freigeschaltetes) und zeichnet es wie den Dialog-Graphen als SVG ohne JavaScript.

Eine eigene Tabelle hätte dieselbe Aussage ein zweites Mal gespeichert und wäre ab der ersten Bearbeitung im Bedingungs-Editor falsch. Vier Dinge, die man beim Ändern kennen muss:

- **`ConditionKind.Unlocked` wählt sein Zielmodul selbst** (`Condition.ChoosesTargetModule`) — freischalten lässt sich ein Skill, ein Rezept, ein Gebiet. Ein fest verdrahtetes Modul verengte den Baum auf eine Sorte Inhalt; deshalb steht in der Maske ein Modul-Auswahlfeld davor.
- **Ein „darf nicht freigeschaltet sein“ ist keine Kante.** Als Voraussetzung gelesen zeigte der Baum das Gegenteil dessen, was dasteht.
- **Die Tiefe ist der längste Weg**, berechnet über eine topologische Sortierung nach Kahn — nicht der kürzeste: Was zwei Voraussetzungen hat, gehört hinter beide.
- **Ringe** sind der Health Check dazu, derselbe Fall wie zyklische Rezepte eine Ebene höher: Alles im Ring wartet auf sich selbst. Sie erscheinen auf dem Dashboard und der Statistik-Seite; im Bild sind ihre Knoten gestrichelt.

Bedingungen ohne Zielentität („Spieler hat Stufe 20“) sind Voraussetzungen, aber keine Knoten — sie bleiben draußen. Ein Ziel, das es nicht mehr gibt, fällt samt seiner Kante heraus; dass es fehlt, meldet der Health Check „unerfüllbare Bedingungen“.

### Formeln und Levelkurven

`ContentFieldType.Curve` ist ein Feld wie jedes andere — der Wert steht als JSON in `FieldValue.TextValue`. Bewusst keine eigene Tabelle: Feldwerte hängen modulübergreifend an einer GUID, und so geht eine Kurve ohne Zutun durch Export, Import, Duplizieren und die Feldvererbung der Unterarten. Nutzbar ist der Typ überall; gedacht ist er für Spieler, Klassen und Effekte.

Eine Kurve ist ein **Ausdruck über `x`** (`100 * x ^ 1.5`), eine **Spanne** und eine **Wertetabelle** — beides zusammen, nicht entweder-oder: `CurveDefinition.Overrides` überschreibt einzelne Stufen, ohne die Formel zu verlieren (der „Boss auf Stufe 50 kriegt einen Sprung“-Fall). Ohne Formel sind die gesetzten Punkte die ganze Kurve.

`CurveExpression` ist ein eigener Shunting-Yard-Parser ohne Fremdbibliothek — dieselbe Abwägung wie beim `ImageDimensionReader`. Drei Feinheiten:

- **Der Ausdruck wird einmal gelesen und dann ausgewertet**: Die Vorschau ruft ihn bis zu 500-mal auf.
- **Zahlen immer in fester Kultur.** Derselbe Ausdruck muss auf jedem Rechner dieselbe Kurve ergeben — er geht so auch in den Export.
- **Vollständigkeit wird beim Lesen geprüft**, nicht erst beim Rechnen (`EnsureComplete` zählt die Stapeltiefe). Sonst nähme die Maske `1 +` klaglos an.

Stellen, an denen die Formel nicht rechnet (Wurzel aus einer negativen Zahl), fallen aus der Wertetabelle heraus, statt die ganze Kurve zu verwerfen — an den übrigen Stufen stimmt sie ja. Ein Textwert, der kein Kurven-JSON ist, ergibt beim Lesen `null`: Ein Feld, das erst später auf „Formel/Kurve“ umgestellt wurde, trägt noch seinen alten Text und darf davon nicht umfallen.

### Anmeldung & Änderungsprotokoll

Das Tool liegt hinter einer Anmeldung, weil das Konzept protokollieren will, „welcher angemeldete Benutzer welche Änderungen getan hat“.

**Anmeldung.** `AppUser` mit PBKDF2-Hash (`PasswordHasher`) und Cookie-Authentifizierung — bewusst kein ASP.NET-Identity: Gebraucht wird ein Konto mit Passwort, und dafür sieben Identity-Tabellen in alle vier Provider zu migrieren wäre ein Vielfaches an Umfang für dasselbe Ergebnis. Benutzer hängen wie die Projekte an keinem Projekt; unterschieden wird allein, wer weitere Benutzer verwalten darf. Es gibt **kein ausgeliefertes Standardkonto** — der erste Start führt in die Ersteinrichtung unter `/konto/einrichten`, und das erste Konto wird immer Verwalter. Der letzte Verwalter kann sich weder entmachten noch sperren noch löschen.

Drei Dinge, die man beim Ändern kennen muss:

- **Die Seiten rund um die Anmeldung werden statisch gerendert** (`[ExcludeFromInteractiveRouting]`, ausgewertet in `App.razor` über `HttpContext.AcceptsInteractiveRouting()`). Ein Cookie lässt sich nur während einer echten HTTP-Antwort setzen; über die SignalR-Verbindung von Blazor Server gibt es keine, in die es hineinpasste. Aus demselben Grund benutzen diese Formulare `InputText` statt MudBlazor-Eingaben — nur echte `<input name="…">` kommen beim Postback wieder an.
- **Geschützt ist alles über `@attribute [Authorize]` in `_Imports.razor`**; die Ausnahmen zeichnen sich mit `[AllowAnonymous]` aus (Anmeldung, Ersteinrichtung, Abmelden, Fehlerseiten). Die Endpunkte für Assets, Export und Exportstände tragen `RequireAuthorization()`.
- **Die Passwortrichtlinie ist Installations-Konfiguration**, keine Datenbanktabelle (die verlangte vier Migrationen): `PasswordPolicy:*` in `appsettings.Local.json`, gehalten von der Singleton-`PasswordPolicySelection` nach dem Muster der Hell/Dunkel-Wahl, eingestellt in der Benutzerverwaltung. Die Datenschicht kennt nur `IPasswordPolicyProvider` — dasselbe Muster wie beim `IChangeAuthorProvider` —, durchgesetzt wird im `UserService` und nur beim **Setzen** neuer Passwörter (Mindestlänge, Ziffer, Sonderzeichen); bestehende bleiben gültig. Sind Passwörter deaktiviert, meldet der Anmeldename allein an (die Sperre gilt weiter), die Masken blenden ihre Passwortfelder aus, und neue Konten tragen einen **leeren Hash** — nach dem Wiedereinschalten kommt dort niemand herein, bis ein Verwalter ein Passwort setzt. „Angemeldet bleiben“ ist ein Haken der Anmeldung (`IsPersistent` des Cookies); seine Vorgabe ist **aus**, weil ein nicht angehakter Haken beim Postback gar nicht ankommt und eine Vorgabe „an“ sich nie abwählen ließe.

**Änderungsprotokoll.** Geschrieben wird es nicht in den gut zwanzig Modul-Diensten, sondern einmal im `ChangeLogInterceptor` am `SaveChanges` — dort weiß der Änderungsverfolger von EF ohnehin, was neu ist und welche Eigenschaften sich geändert haben. Dieselbe Überlegung wie bei `EntityCleanup`: einmal gebündelt statt je Modul wiederholt, sonst fehlte es in dem einen Modul, in dem man es vergisst. Protokolliert wird, was `IChangeLogged` erfüllt — die Schnittstelle bündelt nur die vier Angaben, die `ContentEntity`, `ContentType`, `PlayerCharacter` und `SkillTree` ohnehin schon tragen.

- **Löschungen sieht der Interceptor nicht**: Die Modul-Dienste löschen über `ExecuteDeleteAsync`, das am Änderungsverfolger vorbei arbeitet, und danach ist nichts mehr da, dessen Namen man notieren könnte. Sie melden ihre Löschung deshalb selbst über `ChangeLog.RecordDeletionAsync`, unmittelbar vor dem Löschen und in derselben Transaktion — **den Benutzernamen lassen sie leer**, damit auch dort nur der Interceptor beantwortet, wer gehandelt hat.
- **Ein Import ist ein Eintrag, keine tausend** (`GameDevManagerDbContext.SuppressChangeLog`, gesetzt von `ImportService`). Ein Protokoll, das ein Import flutet, ist danach unlesbar.
- **Name des Benutzers und Name der Entität stehen als Momentaufnahme im Eintrag**, nicht als Verweis. Nach dem Löschen gäbe es nichts mehr aufzulösen — und genau dieser Eintrag ist der wichtigste.
- **Das Protokoll ist Werkzeug-Sache**: Es steht wie die Moduleinstellungen und die Dashboard-Bänder nicht im Export und übersteht den ersetzenden Import — der als eigener Eintrag darin auftaucht.
- **Wer gerade handelt**, beantwortet die Web-Schicht über `IChangeAuthorProvider`. Der `BlazorChangeAuthorProvider` fragt zwei Quellen: den `HttpContext` (statisch gerenderte Seiten, Endpunkte) und den `AuthenticationStateProvider` (laufender Blazor-Kreis, der keinen `HttpContext` mehr hat). Außerhalb beider — beim Anwendungsstart etwa — wirft Letzterer, statt „niemand angemeldet“ zu melden; das wird abgefangen, und es bleibt beim Systemnamen.
- **Gelesen wird es an zwei Stellen**: auf der Protokollseite über alle Module und je Entität im Abschnitt „Geschichte“ der Bearbeitungsmasken (`<EntityHistoryPanel @ref="…" EntityId="…" />`, neben der Referenzansicht, geblättert über `ChangeLogService.GetForEntityAsync`). Zwei Ansichten für dieselben Einträge heißt: Die Beschriftungen der Änderungsarten liegen als `ChangeActionLabels` in `Services/` und nicht an einer Seite — dasselbe Muster wie `ConditionLabels`. Der Abschnitt zeigt sich nur, wenn der Benutzer das Protokoll-Modul überhaupt sehen darf (`UserPermissions.CanAccessModule`), und er wird nach dem Speichern zusammen mit der Referenzansicht aufgefrischt — eine neu geladene Maske hätte den Eintrag von eben sonst nicht.

**Berechtigungen.** Je Benutzer stellt die Benutzerverwaltung ein, was er darf: **lesen oder auch schreiben** (`CanWrite`), welche **Module** er sieht (`AllowedModuleKeys`, kommagetrennt, `null` = alle — bewusst eine Textspalte statt einer Zuordnungstabelle, die vier Migrationen mehr verlangte) und ob **Export/Import** offenstehen (`CanExport`, `CanImport`). Verwalter haben immer alles — aufgelöst an genau einer Stelle (`UserPermissions.For`), damit keine Prüfung an den Verwalterstatus denken muss. Die Rechte wandern als Ansprüche ins Anmelde-Cookie und gelten deshalb — wie das Verwalterrecht — **ab der nächsten Anmeldung**; ein Cookie aus der Zeit vor den Berechtigungen zählt als „alles erlaubt“, so wie jedes Konto vor der Erweiterung.

- **Durchgesetzt wird zentral am `SaveChanges`**: Der `WriteGuardInterceptor` weist für Nur-Lese-Konten jede Änderung ab, außer an `AppUser` (das eigene Passwort). Das deckt auch die Löschpfade ab, obwohl die über `ExecuteDeleteAsync` laufen — jeder Modul-Dienst speichert unmittelbar davor (Protokolleintrag oder Asset-Zeilen), und dieses Speichern läuft im Interceptor auf, innerhalb der Transaktion und **vor** dem irreversiblen Löschen der Asset-Dateien.
- **Die Wege am Interceptor vorbei prüfen selbst** über den scopeden `PermissionGuard`: reine `ExecuteDelete`-Pfade ohne vorheriges Speichern (`TagService.DeleteTagAsync`/`UnassignAsync`, `AssetService.DeleteTagAsync`), Dateisystem-Vorgänge (`ExportSnapshotService.CreateAsync`/`DeleteAsync`, Asset-Upload), `ImportService.ImportAsync` (braucht Import- **und** Schreibrecht — ein Import schreibt den ganzen Bestand), `ProjectService` (Duplizieren und Löschen brauchen das Schreibrecht) und der `UserService` (Benutzer verwalten nur mit Verwalterrecht — die Absicherung hinter der Oberfläche). **Duplizieren braucht kein Export-/Importrecht**: `ImportCoreAsync` und `CreateCoreAsync` sind die ungeprüften Kerne für interne Aufrufer — auch das Sicherheitsnetz darf nicht am fehlenden Exportrecht reißen.
- **Woher die Datenschicht die Rechte kennt**, ist dasselbe Muster wie beim Urheber: `IUserPermissionsProvider` in Data (Vorgabe „alles erlaubt“ — Start, Ersteinrichtung, Tests), `BlazorUserPermissionsProvider` in Web liest die Ansprüche aus `HttpContext` bzw. `AuthenticationStateProvider`. Die Tests stellen die Rechte über `TestDatabase.Permissions` um.
- **Die Oberfläche filtert dieselben Rechte**: Die Modulleiste zeigt nur freigegebene Module, die **Modulwache** im `MainLayout` weist Seiten eines gesperrten Moduls ab (eine Stelle statt ~57 Seiten, greift auch bei eingetippter URL), globale Suche und Dashboard-Bänder lassen gesperrte Module aus, die Export-Seite blendet Export- bzw. Import-Abschnitt aus, und die Endpunkte `/export/*` liefern ohne Exportrecht 403. Ein Nur-Lese-Konto trägt einen Hinweis-Chip in der Appbar; Speicherversuche laufen in die lokalisierte Meldung des Interceptors, die jede Maske ohnehin durchreicht.

**Schreibkonflikt-Erkennung.** `ContentFields.EnsureNotChangedElsewhereAsync` vergleicht den Zeitstempel, den die Maske mitbringt, gegen den in der Datenbank und wirft eine `ContentConcurrencyException` — abgeleitet von `ContentValidationException`, damit jede Maske sie ohne Änderung anzeigt. Kein `rowversion`: Den gibt es nur im SQL Server, PostgreSQL hätte `xmin`, MySQL und SQLite gar nichts; für vier Provider mit derselben Spalte bleibt der Zeitstempel, den jede `ContentEntity` ohnehin trägt. Die Prüfung sitzt in `StageValuesAsync` — der einen Stelle, durch die jeder Modul-Dienst unmittelbar vor dem Speichern läuft. Dass sie funktioniert, hängt daran, dass die Dienste nach dem Speichern den neuen Zeitstempel in die Maske **zurückschreiben**; sonst meldete der zweite Klick einen Konflikt mit einem selbst. Eine inzwischen gelöschte Zeile ist ausdrücklich **kein** Konflikt — Speichern legt sie wieder an.

### Duplizieren

Kopiert wird auf zwei Ebenen, und beide benutzen denselben Kniff: **serialisieren, GUIDs tauschen, zurücklesen** ([GuidRemap.cs](src/GameDevManager.Data/Services/GuidRemap.cs)). Weil Entitäten laut Konzept ausnahmslos über GUIDs aufeinander verweisen, trifft ein Austausch über den gesamten JSON-Text jede Referenz — auch die Fremdschlüssel der Kind-Sammlungen, die Besitzer-GUIDs der Feldwerte und die Ziele der Bedingungen. Ein Verzeichnis der Spalten, in denen GUIDs vorkommen, müsste bei jedem neuen Modul nachgeführt werden.

Eingesammelt wird **nur die Eigenschaft `id`**: Was mitkopiert wird, bekommt eine neue GUID und jeder Verweis darauf folgt; ein Verweis nach außen steht nicht in der Zuordnung und bleibt stehen — genau richtig, die Kopie eines Rezepts stellt dieselben Items her.

- **Ein ganzes Projekt** (`ProjectService.DuplicateProjectAsync` über [ProjectDuplication.cs](src/GameDevManager.Data/Services/ProjectDuplication.cs)): flüchtiger Export → Archiv umschreiben → Import in ein frisch angelegtes Projekt. Der Import erhält GUIDs sonst bewusst (damit ein Projekt ohne Umschreiben umzieht) und liefe deshalb ungetauscht in jeden Primärschlüssel des Originals. Name und Beschreibung werden im **Manifest** gesetzt, damit die Kopie von Anfang an richtig heißt; die Asset-Dateien bleiben unverändert im Archiv liegen (ihr Pfad ist der alte `storageKey`, unter dem der Import sie sucht — GUIDs ohne Bindestriche, vom Austausch also unberührt). Scheitert etwas, wird das leere Gerüst wieder abgeräumt.
- **Eine einzelne Entität** (`EntityDuplicationService` über [EntityDuplication.cs](src/GameDevManager.Data/Services/EntityDuplication.cs)): Die Kind-Sammlungen kommen aus dem **EF-Modell** (`GetNavigations().Where(n => n.IsCollection)`) statt aus einer Aufzählung je Modul; Feldwerte, individuelle Felder und Bedingungssätze werden in **einem** JSON-Text getauscht, damit ein Wert, der auf ein individuelles Feld zeigt, dessen Kopie folgt. Gesucht wird über **alle** getauschten GUIDs, nicht nur die der Entität — an den GUIDs der Teilobjekte (Händler-Posten, Dialogzeilen) hängen eigene Bedingungen. Sprites bleiben bewusst beim Original.

### Topbar

Die Modulleiste steht auf jeder Seite und ist der einzige Weg, der jedes Modul von überall erreicht. Zwei Dinge macht sie einstellbar, ohne den Satz des Konzepts aufzugeben, dass immer alle Module erreichbar sind.

- **Die Reihenfolge** stellt `/einstellungen/topbar` ein (ziehen oder mit den Pfeilen, tastaturfreundlich wie der Story-Zeitstreifen). Gehalten wird sie von der Singleton-`TopbarSelection` über `Topbar:ModuleOrder` in `appsettings.Local.json` — dasselbe Muster wie die Hell/Dunkel-Wahl und aus demselben Grund **keine** Spalte an `ModuleSetting`: Das verlangte eine Migration in allen vier Providern für eine Angabe, die Sache des Bildschirms ist und nicht des Projekts (sie steht folgerichtig auch nicht im Export). Gespeichert wird eine kommagetrennte Schlüsselliste wie bei den Modul-Freigaben eines Benutzers; unbekannte Schlüssel und Dubletten fallen beim Lesen heraus. `TopbarSelection.Sort` sortiert **stabil**: Ein neu hinzugekommenes Modul hat keinen Eintrag, behält damit seinen Platz aus der Registry und steht hinten — an `ModuleRegistry.All` ändert sich nichts.
- **Was nicht mehr hineinpasst, sammelt ein Aufklapp-Knopf am rechten Ende.** Wie breit die Leiste und ein Knopf sind, weiß nur der Browser: [gdm-topbar.js](src/GameDevManager.Web/wwwroot/js/gdm-topbar.js) misst beides und meldet über `SetMetrics` **allein diese zwei Maße**; wie viele Module daraus werden und welche in den Knopf wandern, rechnet `MainLayout.BuildStrip`. Dieselbe Trennung wie bei den Karten — Geometrie im Browser, Fachlichkeit in C# —, und sie hat den praktischen Vorteil, dass ein abgeschaltetes Modul oder ein anderer Benutzer keine neue Messung braucht. Gemessen wird auf drei Wegen: `ResizeObserver` an der Leiste plus `resize` an `window` und an `visualViewport` — beim **Zoomen** ändert sich die Breite der Leiste in CSS-Pixeln nicht zwingend, und ohne die beiden Ereignisse bliebe eine veraltete Aufteilung stehen.
- **Der Aufklapp-Knopf steht neben dem abgeschnittenen Bereich, nicht darin** (`.gdm-module-more` neben `.gdm-module-strip`, beide in `.gdm-module-bar`). Er ist das letzte Element der Reihe, also träfe ihn jede zu großzügige Messung zuerst — und mit ihm wären alle übrigen Module unerreichbar; genau das passierte beim Zoomen. So kostet eine Fehlmessung höchstens ein angeschnittenes Modul-Symbol, und der Weg zu allen Modulen bleibt in jedem Fall offen. Nachgemessen: selbst mit absichtlich veralteter Aufteilung (29 Symbole in einer 96px-Leiste) bleibt der Knopf vollständig sichtbar und anklickbar.
- **Die Knopfbreite wird über alle gerenderten Knöpfe gemittelt**, nicht am ersten abgelesen, und die Rechnung lässt einen Pixel Luft: Auf gebrochenen Zoomstufen rastet jede Kante einzeln auf ganze Gerätepixel ein, und über zwanzig Knöpfe summieren sich die Bruchteile zu einem ganzen Symbol.
- **Das geöffnete Modul steht immer in der Leiste**, auch wenn es rechnerisch im Aufklapp-Knopf läge; es verdrängt dafür den letzten sichtbaren Knopf, der an dessen Stelle in die Liste rutscht und dort die Reihenfolge wahrt. Sonst verlöre die Seite, auf der man gerade ist, ihre Markierung — und die ist das Einzige, was die Leiste über den Ort sagt.
- **`.gdm-module-bar` hat `flex: 1 1 0` und keinen `MudSpacer` mehr daneben.** Ihre Breite darf nicht an der Anzahl der Knöpfe hängen, sonst liefe die Messung im Kreis: weniger Knöpfe → schmalere Leiste → weniger Platz → noch weniger Knöpfe. Aus demselben Grund misst `gdm-topbar.js` ein eigenes `div.gdm-module-item` je Modul und nicht den MudBlazor-Knopf darin — dessen Aufbau (Tooltip-Hülle) gehört uns nicht.
- **Dazu gehört zwingend die Untergrenze `min-width: 96px`** — zwei Knöpfe zu je 48px. Als einziges wachsendes Flex-Kind bekommt die Leiste sonst schlicht das, was übrig bleibt, und das geht gegen null: gemessen blieben bei 820px Fensterbreite 62px, bei 760px noch 2px, und die Module waren über die Topbar gar nicht mehr erreichbar. Ab der Untergrenze gibt stattdessen die Suche nach (`flex: 0 1 320px`). Passt selbst ein Modul daneben nicht mehr, zeigt `BuildStrip` **nur** den Aufklapp-Knopf: Er führt zu allem, die Markierung nur zu einem.
- Vor der ersten Messung zeigt die Leiste **alles**; `overflow: hidden` fängt den einen Frame ab. Ein leeres Band wäre der auffälligere Fehler als ein Frame mit abgeschnittenen Symbolen.

### Projekte & Dashboard

Alle Inhalte hängen an einem `GameProject`. Welches aktiv ist, hält die Singleton-`ProjectSelection` **installationsweit** fest — das Tool wird self-hosted von einer Person betrieben, alle Verbindungen arbeiten auf demselben Projekt. Der Startwert kommt aus `Project:CurrentId` in `appsettings.Local.json` (geschrieben über `LocalSettingsFile`), damit die Auswahl einen Neustart überlebt; der scopede `ProjectContext` cached das geladene Projekt je Verbindung. Gewechselt wird über den `ProjectSwitcher` in der Appbar — ein Wechsel lädt die Anwendung mit `forceLoad` komplett neu, weil jede Seite ihre Daten ohnehin je Projekt frisch lädt; ein Ereignis an ~57 Seiten wäre der aufwendigere Weg.

Verwaltet werden Projekte unter `/projekte` (`ProjectService`): Namen sind installationsweit eindeutig, das aktive und das letzte Projekt lassen sich nicht löschen, und „Kopie anlegen“ dupliziert eines (siehe „Duplizieren“). **Das Löschen nutzt denselben Wipe wie der ersetzende Import** (`ImportService.WipeProjectAsync`, deshalb `internal`) — Feldwerte, individuelle Felder, Bedingungen und Assets hängen ohne Fremdschlüssel am Projekt und blieben bei einem bloßen Löschen der Projektzeile als Waisen zurück. Davor legt es einen Exportstand als Sicherheitsnetz an (siehe „Import & Export“).

Nach demselben Muster wie die Projektauswahl merkt sich die Singleton-`AppearanceSelection` die **Hell/Dunkel-Wahl** über `Appearance:DarkMode`. Auch sie gilt installationsweit statt je Browser: Ein Wert im Browserspeicher wäre am nächsten Gerät wieder weg, und das Tool betreibt eine Person.

**Das Dashboard zeigt den Projektstand und wiederholt bewusst nicht die Navigation.** Die Modulleiste der Appbar erreicht jedes Modul von jeder Seite aus — ein Kartenraster mit einer „Öffnen“-Karte je Modul kostete einen ganzen Bildschirm und sagte nichts. Stattdessen fünf **Bänder** (`DashboardBands`), jedes mit einer eigenen Frage:

- **Projektleiste** — Name, Gesamtzahl der Inhalte, Zustand als eine Zahl, Zeitpunkt des letzten Exportstands.
- **Weiterarbeiten** — die zuletzt bearbeiteten Entitäten quer über alle Module, jüngste zuerst. `UpdatedAtUtc` steht auf jeder `ContentEntity` und wird von allen Modul-Services gepflegt; geladen wird über `IModuleEntitySource.RecentAsync` — ein neues Modul erscheint dadurch von selbst, wie beim Inhaltsregen des Startscreens.
- **Zustand** — dieselben Health Checks wie auf der Statistik-Seite, aber nur als Fundzahl mit Sprungziel. Funde stehen oben, Geprüftes-ohne-Fund darunter.
- **Inhaltsbestand** — alle Module als Zahlen-Chips, gruppiert nach `ModuleGroup` (Welt, Inhalte, Figuren, Erzählung, Fortschritt, Produktion), innerhalb einer Gruppe der größte Bestand zuerst. Der Chip ist Navigation **und** Auskunft und ersetzt damit die früheren Linkkarten. Die Reihenfolge in `ModuleRegistry.All` bleibt davon unberührt — sie ist die Umsetzungsreihenfolge und die Vorgabe der Modulleiste (siehe „Topbar“).
- **Datenbank** — Provider und Verbindung. Einrichtungsdiagnose, deshalb als einziges Band **standardmäßig aus** (`DashboardBands.IsHiddenByDefault`).

Zwei Dinge, die man beim Ändern kennen muss:

- **Die Health Checks lädt `Home.razor` erst in `OnAfterRenderAsync`**, nicht in `OnInitializedAsync`: sieben Prüfungen laufen über den gesamten Bestand mehrerer Module (`CraftingService.FindCyclesAsync` löst den ganzen Rezeptgraphen auf). Bis sie da sind, zeigen Band und Projektleiste „prüft …“. Das Dashboard darf nicht auf die langsamste Prüfung warten.
- **Ein leeres Projekt bekommt eine eigene Ansicht**: Projektleiste plus die drei Einstiege (Items, Sprites, Import). „Nichts bearbeitet“, „nichts zu beanstanden“ und 22 Striche nebeneinander wären drei Arten, dasselbe Nichts zu zeigen.

Konfigurierbar sind Sichtbarkeit und Reihenfolge der Bänder: `DashboardCard` speichert sie je Projekt (`CardKey` = Band-Schlüssel). Zeilen entstehen erst beim Anpassen; Bänder ohne Zeile zeigt das Dashboard mit dem Standard. Die Tabelle heißt weiter `DashboardCard`, weil eine Umbenennung eine Migration in allen vier Providern verlangte — in Bestandsprojekten stehen dort noch die Modul-Schlüssel des alten Kartenrasters, die werden übergangen und beim ersten Speichern entfernt. Wie die Moduleinstellungen ist das Werkzeug-Konfiguration: nicht im Export, übersteht den ersetzenden Import.

Für die Projektleiste liest `ExportSnapshotService.FindLatestExportedAtUtc` den Zeitpunkt **aus den Dateinamen** der Exportstände statt über `List` jedes ZIP zu öffnen — derselbe Zeitstempel, den auch das Manifest trägt, aber ohne Archivzugriff bei jedem Seitenaufruf.

### Import & Export

`ExportService` schreibt den kompletten Projektstand als ZIP: unter `content/` eine JSON-Datei je Modul plus Arten/Felder, Feldwerte, Bedingungen, Tags und Asset-Metadaten; die Asset-Dateien unter `assets/files/` (Pfad = `storageKey`); `project.json` als Manifest mit `FormatVersion`. Das **Ziel** (`ExportTarget`) ändert nur den Wurzelpfad im Archiv (Unity: `Assets/StreamingAssets/GameDevManager/`, Unreal: `Content/GameDevManager/`, Godot: `gamedevmanager/`) und die Hinweise in der generierten README — der Inhalt ist für alle Ziele derselbe.

Drei Entscheidungen, die man kennen muss:

- **Serialisiert werden die Domain-Entitäten selbst**, kein DTO-Satz. Ein `JsonTypeInfo`-Modifier entfernt Navigationsobjekte (Referenzen bleiben als GUID-Spalten — die Regel des Konzepts) und berechnete Nur-Lese-Eigenschaften; Kind-Sammlungen bleiben eingebettet. Wer eine neue Kind-Sammlung lädt, muss sie im Service auch `Include`n und stabil sortieren — nicht geladene Sammlungen erschienen sonst als leere Listen im Export. Sammlungen, die trotz ihrer Form **nicht** ins Archiv gehören, stehen in `IsUnloadedCollection`: `AssetTag.Assignments` (die Zuordnungen stehen an den Assets), `ContentType.InheritedFields` (nur zusammengetragen) und `ContentType.Children` (die Unterarten stehen ohnehin als eigene Einträge in derselben Liste).
- **Alle Listen sind stabil sortiert** (Name bzw. SortOrder, dann GUID): derselbe Stand ergibt denselben Export — die Grundlage der Diff-Ansicht. `FormatVersion` bei jeder Format-Änderung erhöhen; sie steht auf **6**, seit Felddefinitionen ihren `isTagList`-Schalter tragen (davor **5** für NPC-Beziehungen, Karten-Ebenen und die erweiterten Story-Abschnitte, **4** für die `points` der Karten-Markierungen, **3** für `content/world.json`, **2** für die `parentId` der Arten).
- **Das ZIP entsteht in einer Temp-Datei** (`DeleteOnClose`) und wird dann in den Response kopiert: `ZipArchive` schließt Einträge synchron ab, und der Response-Stream von ASP.NET Core verbietet synchrone Schreibzugriffe.

Was Export, Import und Diff gemeinsam über den Aufbau des Archivs wissen (JSON-Regeln samt `JsonTypeInfo`-Modifier, Manifest-Suche über alle Engine-Präfixe, Zuordnung Inhaltsdatei → Modul), steht in [ExportFormat.cs](src/GameDevManager.Data/Services/ExportFormat.cs).

Ausgeliefert wird über den Endpunkt `/export/{projectId}` in `Program.cs` (wie bei den Assets: über SignalR lässt sich kein Download anstoßen); die Seite `/export` baut nur die URL und navigiert mit `forceLoad` dorthin. Feldwerte und individuelle Felder tragen keine Projekt-Spalte und werden über die Menge aller exportierten Entitäts-GUIDs gefiltert.

**Import** (`ImportService`, gleiche Seite): liest ein Export-ZIP wieder ein — Projektumzug und Wiederherstellung einer Sicherung. Der Import stellt immer einen **kompletten Projektstand** her, er ist bewusst kein Teil-Merge: Entweder ist das Zielprojekt leer (sonst Ablehnung), oder mit `replaceExisting` wird der Bestand vorher vollständig entfernt (Wipe über die Projekt-GUID; die Arten erst nach den Entitäten, deren Restrict-Fremdschlüssel blockierte sonst). Die `formatVersion` aus dem Manifest muss exakt passen. Alle Entitäts-GUIDs bleiben erhalten — nur die Projektzugehörigkeit wird umgeschrieben, Name und Beschreibung des Projekts kommen aus dem Manifest mit. Asset-Dateien werden **vor** der Datenbank-Transaktion in den Dateispeicher geschrieben (gefahrlos: derselbe Asset-GUID hat immer denselben Inhalt), verwaiste Dateien des ersetzten Bestands erst **nach** dem Commit gelöscht. Die Datei-Wrapper im `ImportService` müssen mit den anonymen Objekten im `ExportService` synchron bleiben.

**Exportstände** (`ExportSnapshotService`): aufbewahrte Stände sind normale Export-ZIPs im Json-Layout und liegen wie die Assets im Dateisystem (`Exports:StoragePath`, Standard `exports/`) — bewusst ohne Datenbanktabelle, die Metadaten stehen im Manifest des Archivs. Heruntergeladen wird über `/export/snapshots/{fileName}`; der Dateiname (Zeitstempel + Projekt-GUID) wird per Regex streng geprüft, alles andere ist ein 404. Der **Diff** vergleicht zwei Stände — oder einen Stand gegen den flüchtig exportierten aktuellen — Entität für Entität über die GUID (`JsonNode.DeepEquals`) und meldet je Inhaltsdatei: neu, entfernt, geändert samt der geänderten Eigenschaften.

**Aufbewahrung** (`ExportSnapshotService.PruneAsync`): Weil das Sicherheitsnetz bei jedem ersetzenden Import und jedem Projektlöschen einen Stand anlegt, wächst das Verzeichnis von allein — es muss also von allein wieder abnehmen. `Exports:MaxPerProject` (Vorgabe **20**) und `Exports:MaxAgeDays` (Vorgabe **aus**) stehen deshalb neben `Exports:StoragePath`; aufgeräumt wird am Ende von `CreateCoreAsync`, also auf **beiden** Wegen — dem Stand von Hand wie dem Sicherheitsnetz. Vier Dinge dahinter:

- **Der jüngste Stand bleibt in jedem Fall stehen**, auch über dem Höchstalter: Ein Projekt, an dem lange niemand arbeitet, stünde sonst irgendwann ganz ohne Sicherung da — und genau der letzte Stand ist der, auf den man zurückgeht. Ein frisch angelegter Stand kann sich damit auch nie selbst wegräumen.
- **Gearbeitet wird auf `List`**, nicht auf dem Verzeichnis: Entfernt wird genau das, was die Historie zeigt. Eine fremde oder kaputte Datei taucht dort nicht auf und ist nicht unsere, sie zu löschen — dieselbe Linie wie bei `ReadSnapshotInfo`.
- **Ein Stand, der sich nicht löschen lässt** (er wird gerade heruntergeladen), bricht weder Export noch Aufräumen ab; er bleibt stehen und fällt beim nächsten Lauf.
- **Die Grenzen sind Konfiguration, keine Tabelle** — wie `Assets:MaxFileSizeBytes` und aus demselben Grund wie die Passwortrichtlinie: eine Angabe der Installation, für die vier Migrationen unangemessen wären. Die Export-Seite schreibt die geltende Regel im Klartext unter die Historie (auch „unbegrenzt“, sonst wäre die Einstellung nicht auffindbar) und bietet „Jetzt aufräumen“ an — eine geänderte Einstellung griffe sonst erst beim nächsten Stand.

**Sicherheitsnetz vor zerstörenden Aktionen:** `CreateSafetyNetAsync` legt vor dem ersetzenden Import und vor dem Löschen eines Projekts automatisch einen Stand an — in den Diensten (`ImportService`, `ProjectService`) und nicht in der Oberfläche, damit kein zweiter Aufrufer es vergessen kann. Immer **mit** Asset-Dateien, weil der Wipe die Dateien mitnimmt; ein leeres Projekt bekommt keinen Stand (`ImportService.HasContentAsync`, deshalb `internal`). Scheitert das Anlegen, scheitert die Aktion — ein Netz, das reißen darf, ist keines; der IO-Fehler wird dafür in eine `ContentValidationException` umgesetzt, weil die Oberfläche nur diese durchreicht.

Die Export-Seite zeigt außerdem die offenen **Health-Check-Funde** über dem Download-Knopf, ohne ihn zu sperren — dieselbe Linie wie beim Loot-Check: nachschauen, nicht verbieten. Geladen wird wie beim Dashboard erst nach dem ersten Rendern; die Bedingung fragt nach dem Ergebnis statt nach `firstRender`, damit ein Import es zurücksetzen und neu laden lassen kann.

## Neuere Erweiterungen (14.08.2026)

Was man beim Weiterarbeiten an den neuen Teilen wissen muss — die Migration dazu heißt in allen vier Providern `NpcRelationsMapLayersStoryAndBoards` (danach kam `FieldTagList` für die Stichwortfelder), die `FormatVersion` des Exports steht auf **6**:

- **NPC-Beziehungen** (`NpcRelationType` + `NpcRelation`): Die Beziehungsart ist ein Bezeichnungspaar (Richtung/Gegenrichtung), bewusst keine `ContentType`-Art — sie trägt keine Felder. Beziehungen sind gerichtete Kind-Sammlungen des Quell-NPCs (Export eingebettet in `npcs.json`, die Arten daneben als `relationTypes`); die Gegenseite hängt als GUID ohne Fremdschlüssel daran, `NpcService.DeleteNpcAsync` räumt eingehende Beziehungen selbst ab. Die Arten hängen per Restrict-Fremdschlüssel an den Beziehungen — beim Projekt-Wipe deshalb **erst die NPCs, dann die Arten** löschen. `DeleteRelationTypeAsync` ist ein reiner `ExecuteDelete`-Pfad und prüft den `PermissionGuard` selbst.
- **Wesenszüge/Vorlieben/Persönlichkeit am NPC**: Textspalten, keine Tabellen — Vorlieben/Persönlichkeit kommagetrennt (kanonisch über `KeywordList.Normalize`, dieselbe Hilfe wie bei den Stichwortfeldern), Wesenszüge kanonisch über `NpcTraits.Parse/Format` (feste Schlüssel, feste Reihenfolge — derselbe Stand ergibt denselben Export). UI: `ChipListInput` und `TraitBarEditor` unter `Components/Content`.
- **Karten-Ebenen** (`MapLayer`): Kind-Sammlung der Karte; `MapMarker.LayerId` bewusst **ohne** Fremdschlüssel (zweiter Löschpfad auf dieselbe Tabelle neben der Karten-Kaskade) — `SyncMarkers` setzt Zuordnungen zu unbekannten Ebenen auf `null`. Sichtbarkeit ist persistiert, die „aktive“ Ebene ist Bedien-Zustand des Editors.
- **Story**: `Mood`/`GameDate`/`Duration`/`Location` sind Freitext; `TargetMapId`/`TargetMapMarkerId` GUID-Referenzen (beim Löschen einer Karte genullt); `StoryLink` ist die dritte Kind-Sammlung nach bekanntem Muster, eingehende Links räumt `DeleteEntryAsync` ab. `ReorderAsync` nimmt die komplette Drag&Drop-Reihenfolge entgegen; nicht genannte Abschnitte rutschen ans Ende.
- **ToDo/Whiteboard sind Werkzeug-Daten** wie das Änderungsprotokoll: keine `ContentEntity`, kein Export, sie überstehen den ersetzenden Import und schreiben kein Änderungsprotokoll. Ihre `ExecuteDelete`-Pfade prüfen den `PermissionGuard` selbst. Der `WhiteboardNotifier` (Singleton) meldet gespeicherte Änderungen an alle offenen Ansichten; das Ereignis trägt eine Absender-Marke, damit die auslösende Ansicht nicht sich selbst neu lädt. Die Whiteboard-Zeichenfläche rendert mit **fester Pixelgröße** (= Zeichenflächen-Einheiten), damit `OffsetX/Y` der Mausereignisse direkt Koordinaten sind — kein JS-Umweg wie bei den Karten. Die Seiten liegen unter `Pages/Whiteboards` (Plural), weil der Ordnername sonst mit dem Entitätsnamen als Namespace kollidierte.
- **Verbindungen** (`ModuleKeys.Connections`) ist ein Werkzeug-Modul ohne eigene Daten nach dem Muster des Freischaltungs-Graphen: `ConnectionsService` liest Beziehungen und Fraktionszugehörigkeiten, die Seite zeichnet SVG ohne JavaScript (Kreis-Layout, runde Profilbilder per `clipPath`, Fraktionsring aus einer festen Datenfarben-Palette).
- **Spieler-Umbau**: `ModuleKeys.Player` bleibt `"player"` (steht in der Datenbank an Arten/Feldern), nur die Beschriftung heißt „Skilltrees“. `PlayerService.ConvertCharactersToNpcsAsync` überführt Spielerfiguren in einzigartige NPCs — **dieselbe GUID**, alle GUID-Anhänge (Assets, Feldwerte, Bedingungen, Tags, Karten-Markierungen, Story-Beteiligte) wandern per Modul-Schlüssel-Update mit.
- **Shortcuts**: `wwwroot/js/gdm-shortcuts.js` (zweites eigenes JavaScript neben `gdm-map.js`) + `KeyboardShortcuts`-Komponente im `MainLayout`. Strg+K fokussiert `#gdm-global-search`, Strg+S klickt den Knopf mit dem Attribut `data-gdm-save` (bisher an NPC-, Karten- und Story-Editor — neue Masken markieren ihren Speichern-Knopf einfach mit), Alt+Buchstabe navigiert über einen JSInvokable-Rückruf.
- **Drag & Drop** (Story-Zeitstreifen, Kanban-Karten) läuft über natives HTML5-DnD auf einfachen `div`s — die Direktiven `@ondragover:preventDefault` sind nur auf HTML-Elementen erlaubt, nicht auf MudBlazor-Komponenten.

## Design

Anthrazit als Grundton, Gelb (`#FFC300`) als einziger kräftiger Akzent, durchgehend eckige Kanten (Border-Radius 0), Inter als UI-Schrift und JetBrains Mono für GUIDs/IDs. Ebenen trennen sich über 1px-Linien, nicht über Schlagschatten — Cards deshalb mit `Elevation="0"`.

- **Alle Farben und Typo-Stufen stehen in [GameDevManagerTheme.cs](src/GameDevManager.Web/Theme/GameDevManagerTheme.cs)** und werden dem `MudThemeProvider` in `MainLayout.razor` übergeben. In Komponenten nie Hex-Werte schreiben, sondern `Color.Primary` bzw. `var(--mud-palette-…)` verwenden — sonst bricht die Hell/Dunkel-Umschaltung. **Ausnahme sind Datenfarben**: Wo viele Dinge nebeneinander auseinanderzuhalten sein müssen, reicht die Palette nicht, und es steht eine feste Liste in der Komponente — die Fraktionsringe der Verbindungen und die zehn Wesenszüge des `TraitBarEditor` (je Zug eine Farbe nach ihrer Wirkung: Rot für Mut, Blau für Treue, Gelb fürs Risiko). Ihre Helligkeit muss auf hellem wie dunklem Grund tragen.
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
