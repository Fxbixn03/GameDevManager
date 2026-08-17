# ToDo

Ideensammlung für die Zeit nach dem Konzept. Alles aus [Konzept.md](Konzept.md) ist umgesetzt —
was hier steht, geht darüber hinaus.

Jeder Punkt trägt eine Userstory und einen Umsetzungshinweis, der sagt, wo im Bestand er
andockt. Die Marker am Ende jedes Punktes:

- **Aufwand** — S (ein Tag), M (wenige Tage), L (eine Woche und mehr)
- **Migration** — braucht es eine Schemaänderung? Dann in **allen vier Providern**.
- **Format** — muss `ExportFormat.FormatVersion` steigen?

---

## A. Inhalt und Modellierung

### F1 — Quest-Ziele als eigene Schritte ✅ umgesetzt

> **Als** Quest-Designer **möchte ich** eine Quest in einzelne Ziele zerlegen („Sprich mit Alrik",
> „Sammle 5 Kräuter", „Kehre zurück"), jedes mit eigener Abschlussbedingung, **damit** ich den
> Verlauf einer Quest abbilden kann und nicht nur ihren Endzustand.

Heute hat `Quest` genau einen Bedingungssatz im Slot `Completion` — eine Quest ist damit fertig
oder nicht, dazwischen gibt es nichts. `QuestObjective` als vierte Kind-Sammlung nach dem Muster
von `RecipeIngredient`/`TraderOffer`: Text, Reihenfolge, `IsOptional`, und weil ein Teilobjekt mit
eigener GUID Besitzer eines `ConditionSet` sein darf (genau der Fall, für den `ConditionSlots`
gebaut ist), hängt die Abschlussbedingung ohne neue Mechanik daran. Wichtig: `DeleteQuestAsync`
muss `EntityCleanup.DeleteForEntitiesAsync` mit **allen** Ziel-GUIDs aufrufen, sonst bleiben deren
Bedingungen als Waisen zurück. Der Health Check „Quests ohne Abschlussbedingung" prüft dann
zusätzlich Ziele ohne Bedingung.

*Aufwand: M · Migration: ja · Format: +1*

**Umgesetzt.** `QuestObjective` als Kind-Sammlung, Migration `QuestObjectives` in allen vier
Providern, `FormatVersion` auf **9**. Die Abschlussbedingung hängt im Slot `Completion` an der
GUID des Ziels; der Health Check verlangt bei Zielen eine Bedingung je Ziel statt einer an der
Quest. Die Ziel-Texte sind über F8 gleich mit übersetzbar.

### F2 — Feldtyp „Referenzliste" ✅ umgesetzt

> **Als** Nutzer **möchte ich** in einem Feld mehrere Entitäten auswählen (die drei Effekte eines
> Schwerts, die vier erlaubten Klassen einer Rüstung), **damit** ich dafür nicht „Effekt 1",
> „Effekt 2", „Effekt 3" als Einzelfelder anlegen muss.

`ContentFieldType.EntityReference` trägt genau eine GUID. Eine Liste geht nach demselben Muster
wie die Stichwortfelder: ein Schalter `IsMultiValue` an der `FieldDefinition` und eine kanonische,
semikolongetrennte GUID-Liste in `FieldValue.TextValue`. Kein neuer Feldtyp, keine neue Tabelle —
damit geht sie ohne Zutun durch Export, Import, Duplizieren und Feldvererbung, dieselbe Überlegung
wie bei `KeywordList`. Zu bedenken: `ReferenceService` und `GuidRemap` müssen die Liste kennen,
sonst findet die Referenzansicht diese Verwendungen nicht und eine Kopie zeigt auf die Originale.

*Aufwand: M · Migration: ja (ein Schalter) · Format: +1*

**Umgesetzt.** Schalter `IsMultiValue` an der `FieldDefinition`, kanonische GUID-Liste über
`GuidList` (Semikolon, damit sie sich nicht mit den kommagetrennten Stichwörtern in derselben
Spalte beißt), Migration `FieldMultiValue` in allen vier Providern, `FormatVersion` auf **12**.
Der `ReferenceService` sucht zusätzlich im Text; `GuidRemap` traf die Liste ohne Änderung, weil
er über den gesamten JSON-Text tauscht.

### F3 — Feldgruppen und Feldreihenfolge ✅ umgesetzt

> **Als** Nutzer mit einer Item-Art aus 25 Feldern **möchte ich** die Felder in benannte Abschnitte
> gruppieren („Kampfwerte", „Wirtschaft", „Darstellung") und ihre Reihenfolge festlegen, **damit**
> die Bearbeitungsmaske lesbar bleibt.

`FieldDefinition` hat heute keine Sortierung — die Maske zeigt, was die Abfrage liefert. Zwei
Spalten (`SortOrder`, `GroupName`) und `ContentEditContext.TypeFields` sortiert danach; die
`ContentFieldsPanel` baut je Gruppe einen `CollapsiblePanel`. Die Feldvererbung bleibt wie sie ist:
geerbte Felder stehen vor eigenen, innerhalb dessen greift die Sortierung. Ohne Gruppennamen
verhält sich alles wie heute.

*Aufwand: S · Migration: ja · Format: +1*

**Umgesetzt.** `FieldDefinition.SortOrder` gab es bereits; hinzu kam `GroupName`, Migration
`FieldGroups` in allen vier Providern, `FormatVersion` auf **10**. Die Maske baut je Abschnitt
einen aufgeklappten `MudExpansionPanel`, Felder ohne Abschnitt stehen wie bisher oben.

### F4 — Wertebereiche und Formatprüfung an Feldern ✅ umgesetzt

> **Als** Nutzer **möchte ich** an einem Zahlenfeld Minimum und Maximum festlegen und an einem
> Textfeld ein Muster, **damit** ein Tippfehler beim Erfassen auffällt und nicht erst im Spiel.

Heute prüft `ContentFields.ValidateRequired` nur „ausgefüllt oder nicht". Vier Spalten an der
`FieldDefinition` (`MinValue`, `MaxValue`, `Pattern`, `Unit`) und eine Erweiterung derselben
Prüfmethode — die Meldungen kommen aus `DataMessages.resx`, damit sie in beiden Sprachen stehen.
Die Einheit ist reine Anzeige (Suffix im Eingabefeld und in der Liste), aber sie beantwortet die
häufigste Frage an einer Zahl: Sekunden oder Millisekunden?

*Aufwand: S · Migration: ja · Format: +1*

**Umgesetzt.** `Unit` gab es bereits; hinzu kamen `MinValue`, `MaxValue` und `Pattern`,
Migration `FieldValidation` in allen vier Providern, `FormatVersion` auf **11**. Geprüft wird in
`ContentFields.ValidateRequired` und in der Massenbearbeitung; ein kaputtes Muster wird schon
beim Speichern des Feldes abgewiesen.

### F5 — Varianten: eine Entität erbt von einer anderen ✅ umgesetzt

> **Als** Nutzer **möchte ich** ein Item als Variante eines anderen anlegen („Eisenschwert +1" erbt
> alles vom „Eisenschwert" und überschreibt nur den Schaden), **damit** ich Bestandsreihen pflegen
> kann, ohne jeden Wert zu wiederholen.

Die Feldvererbung gibt es bereits — aber nur zwischen **Arten** (`ContentType.ParentId`). Dasselbe
auf Entitätsebene: `ContentEntity.BasedOnId` (GUID, ohne Fremdschlüssel wie alles Modulübergreifende),
und `ContentFields` liefert für einen nicht gesetzten Wert den des Vorbilds nach. Die Maske
markiert geerbte Werte sichtbar und bietet „überschreiben". Dieselben Verbotsregeln wie bei den
Arten: keine Ringe, kein fremdes Modul, kein fremdes Projekt. Achtung beim Export — der Verbraucher
muss die Auflösung kennen, deshalb sollte der Export **aufgelöste** Werte schreiben und die
Herkunft als Zusatzangabe daneben.

*Aufwand: L · Migration: ja · Format: +1*

**Umgesetzt.** `ContentEntity.BasedOnId` (Migration `EntityVariants` in allen vier Providern —
eine Spalte in allen 22 Inhaltstabellen, weil sie an der Basis hängt), `EntityInheritance` als
reine Rechenklasse, `FormatVersion` auf **21**. Sechs Entscheidungen: Geerbt werden
**Feldwerte und sonst nichts** — Name, Beschreibung, Stand und Sprite bleiben eigen. Aufgelöst
wird in `ContentFields.LoadValuesAsync<TEntity>`, das dafür einen **Typparameter** bekam: Die
Kette liegt in der Modul-Tabelle, und der Compiler zwingt so jede der zwanzig Aufrufstellen,
sich einmal dazu zu äußern. Geprüft und fortgeschrieben wird in `StageValuesAsync` — der einen
Stelle, durch die jeder Dienst läuft, wie bei der Schreibkonflikt-Erkennung; „gleiches Modul und
Projekt“ prüft sich dabei von allein, weil `db.Set<TEntity>()` ein Vorbild von anderswo gar nicht
findet. **Geerbte Werte werden nie als Zeile gespeichert**, sonst wäre die Vererbung nach dem
ersten Speichern materialisiert; „wieder erben“ ist deshalb schlicht „leeren“. Beim **Löschen des
Vorbilds** übernimmt die Variante dessen Werte als eigene und rückt in der Kette eine Stufe vor —
ein Löschklick darf nicht den halben Bestand ändern; dafür heißt die typfreie
`EntityCleanup`-Methode jetzt `DeleteForSubObjectsAsync`, damit niemand die Auflösung umgeht. Und
der **Export schreibt aufgelöste Werte** mit `inheritedFromEntityId` daneben, der **Import
überspringt genau die** — sonst wäre die Vererbung nach einem Umzug aufgelöst.

### F6 — Spawn-Regeln als eigene Struktur ✅ umgesetzt

> **Als** Level-Designer **möchte ich** zu einem Mob festlegen, wie viele gleichzeitig in einem
> Gebiet stehen, nach welcher Zeit sie nachwachsen und unter welcher Bedingung sie überhaupt
> erscheinen, **damit** die Karte mehr sagt als „hier ungefähr".

Die Karten-Markierung deckt heute das *Wo* ab, das *Wie viele, wie oft, wann* steht bestenfalls in
benutzerdefinierten Feldern und ist damit nicht auswertbar. `SpawnRule` als Kind-Sammlung des NPCs:
Ziel-Marker oder Ziel-Karte, Anzahl (Spanne), Respawn-Dauer, Tageszeit/Wetter über das
Bedingungssystem im neuen Slot `Spawn`. Der Karten-Editor zeigt sie als Aufklappliste an der
Markierung. Das schließt den letzten offenen Halbsatz des NPC-Kapitels im Konzept („manche NPCs
gibt es nur einmal … andere spawnen nur in bestimmten Bereichen") sauber ab.

*Aufwand: M · Migration: ja · Format: +1*

**Umgesetzt.** `SpawnRule` als Kind-Sammlung des NPCs, Migration `SpawnRules` in allen vier
Providern, `FormatVersion` auf **17**, Bedingungen im neuen Slot `Spawn`. Der Abschnitt steht in
der NPC-Maske; der Karten-Editor zeigt die Regeln inzwischen als Aufklappliste an der
ausgewählten Markierung (`NpcService.GetSpawnRulesForMapAsync`) und Regeln ohne Markierung als
eigenen Abschnitt „Spawns ohne festen Ort“ — gepflegt wird weiterhin in der NPC-Maske.

### F7 — Formeln über Feldnamen statt nur über `x` ✅ umgesetzt

> **Als** Balancing-Verantwortlicher **möchte ich** in einer Formel auf andere Felder derselben
> Entität verweisen (`Schaden * Angriffsgeschwindigkeit`), **damit** abgeleitete Werte nicht von
> Hand nachgerechnet werden müssen.

`CurveExpression` ist ein Shunting-Yard-Parser, der genau eine Variable kennt. Ein zweiter Feldtyp
`Formula` (oder ein Schalter an `Curve`) reicht dem Parser ein Wörterbuch aus den Feldwerten der
Entität durch. Berechnete Felder sind **nicht** speicherbar, sondern werden bei jeder Anzeige
gerechnet — sonst veralten sie beim ersten Umbau. Zwei Fallen: Ringe zwischen berechneten Feldern
(dieselbe Tiefensuche wie bei den Rezepten) und der Export, der den **gerechneten** Wert schreiben
sollte, nicht die Formel — die Engine soll nicht parsen müssen.

*Aufwand: M · Migration: nein (neuer Enum-Wert) · Format: +1*

**Umgesetzt.** `ContentFieldType.Formula` als eigener Typ (kein Schalter an `Curve` — eine
Kurve hat Spanne und Diagramm, ein berechnetes Feld genau eine Zahl), `CurveExpression` kennt
benannte Variablen, `FormulaEvaluator` löst sie samt Ringprüfung auf. `FormatVersion` auf **18**:
Der Export schreibt den gerechneten Wert neben der Formel.

---

## B. Erzählung und Lokalisierung

### F8 — Übersetzbare Kind-Sammlungen (Dialogzeilen!) ✅ umgesetzt

> **Als** Lokalisierer **möchte ich** Dialogzeilen, Antwortmöglichkeiten und Cutscene-Einstellungen
> übersetzen, **damit** das Spiel in einer zweiten Sprache mehr kann als Item-Namen.

**Die größte echte Lücke im Bestand.** `LocalizationService.GetRowsAsync` sammelt nur `name`,
`description` und die Textwerte benutzerdefinierter Felder — die textlastigsten Inhalte des ganzen
Tools (`DialogueLine.Text`, `DialogueChoice.Text`, `CutsceneShot.Text`, `StoryEntry.Body`) sind gar
nicht erfasst. Der `Slot` ist schon eine freie Textspalte und die Adressierung läuft über die GUID
ohne Fremdschlüssel — Teilobjekte haben eigene GUIDs, es passt also alles. Was fehlt, ist ein Weg,
über den ein Modul seine zusätzlichen Texte meldet: `IModuleEntitySource.GetTranslatableTextsAsync`
als virtuelle Methode mit leerer Standardimplementierung, überschrieben von Dialog, Cutscene und
Story. Damit ist auch jedes künftige Modul dabei — dasselbe Muster wie bei `FindReferencesAsync`.
Die Zeichenketten-Tabelle unter `localization/<code>.json` bekommt die Zeilen ohne weiteres Zutun.

*Aufwand: M · Migration: nein · Format: +1 (mehr Schlüssel in der Tabelle)*

**Umgesetzt.** `IModuleEntitySource.GetTranslatableTextsAsync` liefert die Zusatztexte je Modul;
überschrieben von Dialog (Zeilen und Antworten, Slot `text` an ihrer eigenen GUID), Cutscene
(Einstellungen) und Story (`Body`, Slot `body` an der Entität). `FormatVersion` steht auf **8**.

### F9 — Übersetzungen als CSV heraus und wieder herein ✅ umgesetzt

> **Als** Projektleiter **möchte ich** die offenen Übersetzungen einer Sprache als Tabelle
> herausgeben und die ausgefüllte Datei wieder einlesen, **damit** ein externer Übersetzer arbeiten
> kann, ohne einen Zugang zum Tool zu brauchen.

`CsvContentService` und `Csv` (RFC 4180, Trennzeichenerkennung, feste Kultur, BOM) sind fertig und
tun hier dasselbe: Spalten `id`, `slot`, `modul`, `entität`, `ausgangstext`, `übersetzung`,
`stand`. Beim Zurücklesen gelten dieselben Regeln wie beim Modul-CSV — eine Zeile findet ihr Ziel
über `id`+`slot`, eine leere Zelle löscht die Übersetzung, eine kaputte Zeile ist eine Warnung und
kein Abbruch. Filter „nur fehlende und veraltete" macht die Datei erst brauchbar; `SourceText` ist
die Spalte, an der der Übersetzer sieht, was sich geändert hat. XLIFF wäre der Standard, aber CSV
ist der Weg, den jeder Übersetzer ohne Werkzeug öffnet.

*Aufwand: S · Migration: nein · Format: unverändert*

**Umgesetzt.** `LocalizationService.ExportCsvAsync`/`ImportCsvAsync` über denselben `Csv`-Leser,
Download über `/export/translations/{projectId}/{code}`, Knöpfe auf der Lokalisierungs-Seite.
Der Ausgangstext kommt beim Zurücklesen aus dem Bestand, nicht aus der Datei.

### F10 — Vertonung: Audiodatei und Sprecher je Dialogzeile ✅ umgesetzt

> **Als** Audio-Verantwortlicher **möchte ich** an jeder Dialogzeile die aufgenommene Datei und die
> Sprecherrolle hinterlegen, je Sprache, **damit** ich sehe, was noch nicht eingesprochen ist.

`Asset` hängt bereits über `OwnerEntityId` + `OwnerModuleKey` an beliebigen GUIDs — eine Dialogzeile
hat eine GUID, es braucht also keine neue Spalte, nur eine Oberfläche und einen Modul-Schlüssel für
die Zeile. Dazu eine Fortschrittsansicht wie die der Übersetzungen: je Dialog und Sprache, wie viele
Zeilen vertont sind. Der Health Check dazu ist naheliegend: Zeilen mit Übersetzung, aber ohne
Aufnahme.

*Aufwand: M · Migration: nein · Format: +1*

**Umgesetzt.** `VoiceOverService` ohne eigenen Datenbestand: Eine Aufnahme ist ein `Asset` an
der GUID der Zeile — dieselbe Anbindung wie beim Skizzenbild einer Cutscene-Einstellung.
**Doch eine Migration**, anders als hier vermutet: Sprache und Sprecher mussten irgendwo stehen,
und zwei Spalten am Asset (`LanguageCode`, `VoiceActor`, Migration `AssetVoiceOver`) waren
weniger als eine Vertonungs-Tabelle, die die Zuordnung Zeile→Datei ein zweites Mal geführt
hätte. `FormatVersion` auf **20**. Vier Entscheidungen: Der **Sprecher steht am Asset und nicht
an der Zeile** — die Rolle sagt `SpeakerNpcId` längst, und jede Sprache hat ihre eigene
Besetzung. Aufgenommen wird in **allen** Sprachen, die Ausgangssprache eingeschlossen: Ihr Text
steht am Inhalt, ihre Aufnahme aber nirgends. Eine zweite Aufnahme derselben Sprache **löst die
erste ab** (gelöscht, nicht `ReplaceAsync` — an einer Aufnahme hängt kein Verweis, sie wird über
Zeile und Sprache gefunden). Und der Health Check fragt **nur nach Sprachen, in denen der Text
vorliegt**; ohne Sprachen im Projekt findet er nichts, sonst meldete er beim Anlegen der zweiten
Sprache auf einen Schlag jede Zeile.

### F11 — Dialog durchspielen ✅ umgesetzt

> **Als** Autor **möchte ich** einen Dialog im Browser durchklicken, mit setzbaren Flags und
> Beständen, **damit** ich sehe, wo eine Bedingung ihn stumm abbricht — bevor es die Engine tut.

Der Dialog-Graph zeigt die Struktur, aber nicht das Erlebnis. Eine Ansicht `/modules/dialogs/{id}/play`
mit einer Zustandsleiste (welche Flags gesetzt, welche Items im Beutel, Stufe, Tageszeit) und der
Auswertung der `ConditionSet`s an Zeilen und Antworten. Das ist der erste Ort, an dem das
Bedingungssystem **ausgewertet** statt nur verwaltet wird — die Auswertungslogik gehört als
`ConditionEvaluator` in die Datenschicht, weil danach auch der Quest- und der Freischaltungs-Baum
davon leben (siehe F19).

*Aufwand: L · Migration: nein · Format: unverändert*

**Umgesetzt.** `ConditionEvaluator` als reine Rechenklasse in der Datenschicht
(`GameStateAssumption` → Ergebnis je Bedingung und Satz; nicht Rechenbares — `Custom`, fehlende
Ziele — gilt als **ausgewiesene Annahme** statt als stummes Nein). Zeilen und Antworten tragen
jetzt eigene Verfügbarkeits-Bedingungen (Slot `Availability` an ihren GUIDs — die Aufräumpfade
dafür lagen schon bereit); die Knöpfe in der Maske sind wie bei den Quest-Zielen gesperrt,
solange Ungespeichertes ansteht. Die Seite `/modules/dialogs/{id}/play` bietet als
Zustandsleiste **genau die Stellschrauben an, die in den Bedingungen dieses Dialogs vorkommen**,
spielt das Gespräch mit, meldet gesperrte Antworten samt Begründung und zeigt den stummen
Abbruch, wenn eine erreichte Zeile nicht verfügbar wäre. F19 lebt vom selben Kern.

### F12 — Story-Text als Markdown mit Entitäts-Erwähnungen ✅ umgesetzt

> **Als** Autor **möchte ich** im Story-Text `@Eisenschwert` schreiben und beim Speichern eine
> echte Verknüpfung bekommen, **damit** die Referenzansicht des Items auch die Story-Abschnitte
> zeigt, in denen es vorkommt.

`StoryEntry.Body` ist heute Rohtext. Markdown-Auszeichnung plus eine Erwähnungs-Syntax, die beim
Speichern nach GUID auflöst und als `[[modul:guid|Anzeigename]]` gespeichert wird — der Anzeigename
bleibt lesbar, wenn die Entität verschwindet. `ReferenceService` findet die Erwähnungen über eine
Textsuche nach der GUID, dasselbe wie bei den GUID-Spalten. Gilt genauso für `KanbanCard.Notes` und
`ContentEntity.Description`.

*Aufwand: M · Migration: nein · Format: unverändert*

**Umgesetzt** für den Story-Text: `ContentMentions` und `SimpleMarkdown` in der Domäne,
`MentionResolver` in der Datenschicht, `MarkdownView` als Anzeige, Vorschau-Umschalter im
Story-Editor. Die Referenzansicht findet Erwähnungen über die GUID im Text. **Auch für
`KanbanCard.Notes` umgesetzt**: `KanbanService.UpdateCardAsync` löst über den `MentionResolver`
auf, Kartenfläche und Dialog zeigen die `@Name`-Fassung, und die Referenzansicht meldet
„Erwähnt in Aufgabe“ mit dem Board als Ziel (die Karte hat keine eigene Seite; die Suche steht
im `ReferenceService`, weil Werkzeug-Daten bewusst keine `IModuleEntitySource` haben). Offen
bleibt `ContentEntity.Description`: Die Beschreibung wird in ~20 Masken und allen Listen roh
angezeigt — ohne einen zentralen Lade-/Anzeigepfad würde die gespeicherte stabile Form überall
durchsickern; das ist mehr als „nur der Aufruf im Dienst“ und braucht eine eigene Entscheidung.

### F13 — Cutscene-Storyboard mit Bild je Einstellung ✅ umgesetzt

> **Als** Regisseur **möchte ich** jeder Einstellung ein Skizzenbild und eine Dauer geben, **damit**
> das Storyboard wie ein Storyboard aussieht und nicht wie eine Aufzählung.

`CutsceneShot` hat Text und Reihenfolge. Ein Asset je Shot geht über dieselbe GUID-Anhängung wie in
F10, dazu Dauer und Kameranotiz als Spalten. Die Seite wird ein Streifen aus Karten statt einer
Liste — die Drag&Drop-Mechanik gibt es im Story-Zeitstreifen und im Kanban-Board schon zweimal.

*Aufwand: S · Migration: ja · Format: +1*

**Umgesetzt.** `DurationSeconds` und `CameraNote` an `CutsceneShot`, Migration
`CutsceneStoryboard` in allen vier Providern, `FormatVersion` auf **14**. Das Skizzenbild
brauchte keine Spalte — es hängt als Asset an der GUID der Einstellung. Die Seite ist ein
Kartenstreifen mit Drag & Drop.

---

## C. Balance und Auswertung

### F14 — Balancing-Tabelle

> **Als** Balancing-Verantwortlicher **möchte ich** alle Waffen mit ihren Feldern in einer
> sortierbaren Tabelle nebeneinander sehen und Werte direkt in der Zelle ändern, **damit** ich
> Ausreißer erkenne, ohne 40 Masken zu öffnen.

Die Massenbearbeitung setzt **einen** Wert auf viele Entitäten; hier geht es um den umgekehrten
Blick — viele Werte nebeneinander. Alles Nötige ist da: `IModuleEntitySource.LoadForBulkAsync`
(verfolgt, mit Projektgrenze), `ContentFields.CopyValues`, `DynamicFieldInput` je Zelle. Filter nach
Art, Spaltenwahl, Sortierung je Spalte, und eine Spalte „Abweichung vom Mittelwert" macht aus der
Tabelle ein Werkzeug. Speichern je Zelle wie im Übersetzungsraster — eine Balancing-Sitzung geht
über hunderte Änderungen, ein vergessener Klick verlöre sie alle.

*Aufwand: L · Migration: nein · Format: unverändert*

### F15 — Loot-Simulator ✅ umgesetzt

> **Als** Designer **möchte ich** eine Loot-Table zehntausendmal würfeln lassen und die
> Verteilung sehen, **damit** ich weiß, wie lange ein Spieler im Mittel auf das seltene Schwert
> wartet.

`LootRollMode` unterscheidet die beiden Verfahren bereits sauber — die Auswertung fehlt. Ein
Rechenlauf im Speicher, Ergebnis als Tabelle (Erwartungswert je Item, Anteil leerer Würfe, Median
der Versuche bis zum ersten Treffer) plus Balkendiagramm. Der Zufallsgenerator bekommt einen
festen Startwert aus der Oberfläche, damit derselbe Lauf dasselbe Ergebnis liefert. Kein neuer
Datenbestand, reine Auswertung — dasselbe Muster wie der Freischaltungs-Graph.

*Aufwand: S · Migration: nein · Format: unverändert*

**Umgesetzt.** `LootSimulation` als reiner Rechenkern, `LootService.SimulateAsync` daneben,
Seite `/modules/loot/{id}/simulation` mit Balkendiagramm als SVG. Startwert aus der Oberfläche,
Wartezeit als Median, gezählt je Eintrag statt je Item.

### F16 — Wirtschafts-Prüfung ✅ umgesetzt

> **Als** Designer **möchte ich** sehen, wo ein Spieler Geld aus dem Nichts erzeugt — Zutaten beim
> Händler billiger als das Ergebnis, das er verkaufen kann —, **damit** die Wirtschaft nicht beim
> ersten Spieler zusammenbricht.

Alles Nötige liegt vor: `CraftingGraph.SummarizeBaseCost` rechnet ein Rezept auf Grundstoffe
herunter, `TraderOffer` trägt Ein- und Verkaufspreis je Währung. Der Abgleich ist eine Auswertung
über beide: Für jedes Rezept die Summe der Ankaufspreise seiner Zutaten gegen die Verkaufspreise
seiner Ziele. Ein neuer Health Check „Gelddruckmaschine", der wie der Loot-Check meldet statt zu
verbieten. Voraussetzung ist ein Wechselkurs je Währung — heute definiert der Nutzer ihn als Feld
der Währungs-Art, für den Vergleich müsste er eine Spalte werden.

*Aufwand: M · Migration: ja (Wechselkurs) · Format: +1*

**Umgesetzt.** `Currency.ExchangeRate` (Migration `CurrencyExchangeRate` in allen vier
Providern, `FormatVersion` auf **16**) plus `EconomyService` als neunter Health Check. Statt über
`CraftingGraph.SummarizeBaseCost` wird direkt über die Zutaten eines Rezepts gerechnet: Der
Grundstoff-Baum beantwortet „was kostet es von ganz unten“, die Frage hier ist „was kostet es
beim Händler“ — und die stellt sich je Rezeptstufe.

### F17 — Fortschritts-Sicht: „Was hat der Spieler auf Stufe N?" ✅ umgesetzt

> **Als** Designer **möchte ich** eine Zeitleiste über die Spielerstufen sehen, in der steht, welche
> Items, Skills, Quests und Gebiete auf welcher Stufe dazukommen, **damit** ich Lücken und
> Ballungen im Fortschritt erkenne.

Der Freischaltungs-Graph zeigt *woran etwas hängt*, nicht *wann es kommt*. Die Grundlage ist
dieselbe: `ConditionKind.PlayerLevel` in den Slots `Unlock` und `Availability`. Die Auswertung
sortiert alles Freischaltbare nach der niedrigsten Stufenbedingung auf seinem Weg durch den Graphen
und stellt es als Bänder je Stufe dar. Inhalte ohne Stufenbezug landen in einer Spalte „jederzeit".

*Aufwand: M · Migration: nein · Format: unverändert*

**Umgesetzt.** `ProgressionService` über denselben Graphen wie der Freischaltungs-Baum, Seite
`/modules/techtree/fortschritt` mit einem Band je Stufe. Die Stufe erbt sich über die
Voraussetzungen; Inhalte ohne Stufenbezug stehen unter „jederzeit“.

### F18 — Eigene Health-Check-Regeln

> **Als** Nutzer **möchte ich** eigene Prüfungen festlegen („jedes Item braucht ein Sprite", „jeder
> Mob braucht eine Loot-Table", „kein NPC ohne Fraktion"), **damit** die Qualitätsprüfung die Regeln
> meines Projekts kennt und nicht nur die eingebauten.

Die acht eingebauten Prüfungen sind fest verdrahtet. Eine `ContentRule` je Projekt: Modul, optional
Art, Bedingung („Feld X ist leer", „hat kein primäres Sprite", „hat keine Referenz aus Modul Y"),
Schweregrad. Ausgewertet über die `IModuleEntitySource` und angezeigt in denselben zwei Ansichten
wie heute (Dashboard-Band und Statistik-Seite). Bewusst keine freie Skriptsprache — eine Handvoll
Regelarten deckt neunzig Prozent ab und lässt sich in einer Maske erfassen.

*Aufwand: L · Migration: ja · Format: +1*

### F19 — Bedingungen auswerten statt nur verwalten ✅ umgesetzt

> **Als** Designer **möchte ich** einen Spielzustand annehmen (Stufe 12, Nacht, Regen, hat den
> Schlüssel) und sehen, welche Quests, Dialoge, Shop-Posten und Freischaltungen in diesem Zustand
> offen wären, **damit** ich einen Spielstand durchdenken kann, ohne das Spiel zu bauen.

Der Unterbau aus F11 (`ConditionEvaluator`) trägt hier eine eigene Seite: links der angenommene
Zustand, rechts alles, was er öffnet und schließt. Der Health Check „unerfüllbare Bedingungen"
meldet heute nur, was sich *ohne* Kenntnis des Spielzustands sicher sagen lässt — mit einem
angenommenen Zustand lässt sich viel mehr sagen, ohne die bewusste Zurückhaltung des Checks
aufzugeben.

*Aufwand: M (nach F11) · Migration: nein · Format: unverändert*

**Umgesetzt.** Seite `/modules/techtree/zustand` beim Freischaltungs-Modul, verlinkt von Graph
und Fortschritt. Die Zustandsleiste ist als `GameStateAssumptionPanel` aus der Durchspiel-Seite
herausgelöst — zwei Ansichten, ein Kern. `ConditionStateService` sammelt alle Bedingungssätze
des Projekts und löst die Besitzer auf: ganze Entitäten über die `IModuleEntitySource`,
Teilobjekte (Händler-Posten, Dialogzeilen und -antworten, Quest-Ziele, Spawn-Regeln) über
ausdrückliche Nachschlagewege mit dem Elternteil als Sprungziel; Waisen stehen als „unbekannter
Besitzer“ da statt zu fehlen. Gruppiert wird nach Slot, Geschlossenes zuerst samt der
Bedingungen, an denen es liegt; ein Schalter blendet Offenes aus.

---

## D. Zusammenarbeit und Prozess

### F20 — Bearbeitungsstatus je Entität ✅ weitgehend umgesetzt

> **Als** Teammitglied **möchte ich** an jeder Entität sehen, ob sie Entwurf, in Arbeit, im Review
> oder fertig ist, und danach filtern, **damit** ich weiß, worauf ich mich verlassen kann.

Eine `ContentStatus`-Spalte auf `ContentEntity` (Enum wie `NpcKind`, feste Zahlen) — sie wirkt damit
in allen 22 Inhaltsmodulen auf einmal. Der Filter gehört in jede Modul-Liste und in die globale
Suche, die Zahlen in ein Dashboard-Band („12 Entwürfe, 4 im Review"). Der Export bekommt eine
Option „nur Fertiges" — das ist der eigentliche Zweck: einen halbfertigen NPC nicht versehentlich
ins Spiel zu liefern.

*Aufwand: M · Migration: ja · Format: +1*

**Umgesetzt.** `ContentStatus` als Spalte an `ContentEntity`, Migration `ContentStatus` in allen
vier Providern, `FormatVersion` auf **13**. Auswahl in jeder Bearbeitungsmaske
(`EntityStatusSelect` im Referenz-Panel), Massenbearbeitung über `BulkEditService.SetStatusAsync`,
Dashboard-Band „Bearbeitungsstand“ und der Export-Filter als **Mindeststand**.
**Nachgezogen mit F27:** Der Filter nach Bearbeitungsstand steht in den gespeicherten
Ansichten (`/modules/views`) — dort, wo auch nach Art, Feldwert, Tag und Sprite gefiltert wird.
Die einzelnen Modul-Listen behalten ihre eigenen, schlanken Filter: Eine zweite vollständige
Filterleiste je Liste wäre zwanzigmal dieselbe Pflege.

### F21 — Kommentare an Entitäten ✅ umgesetzt

> **Als** Teammitglied **möchte ich** an einer Entität eine Anmerkung hinterlassen („Schaden ist zu
> hoch, siehe Playtest vom 3.") und sie als erledigt markieren, **damit** Rückmeldungen dort stehen,
> wo sie hingehören, und nicht im Chat.

`ContentComment` nach dem Muster des Änderungsprotokolls: GUID-Anhängung über `OwnerEntityId` +
`OwnerModuleKey`, Urheber als Momentaufnahme (nicht als Verweis — dieselbe Überlegung wie beim
`ChangeLogEntry`), `ResolvedAtUtc`. Als Abschnitt in jede Bearbeitungsmaske neben „Geschichte" und
„Referenzen" — eine Komponente, kein Modul-Aufwand. Werkzeug-Daten wie das Änderungsprotokoll: nicht
im Export, überstehen den ersetzenden Import. Erwähnungen (`@benutzer`) speisen F23.

*Aufwand: M · Migration: ja · Format: unverändert (nicht im Export)*

**Umgesetzt.** `ContentComment` plus `ContentCommentService`, Migration `ContentComments` in
allen vier Providern, `EntityCommentsPanel` in jeder Maske und ein Dashboard-Band mit den
offenen. Erwähnungen (`@benutzer`) stehen noch aus — sie gehören zu F23.

### F22 — ToDo-Karten mit Verknüpfung, Zuständigem und Fälligkeit ✅ umgesetzt

> **Als** Projektleiter **möchte ich** eine Kanban-Karte mit einer Entität, einem Zuständigen und
> einem Datum versehen, **damit** aus dem Board eine Arbeitsplanung wird und nicht bloß eine
> Merkliste.

`KanbanCard` trägt heute Titel, Notiz und Sortierung. Vier Spalten mehr: `AssignedUserId`,
`DueDate`, `Color`/`Label`, und `TargetModuleKey` + `TargetEntityId` für die Verknüpfung. Der
Gegenzug ist wertvoller als die Karte selbst: In der Bearbeitungsmaske einer Entität steht, welche
offenen Aufgaben an ihr hängen. Dazu ein Dashboard-Band „meine Aufgaben" und ein Filter „fällig
diese Woche". Bleibt Werkzeug-Daten — nicht im Export.

*Aufwand: M · Migration: ja · Format: unverändert*

**Umgesetzt.** `AssignedUserId`, `DueDate`, `Color`, `Label`, `TargetModuleKey` und
`TargetEntityId` an `KanbanCard`, Migration `KanbanCardDetails` in allen vier Providern. Der
Gegenzug steht als `EntityTasksPanel` in jeder Bearbeitungsmaske. Das Dashboard-Band
„Meine Aufgaben“ (`KanbanService.GetMyOpenCardsAsync`, Fälliges zuerst, Überfälliges rot) und
der Filter „Nur fällig diese Woche“ auf der Board-Seite sind nachgezogen; die Kartenfläche
zeigt seither auch Farbe, Etikett, Fälligkeit und Zuständigen.

### F23 — Aktivitäts-Feed und Benachrichtigungen ✅ umgesetzt

> **Als** Teammitglied **möchte ich** beim Anmelden sehen, was sich seit meinem letzten Besuch
> geändert hat und wo ich erwähnt wurde, **damit** ich in einem Projekt mit zwei oder drei Leuten
> den Anschluss nicht verliere.

Das Änderungsprotokoll hat die Daten bereits vollständig — was fehlt, ist ein „gelesen bis"-Zeitpunkt
je Benutzer (eine Spalte an `AppUser`) und eine Ansicht, die daraus einen Feed macht: gruppiert nach
Entität statt nach Einzeländerung, sonst ertrinkt er in Speichervorgängen. Erwähnungen aus F21 und
Zuweisungen aus F22 kommen als eigene Sorte dazu. Kein Mailversand — das Tool wird self-hosted von
kleinen Teams betrieben, ein Glockensymbol in der Appbar reicht.

*Aufwand: M · Migration: ja (eine Spalte) · Format: unverändert*

**Umgesetzt.** `ActivityFeedService` ohne eigenen Datenbestand, `AppUser.FeedReadAtUtc` als
einzige neue Spalte (Migration `ActivityFeedMarker`), Seite `/aktivitaet` und die Glocke in der
Appbar. Erwähnungen kommen über „@Name“ aus den Anmerkungen (F21), Zuweisungen aus den
Kanban-Karten (F22).

### F24 — Papierkorb ✅ umgesetzt

> **Als** Nutzer **möchte ich** eine versehentlich gelöschte Entität zurückholen, **damit** ein
> Fehlklick nicht heißt, den letzten Exportstand einzuspielen und alles seitdem zu verlieren.

Heute ist Löschen endgültig: `ExecuteDeleteAsync` plus `EntityCleanup` plus `AssetService`-Aufräumen.
Kein Soft-Delete-Flag auf `ContentEntity` — das zöge eine Filterbedingung durch jede Abfrage des
ganzen Bestands und wäre die Sorte Änderung, die man an einer Stelle vergisst. Stattdessen dasselbe
Verfahren wie beim Duplizieren, nur rückwärts: Vor dem Löschen schreibt `EntityDuplicationService`
den kompletten JSON-Baum der Entität (samt Feldwerten, Bedingungen, Kind-Sammlungen) in eine
`RecycleBinEntry`-Zeile; Wiederherstellen liest ihn mit den **originalen** GUIDs zurück. Aufbewahrung
wie bei den Exportständen: Höchstalter und Obergrenze als Konfiguration, aufgeräumt vom bestehenden
`ChangeLogMaintenance`-Hintergrunddienst.

*Aufwand: L · Migration: ja · Format: unverändert*

**Umgesetzt** genau so: `RecycleBinEntry` mit dem JSON-Baum, Migration `RecycleBin` in allen
vier Providern, aufgeräumt vom bestehenden `ChangeLogMaintenance` (ein zweiter Hintergrunddienst
für dieselbe Frage wäre einer zu viel). Fünf Dinge dahinter: **Erfasst wird in
`EntityCleanup.DeleteForEntityAsync`** — der einen Stelle, durch die jeder Löschpfad läuft und
die seit F5 das `DbSet` ohnehin in der Hand hat; ein Aufruf je Modul-Dienst wäre der, den ein
neues Modul vergisst. **Vor dem Auflösen der Varianten**, sonst stünden deren übernommene Werte
doppelt im Baum. **Sprites kommen nicht mit zurück**: Beim Löschen verschwinden auch die
Dateien, und die ließen sich aus einer Datenbankzeile nicht wiederherstellen — dafür gibt es die
Exportstände; die Seite sagt das ausdrücklich. Eine **belegte GUID blockt** das Zurückholen
(sichtbar als Chip, nicht erst beim Klick) — sonst überschriebe eine Wiederherstellung den
Datensatz, der inzwischen dort steht. Und der Eintrag **fällt mit dem Zurückholen weg**: Er
beschreibt einen Zustand, den es nicht mehr gibt. Aufbewahrung als Konfiguration
(`RecycleBin:MaxAgeDays`, Vorgabe **30**; `RecycleBin:MaxPerProject`, Vorgabe aus;
`RecycleBin:Enabled`), die Seite steht unter `/modules/changelog/papierkorb`.

### F25 — „Wird gerade bearbeitet von …" ✅ umgesetzt

> **Als** Teammitglied **möchte ich** beim Öffnen einer Maske sehen, dass jemand anders sie schon
> offen hat, **damit** ich den Konflikt vermeide, statt ihn beim Speichern gemeldet zu bekommen.

Die Schreibkonflikt-Erkennung meldet den Zusammenstoß — sie verhindert ihn nicht. Ein Singleton
nach dem Muster des `WhiteboardNotifier` (der die Mechanik bereits vormacht: Absender-Marke, damit
sich die auslösende Ansicht nicht selbst neu lädt) hält je Entitäts-GUID die offenen Sitzungen mit
Zeitstempel; ein Eintrag verfällt nach wenigen Minuten ohne Lebenszeichen. Reiner Arbeitsspeicher,
keine Tabelle — das Tool läuft in einem Prozess.

*Aufwand: S · Migration: nein · Format: unverändert*

**Umgesetzt.** `EditingPresence` als Singleton in der Datenschicht, `EditingPresenceBanner`
unter dem `ModuleHeader` jeder Bearbeitungsmaske. Verfall nach drei Minuten ohne Lebenszeichen,
Herzschlag der Maske alle 45 Sekunden, Sitzungskennung je Maske statt je Benutzer.

---

## E. Bedienung

### F26 — Kommandopalette ✅ umgesetzt

> **Als** Vielnutzer **möchte ich** mit Strg+P ein Feld öffnen, in das ich „ei schwert" tippe und
> direkt in der Maske lande — oder „export" tippe und die Aktion auslöse, **damit** ich nicht durch
> Modulleiste und Liste klicken muss.

Die globale Suche liegt bereits über alle Module (`SearchService` über die `IModuleEntitySource`,
plus Feldwerte und Dialogzeilen), `gdm-shortcuts.js` und die `KeyboardShortcuts`-Komponente hängen
schon im `MainLayout`. Was fehlt, ist der Sprung von „Treffer anzeigen" zu „Aktionen anbieten":
Module öffnen, neue Entität anlegen, Projekt wechseln, Sprache umstellen, Export starten. Unscharfe
Suche über die Anfangsbuchstaben statt `Contains` — bei „ei schwert" soll das Eisenschwert kommen.

*Aufwand: M · Migration: nein · Format: unverändert*

**Umgesetzt.** `CommandPalette` im `MainLayout`, Strg+P über `gdm-shortcuts.js`. Aktionen
(Module öffnen, neu anlegen, Projekte, Einstellungen, Export) plus Entitäten aus demselben
`SearchService` wie die Appbar; unscharfer Vergleich als `FuzzyMatch` in der Datenschicht.

### F27 — Gespeicherte Suchen und Listenansichten ✅ umgesetzt

> **Als** Nutzer **möchte ich** einen Filter („alle Waffen ohne Sprite, Schaden über 50, Status
> Entwurf") benennen und wiederfinden, samt der Spalten, die ich sehen will, **damit** ich ihn nicht
> jedes Mal neu zusammenklicke.

Zwei Dinge in einem: eine gespeicherte Filterdefinition und eine gespeicherte Spaltenwahl. Beides
gehört je Projekt und je Benutzer in eine Tabelle (`SavedView`) mit dem Filter als JSON — dieselbe
Überlegung wie bei den Kurven: Als Text geht er ohne Zutun durch Export und Duplizieren, und neue
Filterarten brauchen keine Migration. Voraussetzung ist ein einheitlicher Filter-Aufbau über die
`IModuleEntitySource`, den es heute nicht gibt — die Modul-Listen filtern je selbst.

*Aufwand: L · Migration: ja · Format: unverändert*

**Umgesetzt.** `ContentFilter` als einheitliche Filterdefinition, `IModuleEntitySource.QueryAsync`
als der gemeinsame Weg, `SavedView` mit dem Filter als JSON (Migration `SavedViews` in allen vier
Providern), Werkzeug-Modul `ModuleKeys.Views` unter `/modules/views`. Fünf Entscheidungen:
**Eine Seite statt einer Filterleiste in jeder Modul-Liste** — dieselbe Begründung wie bei der
Massenbearbeitung: Die Listen sind je Modul eigen gebaut, dieselbe Leiste zwanzigmal nachzubauen
hieße, sie zwanzigmal zu pflegen. Gefiltert wird **in der Datenbank, soweit es geht** (Name, Art,
Stand, Vorbild) und **im Speicher, wo es sein muss** (Feldwerte, Tags, Sprites) — die hängen ohne
Fremdschlüssel an der GUID. Der **Vergleichswert steht als Text**, auch für Zahlen: Der Filter
geht als JSON in die Ansicht, und ein neuer Feldtyp soll dort keine Migration verlangen.
**Unterarten werden bei jeder Abfrage neu aufgelöst** und nicht mitgespeichert — wer später eine
Unterart anlegt, findet sie in seiner Ansicht wieder, ohne sie neu zu wählen. Und die Ansicht
gehört **dem Benutzer**, nicht dem Projekt: Sie ist eine Arbeitsgewohnheit, keine Aussage über
den Spielinhalt.

Damit ist auch der offene Rest aus **F20** erledigt: Der Statusfilter steht in dieser Ansicht;
die einzelnen Modul-Listen behalten bewusst ihre eigenen, schlanken Filter.

### F28 — Favoriten und „zuletzt besucht" ✅ umgesetzt

> **Als** Nutzer **möchte ich** die fünf Entitäten anheften, an denen ich diese Woche arbeite,
> **damit** sie nicht in der Liste mit 300 Items untergehen.

Das Dashboard-Band „Weiterarbeiten" zeigt das zuletzt **Geänderte** — das ist nicht dasselbe wie das
zuletzt Angesehene und schon gar nicht das absichtlich Angeheftete. `UserPin` (Benutzer, Modul,
GUID) als kleine Tabelle, ein Sternsymbol in jeder Maske und Liste, ein Dashboard-Band daneben.

*Aufwand: S · Migration: ja · Format: unverändert*

**Umgesetzt** als Favoriten: `UserPin` plus `UserPinService`, Migration `UserPins` in allen vier
Providern, Sternsymbol im Referenz-Panel jeder Maske und ein Dashboard-Band daneben. „Zuletzt
besucht“ bleibt bewusst draußen — das zuletzt Geänderte zeigt „Weiterarbeiten“ bereits, und ein
drittes, nur beiläufig gefülltes Band sagte weniger als der Stern.

### F29 — Deutsche und englische Modulseiten fertigstellen

> **Als** englischsprachiger Nutzer **möchte ich** auch die Modulseiten auf Englisch sehen, **damit**
> nicht nach dem Umschalten der Rahmen englisch und der Inhalt deutsch ist.

Der bekannte offene Punkt aus [CLAUDE.md](../CLAUDE.md): Übersetzt sind die geteilten Ebenen
(`DataMessages`, Modul-/Bedingungs-/Feldtyp-Beschriftungen, Rahmen, Dashboard, Start- und
Fehlerseiten), die rund 60 Modulseiten zeigen weiterhin Deutsch. Codeänderungen braucht es dafür
keine — Schlüssel und deutsche Werte je `.resx` einsammeln, übersetzen, als `<Datei>.en.resx`
danebenlegen. Es ist Fleißarbeit, aber es ist die auffälligste Unfertigkeit im Tool. Ein Testfall
nach dem Muster von `LanguageTests`, der **jede** neutrale `.resx` mit ihrer englischen vergleicht
und fehlende Schlüssel meldet, macht daraus eine abhakbare Aufgabe.

*Aufwand: L (aber trivial) · Migration: nein · Format: unverändert*

### F30 — Beispielprojekt zum Laden ✅ umgesetzt

> **Als** neuer Nutzer **möchte ich** beim ersten Start ein gefülltes Beispielprojekt einspielen
> können, **damit** ich sehe, wie Arten, Felder, Bedingungen und Referenzen zusammenspielen, statt
> vor 33 leeren Modulen zu stehen.

Der Import liest ein Export-ZIP — ein mitgeliefertes Demoprojekt ist also nur eine Datei im
Anwendungsverzeichnis und ein Knopf in der Ersteinrichtung. Inhaltlich ein kleines Fantasy-Set:
zwei Item-Arten mit Feldvererbung, ein Händler mit bedingtem Angebot, ein Rezeptbaum über drei
Stufen, ein Dialog mit Verzweigung, eine Karte mit Gebieten. Damit ist zugleich ein realistischer
Testbestand für die Entwicklung da.

*Aufwand: M · Migration: nein · Format: unverändert*

**Umgesetzt** — als `SampleProjectService` statt als mitgeliefertes ZIP: Ein ZIP im
Anwendungsverzeichnis veraltete bei jeder `FormatVersion`-Erhöhung still; der Seeder geht durch
die echten Modul-Dienste und ist damit zu jedem Stand gültig. Der Knopf steht auf `/projekte`
(nicht in der Ersteinrichtung — die ist statisch gerendert und liegt vor der Anmeldung, der
Seeder braucht aber einen angemeldeten Urheber mit Schreibrecht) und wechselt nach dem Anlegen
direkt ins Beispiel; das leere Dashboard verlinkt als vierter Einstieg dorthin. Die Inhalte
kommen aus `DataMessages` (deutsch und englisch), der Name weicht bei Wiederholung aus
(„Beispielprojekt 2“), und `SampleProjectTests` stellt sicher, dass die Health Checks am
Beispiel nichts zu beanstanden haben.

### F31 — Tastatur-Übersicht und mehr Shortcuts ✅ umgesetzt

> **Als** Vielnutzer **möchte ich** mit `?` eine Übersicht der Tastenkürzel sehen und in jeder Liste
> mit den Pfeiltasten navigieren, **damit** ich die Hand nicht ständig zur Maus nehme.

`gdm-shortcuts.js` kann heute drei Dinge: Strg+K in die Suche, Strg+S auf den mit `data-gdm-save`
markierten Knopf, Alt+Buchstabe für die Navigation. Sinnvolle Ergänzungen: `n` für „neu" in jeder
Liste, `e` für „bearbeiten" auf dem markierten Eintrag, `Entf` mit Rückfrage, `Esc` schließt Dialoge,
`?` zeigt die Übersicht. Der Speichern-Knopf sollte in **allen** Masken das Attribut tragen — heute
nur in drei.

*Aufwand: S · Migration: nein · Format: unverändert*

**Umgesetzt.** `?` zeigt den `ShortcutOverviewDialog`, `n` legt neu an, `e` öffnet einen
Eintrag; der Speichern-Knopf trägt `data-gdm-save` jetzt in allen Masken, die „Neu“-Knöpfe der
Listen `data-gdm-new`. Die Alt-Routen stehen in C# statt im Skript. `Esc` blieb draußen —
MudBlazor schließt seine Dialoge bereits selbst damit.

---

## F. Produktion und Assets

### F32 — Sprite-Sheet zerlegen ✅ umgesetzt

> **Als** Grafiker **möchte ich** ein Atlas-Bild hochladen, ein Raster angeben und daraus einzelne
> Sprites schneiden lassen, **damit** ich nicht 24 Einzeldateien exportieren muss.

Die Asset-Bibliothek nimmt Einzeldateien. `ImageDimensionReader` liest bereits Breite und Höhe aus
dem Dateikopf ohne Bildbibliothek — für das Schneiden selbst führt daran allerdings kein Weg vorbei
(`SixLabors.ImageSharp` wäre die naheliegende, plattformneutrale Wahl; `System.Drawing` fällt unter
Linux aus). Alternative ohne Abhängigkeit: nicht schneiden, sondern nur **Ausschnitte verwalten** —
ein `AssetRegion` (x, y, Breite, Höhe, Name) am Asset, das der Export als Metadaten mitgibt und die
Engine selbst anwendet. Das passt besser zur Linie des Hauses und ist für Unity sogar der
gebräuchlichere Weg.

*Aufwand: M · Migration: ja · Format: +1*

**Umgesetzt** als der zweite Weg — Ausschnitte verwalten statt schneiden: `AssetRegion` als
Kind-Sammlung des Assets, Migration `AssetRegions` in allen vier Providern, `FormatVersion` auf
**19**. Gemessen wird in **Pixeln** und nicht relativ wie beim `MapMarker`: Ein Raster ist in
Pixeln definiert, die Engine erwartet ein Pixel-Rechteck, und die Maße stehen am Asset. Das
Raster (`BuildGridAsync` — Zellmaß, Rand, Abstand) erzeugt einen **Vorschlag und speichert
nicht**; Zellen über den Bildrand hinaus entstehen gar nicht erst. Ohne lesbare Bildmaße gibt es
kein Raster, aber weiterhin Ausschnitte von Hand. Oberfläche als `AssetRegionsDialog` in der
Bibliothek, mit dem Bild und einem SVG-Overlay der Rechtecke. Nebenbei berichtigt: Die früheren
Fassungen eines Assets standen als immer leere Liste im Archiv — sie sind Werkzeug-Daten und
stehen jetzt in `IsUnloadedCollection`.

### F33 — Asset ersetzen statt neu hochladen ✅ umgesetzt

> **Als** Grafiker **möchte ich** die Datei hinter einem Asset austauschen, ohne dass sich die GUID
> ändert, **damit** alle Verweise darauf bestehen bleiben und der Diff zeigt, dass es dieselbe
> Grafik in neu ist.

Heute heißt „neue Fassung" praktisch: hochladen, primär setzen, altes löschen — drei Schritte, und
der Verweis wandert. Ein „Ersetzen"-Knopf schreibt die neue Datei unter denselben `StorageKey`
(bzw. einen neuen mit Fortschreibung der Maße) und lässt die Zeile stehen. Dazu passend eine
Fassungs-Historie: die vorherige Datei behalten wie einen Exportstand, mit derselben
Aufbewahrungsregel.

*Aufwand: M · Migration: ja (Fassungen) · Format: +1*

**Umgesetzt.** `AssetService.ReplaceAsync` plus `AssetVersion` (Migration `AssetVersions` in
allen vier Providern), Knopf an jedem Sprite. Der `StorageKey` wird bewusst **neu** vergeben —
der Endpunkt cached unbefristet. `FormatVersion` blieb unverändert: Die Fassungen sind
Werkzeug-Daten wie das Änderungsprotokoll und stehen in keinem Export.

### F34 — Massen-Upload mit Namenszuordnung ✅ umgesetzt

> **Als** Grafiker **möchte ich** 40 Dateien auf einmal hochladen und sie automatisch den Entitäten
> zuordnen lassen, deren Namen sie tragen (`eisenschwert.png` → Item „Eisenschwert"), **damit** ich
> nicht 40-mal dieselbe Maske bediene.

Der `AssetUpload` nimmt Dateien bereits ohne Besitzer entgegen — genau der Zustand „hochgeladen,
noch nicht zugeordnet". Was fehlt, ist der Abgleich: Dateiname normalisieren, über die
`IModuleEntitySource` in allen Modulen nach dem Namen suchen, Vorschlagsliste zur Bestätigung
zeigen (nie stillschweigend zuordnen — bei zwei gleichnamigen Entitäten in verschiedenen Modulen
wäre die Wahl geraten), dann in einem Rutsch zuordnen und das erste je Entität als primär setzen.

*Aufwand: S · Migration: nein · Format: unverändert*

**Umgesetzt.** `AssetService.SuggestOwnersAsync`/`AssignOwnersAsync` plus
`AssetOwnerMatchDialog` in der Bibliothek. Verglichen wird über eine Normalform ohne
Trennzeichen; ein eindeutiger Treffer ist vorgewählt, mehrere bleiben zur Wahl.

### F35 — Verwaiste Dateien im Speicher finden ✅ umgesetzt

> **Als** Betreiber **möchte ich** wissen, welche Dateien im Asset-Verzeichnis liegen, zu denen es
> keine Datenbankzeile mehr gibt, **damit** der Speicher nicht unbemerkt zuwächst.

Der Health Check „verwaiste Sprites" prüft die Gegenrichtung: `Asset`-Zeilen, deren Besitzer es
nicht mehr gibt. Der umgekehrte Fall — Dateien ohne Zeile — entsteht bei jedem abgebrochenen Import
und jedem Fehler zwischen Dateisystem und Transaktion. Ein Wartungslauf in den Einstellungen, der
das Verzeichnis gegen die `StorageKey`s hält und **anzeigt statt löscht**; das Löschen ist ein
zweiter, ausdrücklicher Klick. Dieselbe Zurückhaltung wie bei `ExportSnapshotService.PruneAsync`,
das fremde Dateien im Exportverzeichnis bewusst stehen lässt.

*Aufwand: S · Migration: nein · Format: unverändert*

**Umgesetzt.** `IAssetStorage.ListKeys` plus `AssetService.FindOrphanedFilesAsync` und
`DeleteOrphanedFilesAsync`, Oberfläche unter „Einstellungen → Speicher“. Angezeigt statt
gelöscht; vor dem Löschen wird erneut geprüft.

---

## G. Import, Export und Integration

### F36 — Schreibende API

> **Als** Entwickler eines eigenen Werkzeugs **möchte ich** Inhalte über die API anlegen und ändern,
> **damit** ich ein Skript schreiben kann, das 200 Items aus einer Tabelle einspielt, ohne den
> Umweg über CSV und Browser.

Die API ist heute bewusst nur lesend, und die Begründung in [CLAUDE.md](../CLAUDE.md) steht: Ein
schreibender Schlüssel wäre ein zweiter Weg an Rechteprüfung, Änderungsprotokoll und
Schreibkonflikt-Erkennung vorbei. Das ist kein Argument gegen das Schreiben, sondern eine
Anforderungsliste: Der Schreibpfad muss durch dieselben Modul-Dienste laufen wie die Oberfläche
(dann greifen `WriteGuardInterceptor` und `ChangeLogInterceptor` von selbst), der API-Schlüssel
braucht ein eigenes Schreibrecht und einen Benutzerbezug für das Protokoll, und der Zeitstempel aus
`StageValuesAsync` muss als `If-Match` mitgegeben werden. Dazu Idempotenz-Schlüssel, damit ein
wiederholter Aufruf nach einem Verbindungsabbruch nichts doppelt anlegt.

*Aufwand: L · Migration: ja (Rechte am Schlüssel) · Format: unverändert*

### F37 — Webhooks

> **Als** Betreiber **möchte ich**, dass das Tool bei Änderungen eine URL aufruft, **damit** ein
> Build-Server den Export automatisch abholen kann, wenn sich etwas geändert hat.

Der `ChangeLogInterceptor` sieht bereits jede Änderung an genau einer Stelle. Ein
`WebhookDispatcher` als Hintergrunddienst mit Warteschlange (nicht im `SaveChanges` — eine hängende
HTTP-Anfrage darf keine Transaktion aufhalten), Ziel-URL und Modulfilter je Eintrag,
Wiederholversuche mit wachsendem Abstand, ein signierter Kopfeintrag. Zusammen mit F36 wird das Tool
damit von einer Insel zu einem Glied in der Kette.

*Aufwand: M · Migration: ja · Format: unverändert*

### F38 — Export-Profile ✅ umgesetzt

> **Als** Nutzer **möchte ich** einen Export benennen und speichern („Unity, nur Fertiges, ohne
> Werkzeug-Module, englisch"), **damit** ich ihn mit einem Klick wiederhole statt jedes Mal
> dieselben fünf Schalter zu setzen.

`ExportTarget` ist heute die einzige Einstellung und ändert nur den Wurzelpfad. Ein `ExportProfile`
je Projekt bündelt Ziel, Modulauswahl, Sprachauswahl, Statusfilter (F20) und den Schalter für
Asset-Dateien. Das ist zugleich die Voraussetzung für F39 — ein Zeitplan braucht etwas, das er
ausführen kann.

*Aufwand: S · Migration: ja · Format: +1*

**Umgesetzt.** `ExportProfile` plus `ExportProfileService`, Migration `ExportProfiles` in allen
vier Providern, `FormatVersion` auf **15**. Der Export nimmt jetzt zusätzlich eine Modulauswahl
entgegen; abgewählte Module stehen als leere Liste im Archiv. Die Sprachauswahl bleibt draußen —
der Export schreibt ohnehin je Zielsprache eine eigene Zeichenketten-Tabelle, und die Sprachwahl
fällt laut Konzept im Spiel.

### F39 — Exportstände nach Zeitplan ✅ umgesetzt

> **Als** Betreiber **möchte ich** jeden Abend automatisch einen Exportstand anlegen lassen,
> **damit** eine Sicherung existiert, auch wenn niemand daran gedacht hat.

Das Sicherheitsnetz greift heute nur bei zerstörenden Aktionen (ersetzender Import, Projekt löschen).
Der `ChangeLogMaintenance`-Hintergrunddienst zeigt das Muster; hier ruft ein zweiter
`ExportSnapshotService.CreateAsync` je Projekt auf, mit `Exports:ScheduleCron` als Konfiguration —
dieselbe Linie wie Passwortrichtlinie und Aufbewahrung, keine Tabelle. Wichtig: nur anlegen, wenn
sich seit dem letzten Stand etwas geändert hat, sonst füllt sich das Verzeichnis mit identischen
Archiven und verdrängt die interessanten.

*Aufwand: S · Migration: nein · Format: unverändert*

**Umgesetzt.** `ExportSnapshotService.CreateScheduledAsync` plus `ScheduledExportSnapshots` als
Hintergrunddienst. Statt `Exports:ScheduleCron` eine schlichte Uhrzeit `Exports:ScheduleTime`
(`HH:mm`) — ein Cron-Parser wäre eine Fremdbibliothek für eine Angabe aus Stunde und Minute.
Angelegt wird nur bei Änderungen seit dem letzten Stand.

### F40 — Design-Dokument erzeugen ✅ umgesetzt

> **Als** Projektleiter **möchte ich** aus dem Bestand ein lesbares Dokument erzeugen — Story,
> Figuren, Fraktionen, Quests mit Bildern, als HTML zum Ausdrucken —, **damit** ich einem Publisher
> oder einem neuen Teammitglied etwas in die Hand geben kann.

Das Export-ZIP ist für Maschinen, die Oberfläche für die tägliche Arbeit; für „jemandem etwas
zeigen" gibt es nichts. Eine Kapitelauswahl (welche Module, in welcher Reihenfolge), eine feste
Vorlage je Modul, und die Ausgabe als eigenständige HTML-Datei mit eingebetteten Bildern als
`data:`-URI — dann ist es eine Datei, die man verschicken kann. Der Weg über den Browser-Druck
liefert das PDF, ohne eine PDF-Bibliothek einzuziehen.

*Aufwand: M · Migration: nein · Format: unverändert*

**Umgesetzt.** `DesignDocumentService` baut die eigenständige HTML-Datei (Kapitel Story,
Fraktionen, Figuren, Quests, Items in fester Reihenfolge; primäre Sprites als `data:`-URI bis
1,5 MB, Story-Text über `SimpleMarkdown` mit Erwähnungen als Anzeigename). Download über
`/export/design/{projectId}?chapters=…` (Exportrecht), Kapitelauswahl als Abschnitt auf der
Export-Seite. Druckfreundliche feste Vorlage — das PDF liefert der Browser-Druck. Bewusst kein
eigenes Format: vollständig abgeleitet, kein Import, keine `FormatVersion`. Benutzerdefinierte
Felder stehen (noch) nicht im Dokument — die Kapitel zeigen Name, Art, Beschreibung und Bild.

### F41 — Git-freundlicher Export

> **Als** Entwickler **möchte ich** den Export als eine Datei je Entität statt einer je Modul,
> **damit** ich ihn in Git legen kann und der Diff zeigt, welche Entität sich geändert hat, statt
> einer 4000-Zeilen-Datei.

Der Export schreibt heute `content/items.json` mit allem darin — stabil sortiert, also diffbar, aber
eine geänderte Zeile macht die ganze Datei geändert. Ein zweites Ablage-Muster (`content/items/<guid>.json`),
gewählt im Export-Profil aus F38. Der Import muss beide lesen können; die Zuordnung Datei → Modul
steht ohnehin schon zentral in `ExportFormat`. Der Dateiname sollte die GUID tragen und nicht den
Namen — ein Umbenennen soll keine Datei verschieben.

*Aufwand: M · Migration: nein · Format: +1*

### F42 — Teil-Import mit Konfliktanzeige

> **Als** Nutzer **möchte ich** aus einem fremden Export einzelne Module oder Entitäten übernehmen
> und vorher sehen, was sich dadurch ändert, **damit** ich Inhalte zwischen Projekten austauschen
> kann, ohne alles zu ersetzen.

Der Import ist heute bewusst Alles-oder-nichts. Die beiden Bausteine liegen aber schon vor: Der
Diff der Exportstände vergleicht Entität für Entität über die GUID (`JsonNode.DeepEquals`) und
meldet neu/entfernt/geändert; `GuidRemap` kann GUIDs tauschen, wenn eine Entität als Kopie statt als
Überschreibung kommen soll. Zusammen ergibt das einen Auswahl-Import: Diff anzeigen, Häkchen setzen,
je Konflikt „behalten / überschreiben / als Kopie" wählen. Das ist der Weg, auf dem ein Team eine
gemeinsame Item-Basis pflegt.

*Aufwand: L · Migration: nein · Format: unverändert*

### F43 — Engine-Pakete: Unity-Package und Godot-Addon

> **Als** Unity-Entwickler **möchte ich** ein Paket in mein Projekt ziehen, das die exportierten
> Daten einliest und mir im Editor ein Auswahlfenster zeigt, **damit** ich GUIDs nicht abtippe.

Die Engine-Presets erzeugen bereits die passenden Typen (ScriptableObject-Klasse plus JSON für
Unity, DataTable-CSV für Unreal, `.tres` für Godot) — die Seite der Engine fehlt. Ein kleines
Unity-Package (Importer für den JSON-Ordner, `[GdmReference]`-Attribut mit Property-Drawer,
Auswahlfenster) und ein Godot-Addon in GDScript. Zusammen mit der lesenden API sogar live: Das
Fenster fragt `/api/v1/…` und braucht keinen Export.

*Aufwand: L · Migration: nein · Format: unverändert*

---

## H. Betrieb und Sicherheit

### F44 — Sicherung und Wiederherstellung aus der Oberfläche

> **Als** Betreiber **möchte ich** die gesamte Installation sichern und zurückspielen — alle
> Projekte, Benutzer, Boards, das Protokoll —, **damit** ich beim Umzug auf einen neuen Server nicht
> Datenbank und Verzeichnisse von Hand zusammensuche.

Der Exportstand sichert ein Projekt, nicht die Installation: Benutzer, API-Schlüssel, Kanban-Boards,
Whiteboards, Änderungsprotokoll und die lokalen Einstellungen stehen bewusst nicht im Export. Ein
Installations-Archiv (alle Projekte plus die Werkzeug-Daten plus `appsettings.Local.json` ohne
Geheimnisse) schließt die Lücke. Beim Zurückspielen dieselbe Vorsicht wie beim ersetzenden Import:
vorher ein Sicherheitsnetz, und es reißt nicht.

*Aufwand: M · Migration: nein · Format: eigenes Format*

### F45 — Rollen statt Rechte je Benutzer ✅ umgesetzt

> **Als** Verwalter **möchte ich** Rollen anlegen („Autor", „Grafiker", „Nur lesen") und Benutzern
> zuweisen, **damit** ich beim zehnten Konto nicht zum zehnten Mal dieselben Häkchen setze.

`AppUser` trägt die Rechte heute direkt (`CanWrite`, `AllowedModuleKeys` als kommagetrennte
Textspalte, `CanExport`, `CanImport`). Eine `UserRole`-Tabelle mit denselben Feldern und einem
Verweis am Benutzer; `UserPermissions.For` bleibt die eine Stelle, an der aufgelöst wird — dort
kommt die Rolle als Vorgabe hinzu, das Konto darf sie überschreiben. Die Rechte wandern weiterhin
als Ansprüche ins Cookie und gelten ab der nächsten Anmeldung.

*Aufwand: M · Migration: ja · Format: unverändert*

**Umgesetzt.** `UserRole` plus `AppUser.RoleId` (SetNull) und der Schalter `OverridesRole`,
Migration `UserRoles` in allen vier Providern. Abgewichen wird **ganz oder gar nicht** (ein
Schalter statt vier Nullwerte je Recht), aufgelöst wird **live** in der neuen
`UserPermissions.For`-Überladung — eine geänderte Rolle wirkt damit ab der nächsten Anmeldung,
ohne jedes Konto anzufassen. Beim **Löschen** einer Rolle stempelt `DeleteRoleAsync` ihre Rechte
auf die nicht-abweichenden Konten, sonst fielen die auf ihre alten Spalten zurück und bekämen
still mehr. Ein Verwalterrecht kennt die Rolle bewusst nicht. Verwaltung als Panel in der
Benutzerverwaltung (`RoleDialog`), Zuweisung im Benutzer-Dialog.

### F46 — Anmeldung über einen externen Anbieter

> **Als** Betreiber eines kleinen Studios **möchte ich** die Anmeldung an unser vorhandenes Konto
> (GitHub, Google, ein eigener OIDC-Server) hängen, **damit** niemand ein weiteres Passwort braucht.

Die Anmeldung ist bewusst kein ASP.NET Identity, sondern PBKDF2 plus Cookie — das steht dem hier
nicht im Weg: OpenID Connect kommt als zweites Authentifizierungsschema daneben, der `AppUser`
bekommt eine Spalte für den externen Bezeichner, und alles Weitere (Ansprüche, Berechtigungen,
`IChangeAuthorProvider`) bleibt unverändert. Wichtig ist die Regel, wer beim ersten externen
Anmelden angelegt wird — Vorgabe: niemand, ein Verwalter muss das Konto vorbereiten.

*Aufwand: M · Migration: ja · Format: unverändert*

### F47 — Zwei-Faktor-Anmeldung

> **Als** Betreiber, dessen Instanz aus dem Internet erreichbar ist, **möchte ich** einen zweiten
> Faktor verlangen können, **damit** ein geratenes Passwort nicht reicht.

TOTP nach RFC 6238 ist wenige Dutzend Zeilen und braucht keine Fremdbibliothek — dieselbe Abwägung
wie bei `ImageDimensionReader`, `Csv` und `CurveExpression`. Geheimnis und Wiederherstellungscodes
an `AppUser`, erzwungen im statisch gerenderten Anmeldeformular (das ist der Grund, warum es
`InputText` statt MudBlazor benutzt — hier gilt dasselbe). Ein- und ausschaltbar wie die
Passwortrichtlinie, also über die Konfiguration.

*Aufwand: M · Migration: ja · Format: unverändert*

### F48 — Betriebs-Kennzahlen ✅ umgesetzt

> **Als** Betreiber **möchte ich** einen Endpunkt für Verfügbarkeit und ein paar Kennzahlen
> (Datenbankverbindung, Größe des Asset-Verzeichnisses, Alter des jüngsten Exportstands), **damit**
> meine Überwachung merkt, wenn etwas klemmt.

`/health` mit den ASP.NET-Health-Checks (die Datenbankprüfung gibt es fertig je Provider) und
`/metrics` im Prometheus-Format. Beides ohne Anmeldung erreichbar zu machen wäre falsch — die
Zahlen verraten die Größe des Bestands; also über einen API-Schlüssel, der Filter der `MapGroup`
ist schon da.

*Aufwand: S · Migration: nein · Format: unverändert*

---

## Reihenfolge-Vorschlag

Wenn zuerst das gemacht werden soll, was am meisten fehlt und am wenigsten kostet:

1. **F8** — übersetzbare Dialogzeilen. Die einzige echte Lücke im Bestand: Das Lokalisierungsmodul
   erfasst ausgerechnet den textlastigsten Inhalt nicht.
2. **F1** — Quest-Ziele. Der letzte fachlich unvollständige Punkt aus dem Konzept.
3. **F29** — englische Modulseiten. Die auffälligste Unfertigkeit, und sie braucht keinen Code.
4. **F20 + F21 + F22** — Status, Kommentare, Aufgaben-Verknüpfung. Zusammen machen sie aus dem
   Verwaltungstool ein Werkzeug, mit dem zu zweit oder zu dritt gearbeitet wird.
5. **F15 + F14** — Loot-Simulator und Balancing-Tabelle. Der Bestand ist vollständig, die
   Auswertung ist es nicht.
6. **F3 + F4 + F2** — Feldgruppen, Wertebereiche, Referenzlisten. Kleine Eingriffe ins Feldsystem
   mit Wirkung in allen 22 Inhaltsmodulen.
7. **F24** — Papierkorb. Nicht dringend, bis es einmal dringend ist.
8. Alles Weitere nach Bedarf.
