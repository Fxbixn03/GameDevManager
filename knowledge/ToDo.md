# ToDo

Ideensammlung für die Zeit nach dem Konzept. Alles aus [Konzept.md](Konzept.md) ist umgesetzt,
ebenso die erste Welle F1–F48 — was hier steht, geht darüber hinaus. Die Nummern laufen deshalb
ab **F49** weiter; die abgearbeiteten Punkte stehen in der Git-Historie dieser Datei. Aus dieser
zweiten Welle sind inzwischen **F65** (Review-Workflow), **F84** (Live-Sync in den Engine-Editor)
und **F89** (Mail-Digest) umgesetzt und deshalb hier entfernt — die Nummern werden nicht neu
vergeben.

Jeder Punkt trägt eine Userstory und einen Umsetzungshinweis, der sagt, wo im Bestand er
andockt. Die Marker am Ende jedes Punktes:

- **Aufwand** — S (ein Tag), M (wenige Tage), L (eine Woche und mehr)
- **Migration** — braucht es eine Schemaänderung? Dann in **allen sechs Providern** (SqlServer, PostgreSql, MySql, MariaDb, Sqlite, Oracle).
- **Format** — muss `ExportFormat.FormatVersion` steigen? Sie steht derzeit auf **22**.

---

## A. Inhalt und Modellierung

### F49 — Feldtyp „Asset-Referenz“

> **Als** Nutzer **möchte ich** in einem Feld auf eine Datei aus der Asset-Bibliothek zeigen
> (die Angriffsanimation einer Waffe, das Weltmodell eines Items, der Trefferklang), **damit**
> ich nicht für jede Datei außer dem Icon einen Dateinamen von Hand abtippe.

Eine Entität hat heute Sprites über `AssetSpritePanel`, davon genau eines als `IsPrimary` — mehr
Zuordnung gibt es nicht. `ContentFieldType.AssetReference = 12` ist die fehlende Hälfte: Der Wert
ist die GUID eines `Asset` und steht in `FieldValue.ReferenceValue`, das Auswahlfeld holt sich die
Assets des Projekts über den `AssetService` statt über eine `IModuleEntitySource`. Zwei Dinge sind
zu bedenken: Der `ReferenceService` muss diese Verwendung finden, sonst meldet die Referenzansicht
einer Datei weiterhin nur ihren Besitzer; und der Export sollte neben der GUID den `storageKey`
danebenschreiben, damit die Engine die Datei ohne zweite Abfrage findet — dieselbe Überlegung wie
beim gerechneten Wert eines berechneten Feldes.

*Aufwand: M · Migration: nein (neuer Enum-Wert) · Format: +1*

### F50 — Standardwerte an der Felddefinition

> **Als** Nutzer **möchte ich** an einem Feld einen Vorgabewert hinterlegen („Stapelgröße 1“,
> „handelbar: ja“), **damit** eine neu angelegte Entität sinnvoll vorbelegt ist und ich nicht
> dreißigmal denselben Wert eintippe.

`FieldDefinition` trägt Pflicht-Schalter, Grenzen, Muster und Einheit — aber keinen Vorgabewert.
Eine Spalte `DefaultValue` (Text, wie überall in fester Kultur gelesen) und ein Griff in
`ContentEditContext`, wenn eine Entität **neu** ist. Wichtig: nur beim Anlegen, nie beim Laden
eines Bestandsdatensatzes — sonst füllte ein nachträglich gesetzter Vorgabewert stillschweigend
alle Leerstellen des Bestands. Und der Vorgabewert wird als **Wert** gespeichert und nicht als
Verweis auf die Definition: Eine Variante (`BasedOnId`) muss ihn überschreiben können, und
„geerbt“ bedeutet dort etwas anderes.

*Aufwand: S · Migration: ja · Format: +1*

### F51 — Pflichtfeld erst ab einem Bearbeitungsstand

> **Als** Autor **möchte ich**, dass Pflichtfelder erst greifen, wenn ich eine Entität auf „im
> Review“ oder „fertig“ setze, **damit** ich einen Entwurf mit drei Stichworten speichern kann,
> ohne vorher zwölf Zahlen zu erfinden.

Heute blockt `ContentFields.ValidateRequired` jedes Speichern, sobald ein Pflichtfeld leer ist —
das steht quer zum Bearbeitungsstand aus F20, der ausdrücklich sagt, worauf man sich noch nicht
verlassen kann. `FieldDefinition.RequiredFromStatus` (`ContentStatus?`, `null` = wie heute) und
dieselbe Prüfmethode vergleicht gegen `entity.Status`. Der Health Check bekommt die Gegenrichtung
gratis: „als fertig markiert, aber Pflichtfeld leer“ kann gar nicht mehr entstehen, dafür lohnt
ein Fund „Entwurf mit fehlenden Pflichtangaben“ als Auskunft ohne Verbot — dieselbe Linie wie beim
Loot-Check.

*Aufwand: S · Migration: ja · Format: +1*

### F52 — Bedingte Feldsichtbarkeit in der Maske

> **Als** Nutzer **möchte ich**, dass ein Feld nur erscheint, wenn ein anderes einen bestimmten
> Wert hat („Munitionsart“ nur bei Waffenart = Fernkampf), **damit** die Maske einer Art mit
> fünfundzwanzig Feldern nicht die Hälfte davon sinnlos anzeigt.

Die Feldgruppen aus F3 machen eine große Maske lesbar, aber nicht kürzer. Zwei Spalten an der
`FieldDefinition` (`VisibleWhenFieldId`, `VisibleWhenValue` als Text wie der Vergleichswert einer
`ContentFilter`-Bedingung) und die Auswertung im `ContentEditContext`, der die Werte der Entität
ohnehin in der Hand hält — dieselbe Stelle, die auch die berechneten Felder rechnet. Drei Dinge:
Ein **verstecktes Feld ist nicht pflichtig**, sonst meldete die Maske etwas, das nicht zu sehen
ist (die Regel gab es bei den Feldgruppen schon einmal, deshalb sind die Abschnitte aufgeklappt).
Der **Wert bleibt stehen**, wenn das Feld wieder verschwindet — Löschen wäre Datenverlust durch
einen Klick woanders; Export und Referenzansicht sehen ihn weiterhin. Und ein Ring
(A zeigt auf B zeigt auf A) wird beim Speichern des Feldes abgewiesen, wie bei den Unterarten.

*Aufwand: M · Migration: ja · Format: +1*

### F53 — Wiederverwendbare Bedingungssätze

> **Als** Designer **möchte ich** eine Bedingung einmal benennen („Kapitel 2 abgeschlossen“) und
> sie an vierzig Stellen verwenden, **damit** ich beim Umbau der Story nicht vierzig
> Bedingungssätze einzeln nachziehen muss.

Das Bedingungssystem hängt jeden Satz an genau einen Besitzer; dieselbe Prüfung steht damit so
oft da, wie sie gebraucht wird. Ein schlanker Weg: eine Entität `ConditionPreset` (Name,
Beschreibung, dazu ein `ConditionSet` im neuen Slot `Preset` an ihrer eigenen GUID — Teilobjekte
als Besitzer gibt es längst) plus eine `ConditionKind.PresetFulfilled`, deren Ziel auf sie zeigt.
Der `ConditionEvaluator` löst sie rekursiv auf, mit Tiefenbegrenzung und Ringprüfung wie bei den
Rezepten; der Freischaltungs-Graph liest die Kante durch die Vorlage hindurch, sonst zerfiele der
Baum an genau der Stelle, an der die Vorlage eingeführt wurde. Der Health Check „unerfüllbare
Bedingungen“ bekommt einen Fund dazu: Vorlage gelöscht, Verweise stehen geblieben.

*Aufwand: L · Migration: ja · Format: +1*

### F54 — Attribut-Katalog statt freier Feldnamen

> **Als** Balancer **möchte ich** einen projektweiten Katalog der Kennwerte pflegen (Leben, Mana,
> Schaden — je mit Einheit, Spanne und Beschreibung) und Felder daran binden, **damit** „Schaden“
> und „Schadenswert“ nicht zwei Kennwerte sind, die nichts voneinander wissen.

Heute ist ein Feld über seinen **Namen** identifiziert — die Balancing-Tabelle gruppiert danach,
die berechneten Felder sprechen einander so an, das CSV bildet je Feldname eine Spalte. Das trägt
weit, aber ein Tippfehler erzeugt lautlos einen zweiten Kennwert. Ein `Stat`-Katalog (eigene
Tabelle, kein Modul mit `ContentEntity` — Kennwerte tragen keine Arten und keine Felder) und
`FieldDefinition.StatId` als optionale Bindung: Wer bindet, bekommt Einheit, Grenzen und
Beschreibung von dort und taucht in der Balancing-Tabelle modulübergreifend unter **einem**
Kennwert auf. Ungebundene Felder verhalten sich wie heute — sonst wäre es eine Umstellung des
Bestands statt einer Erweiterung.

*Aufwand: L · Migration: ja · Format: +1*

---

## B. Erzählung und Lokalisierung

### F55 — Dialog als Autorenskript heraus und wieder herein

> **Als** Autor **möchte ich** ein Gespräch als Textdatei herunterladen, in meinem Editor
> überarbeiten und wieder einlesen, **damit** ich lange Dialoge schreiben kann, ohne für jede
> Zeile ein Formular auszufüllen.

Für Übersetzungen gibt es diesen Weg schon (`LocalizationService.ExportCsvAsync`), für den
Ausgangstext nicht. Ein kleines Zeilenformat genügt und ist selbst zu schreiben — dieselbe
Abwägung wie beim `Csv` und beim `SimpleMarkdown`: `Alrik: Was willst du?` für eine Zeile,
`> Nichts. -> #ende` für eine Antwort samt Sprungmarke, `#marke` als Zeilenanker. Zwei Dinge:
Die **GUIDs müssen mitreisen** (als Kommentar hinter der Zeile), sonst legt jeder Reimport alles
neu an und nimmt Bedingungen, Übersetzungen und Vertonungen mit ins Grab — sie hängen an den
Zeilen-GUIDs. Und der Import prüft dieselben Regeln wie die Maske: Sprecher muss Beteiligter
sein, Sprungziel muss im selben Dialog liegen.

*Aufwand: M · Migration: nein · Format: unverändert*

### F56 — Dialog-Graph bearbeiten statt nur ansehen

> **Als** Designer **möchte ich** im Graphen Knoten verschieben und Antworten durch Ziehen mit
> einer Zeile verbinden, **damit** ich die Verzweigung eines Gesprächs sehe, während ich sie baue.

`GetGraphAsync` zeichnet heute ein Bild ohne JavaScript, die Spalten kommen aus der Breitensuche
(`Depth`). Zum Bearbeiten braucht es zwei Spalten an der `DialogueLine` (`GraphX`, `GraphY`;
`null` heißt „automatisch platzieren“, damit Bestandsdialoge aussehen wie heute) und ein
Ziehen-Skript nach dem Muster von `gdm-map.js`: Der Browser meldet die Position, gerechnet und
geprüft wird in C#. Eine neue Kante ist nichts anderes als `DialogueChoice.NextLineId` — der
Dienst prüft schon, dass das Ziel im selben Dialog liegt. Der Health Check „Sackgassen“ wird
damit zur Live-Anzeige: unerreichbare Knoten stehen im Bild bereits in einer eigenen Spalte.

*Aufwand: L · Migration: ja · Format: +1*

### F57 — Barks: Gewichtung und Wiederholsperre

> **Als** Designer **möchte ich** einzelnen Sprechblasen-Zeilen ein Gewicht und eine Sperrzeit
> geben, **damit** der Wachposten nicht dreimal hintereinander denselben Satz sagt und die
> seltene Zeile wirklich selten ist.

`DialogueKind.Bark` sagt heute nur, dass die Zeilen unabhängig nebeneinander stehen; wie oft
welche kommt, entscheidet die Engine ohne Anhalt. Zwei Spalten an der `DialogueLine`
(`Weight`, Vorgabe 1; `CooldownSeconds`, `null` = keine Sperre) — sie gelten nur für Barks, in
einem Gespräch wären sie sinnlos und werden dort nicht angezeigt. Der Wert wandert in den Export,
mehr tut das Tool nicht: Gewürfelt wird im Spiel. Die Vorschau kann trotzdem die Anteile
ausrechnen und danebenschreiben („diese Zeile: 12 %“) — dieselbe Auskunft wie beim Loot-Simulator,
nur ohne Simulation.

*Aufwand: S · Migration: ja · Format: +1*

### F58 — Übersetzungs-Glossar und Konsistenzprüfung

> **Als** Übersetzer **möchte ich** feste Begriffe hinterlegen („Eisenschwert“ = „Iron Sword“)
> und gemeldet bekommen, wo ich davon abgewichen bin, **damit** derselbe Gegenstand im ganzen
> Spiel gleich heißt.

Das Lokalisierungsmodul zählt heute „fehlt“ und „veraltet“ — nicht „falsch“. Ein `GlossaryTerm`
je Sprache (Ausgangsbegriff, Zielbegriff, optional der Verweis auf die Entität, von der er
stammt) und eine Prüfung, die über die Übersetzungen läuft: Kommt der Ausgangsbegriff im
`SourceText` vor, muss der Zielbegriff in der Übersetzung stehen. Dazu die zweite, billigere
Prüfung derselben Seite: Eine Übersetzung, die deutlich länger ist als ihr Ausgangstext, sprengt
später die Oberfläche — als Warnung mit einstellbarer Schwelle, nicht als Verbot. Beides sind
Funde und keine Sperren, wie überall bei den Health Checks.

*Aufwand: M · Migration: ja · Format: +1*

### F59 — XLIFF für Übersetzungsbüros

> **Als** Projektleiter **möchte ich** die offenen Texte als XLIFF herausgeben und die Rückgabe
> wieder einlesen, **damit** ein Übersetzungsbüro mit seinen üblichen Werkzeugen arbeiten kann.

Das CSV aus F9 trägt den Weg für eine Person mit einer Tabellenkalkulation; Agenturen arbeiten
mit CAT-Werkzeugen, und deren gemeinsame Sprache ist XLIFF. Das Format ist XML und mit
`System.Xml` von Hand zu schreiben — dieselbe Abwägung wie beim `Csv` und beim `Totp`, eine
Fremdbibliothek lohnt für zwei Elementtypen nicht. Adressiert wird wie im CSV über `id` und
`slot` (zusammen die `<trans-unit id>`), der `SourceText` steht als `<source>`, die Übersetzung
als `<target>` mit `state`. Beim Zurücklesen gilt dieselbe Regel wie beim CSV: Der Ausgangstext
kommt aus dem Bestand und nicht aus der Datei, sonst erklärt eine alte Lieferung eine veraltete
Übersetzung für aktuell.

*Aufwand: M · Migration: nein · Format: unverändert*

---

## C. Balance und Auswertung

### F60 — Verteilung eines Zahlenfeldes

> **Als** Balancer **möchte ich** sehen, wie sich die Werte eines Feldes über den Bestand
> verteilen (alle Item-Preise als Säulen), **damit** ich erkenne, ob meine Wirtschaft eine Mitte
> hat oder aus drei Ausreißern und zweihundert Einserwerten besteht.

Die Balancing-Tabelle aus F14 zeigt Mittelwert, Minimum und Maximum je Spalte — eine Zahl sagt
aber nichts über die Form. Die Trefferliste liegt bereits geladen vor (`SavedViewService`), es
fehlt nur das Bild: dieselbe SVG-Technik wie beim `CurveChart`, Klassenbreite nach Freedman-Diaconis
oder schlicht zehn Klassen mit einstellbarer Zahl. Zwei Dinge, die ein Histogramm hier nützlich
machen: Ein Klick auf eine Säule **filtert die Tabelle darunter** auf genau diese Klasse — sonst
weiß man, dass es einen Ausreißer gibt, aber nicht, welcher es ist; und leere Werte bekommen eine
eigene Säule daneben statt in der Null zu verschwinden.

*Aufwand: S · Migration: nein · Format: unverändert*

### F61 — Zwei Entitäten nebeneinander vergleichen

> **Als** Designer **möchte ich** zwei Items nebeneinander legen und die Unterschiede hervorgehoben
> sehen, **damit** ich beurteilen kann, ob das seltene Schwert das gewöhnliche wirklich schlägt.

Der Diff zweier Exportstände kann das für ganze Projekte, für zwei Datensätze gibt es nichts.
Eine Seite im Werkzeug-Bereich, zwei Auswahlfelder über die `IModuleEntitySource` (dasselbe Modul,
sonst haben die Zeilen nichts gemeinsam), Werte über `ContentFields.LoadValuesAsync` — mit
aufgelöster Variantenvererbung, sonst stünde bei der Variante die halbe Spalte leer. Unterschiede
farbig, Gleiches blass; bei Zahlen die Differenz und der Faktor daneben. Der Einstieg gehört in
die Modul-Liste („zum Vergleich merken“, dann „vergleichen mit …“ im Rechtsklick-Menü) — von der
Vergleichsseite aus zweimal zu suchen ist der umständlichere Weg.

*Aufwand: S · Migration: nein · Format: unverändert*

### F62 — Ausreißer-Prüfung über Zahlenfelder

> **Als** Balancer **möchte ich** gemeldet bekommen, wenn ein Wert weit außerhalb dessen liegt,
> was seine Geschwister tragen (ein Dolch mit dem zehnfachen Schaden aller anderen Dolche),
> **damit** ich eine vergessene Null finde, bevor sie im Spiel steht.

Die Zutaten liegen bereit: Die Balancing-Tabelle rechnet Mittelwert und Abweichung je Spalte
bereits aus, die Health Checks sind der Ort, an dem so etwas auffällt. Als eingebauter Check je
Modul und Art: Wert weiter als *n* Standardabweichungen (oder außerhalb des 1,5-fachen
Interquartilsabstands — robuster bei kleinen Beständen) vom Median seiner Art. Zwei Regeln, damit
es nicht rauscht: Unter einer Mindestzahl von Datensätzen je Art wird gar nicht geprüft — bei drei
Schwertern ist jedes ein Ausreißer; und ein Fund ist **abhakbar**, sonst meldet der Check die
absichtlich mächtige Legendärwaffe bis in alle Ewigkeit. Das Abhaken passt zur `ContentRule`-Idee
aus F18 und braucht dort eine kleine Ausnahmeliste.

*Aufwand: M · Migration: ja (Ausnahmen) · Format: unverändert*

### F63 — Erreichbarkeits-Prüfung über alle Module

> **Als** Designer **möchte ich** wissen, welche Inhalte der Spieler nie zu sehen bekommt — ein
> Item in keiner Loot-Tabelle, bei keinem Händler, in keiner Quest-Belohnung und in keinem Rezept,
> **damit** ich entweder eine Quelle ergänze oder den Inhalt streiche.

Den kleinen Fall deckt der eingebaute Check „tote Items“ ab. Die allgemeine Umkehrung wurde bei
F18 bewusst **nicht** als nutzerdefinierte Regel gebaut, und die Begründung gilt weiter: Verweise
laufen teils über Feldwerte, teils über modul-eigene Spalten, und ein Modul, das
`FindReferencesAsync` nicht überschreibt, meldete stille Fehlfunde. Als **eingebauter** Check ist
genau das lösbar, weil die Kenntnis je Modul dort stehen darf, wo sie hingehört: eine Liste der
Module, deren Rückrichtung vollständig ist, und ein sichtbarer Hinweis, welche Module die Prüfung
auslässt. Rechnen lässt sich das in einem Durchgang — alle GUIDs einsammeln, alle Verweise
einsammeln, Differenz —, nicht mit einer Abfrage je Entität.

*Aufwand: M · Migration: nein · Format: unverändert*

### F64 — Verlauf der Health-Check-Funde

> **Als** Projektleiter **möchte ich** sehen, ob die Zahl der Funde über die Wochen sinkt,
> **damit** ich weiß, ob wir aufräumen oder nur draufpacken.

Die Prüfungen laufen heute bei jedem Aufruf des Dashboards neu und hinterlassen nichts. Eine
schmale Tabelle (Projekt, Zeitpunkt, Prüfung, Anzahl) und ein Eintrag je Lauf des ohnehin
vorhandenen `ChangeLogMaintenance`-Hintergrunddienstes — einmal täglich genügt, die Kurve
beantwortet eine Frage über Wochen. Zwei Dinge: Geschrieben wird **nicht** beim Öffnen des
Dashboards, sonst hinge die Kurve daran, wer wie oft hinsieht; und die Aufbewahrung folgt der
Linie des Änderungsprotokolls (Konfiguration, kein Pflichteintrag) — es ist eine Auskunft, kein
Weg zurück. Angezeigt als Sparkline im Zustandsband, ausführlich auf der Statistik-Seite.

*Aufwand: M · Migration: ja · Format: unverändert*

---

## D. Zusammenarbeit und Prozess

### F66 — Benutzer erwähnen und benachrichtigen

> **Als** Teammitglied **möchte ich** in einer Anmerkung oder auf einer Kanban-Karte eine Person
> erwähnen und ihr damit eine Meldung im Feed erzeugen, **damit** eine Frage nicht darauf wartet,
> dass die andere Person zufällig dieselbe Maske öffnet.

`ContentMentions` löst `@Name` heute auf **Entitäten** auf und speichert `[[items:GUID|Name]]`.
Personen dazuzunehmen heißt vor allem, den Zusammenstoß zu klären: Ein NPC und ein Konto können
gleich heißen, und der längste passende Name gewinnt. Deshalb ein eigenes Zeichen (`@@Fabian`
oder die ausdrückliche Auswahl aus der Vorschlagsliste) und die Ablage als `[[user:GUID|Name]]`.
Aufgelöst wird wie gehabt im Dienst (`MentionResolver`), nicht in der Maske. Der
`ActivityFeedService` bekommt die Erwähnung als vierte Quelle — und sie ist die erste, die auch
das **eigene** Tun betrifft nicht: Erwähnt zu werden ist immer eine Nachricht an jemand anderen.

*Aufwand: M · Migration: nein · Format: unverändert*

### F67 — Antworten auf Anmerkungen, Anmerkung am einzelnen Feld

> **Als** Nutzer **möchte ich** auf eine Anmerkung antworten können und eine Anmerkung an ein
> bestimmtes Feld hängen („der Preis hier ist zu hoch“), **damit** aus drei losen Zetteln ein
> Gespräch wird und klar ist, worüber es geht.

`ContentComment` ist heute flach und hängt an der Entität als Ganzes. Zwei Spalten:
`ParentId` (Antwort auf eine Anmerkung; nur eine Ebene — verschachtelte Bäume liest niemand) und
`AnchorSlot` als Text, der wie beim Übersetzungs-`Slot` entweder `"name"`, `"description"` oder
die GUID einer `FieldDefinition` trägt. Damit kann die Maske am Feld ein kleines Zeichen zeigen
und beim Klick den passenden Faden aufklappen. Erledigen gilt für den ganzen Faden, nicht je
Antwort — sonst steht ein erledigter Anfang unter offenen Rückfragen.

*Aufwand: S · Migration: ja · Format: unverändert*

### F68 — Aufgabe aus einem Health-Check-Fund

> **Als** Projektleiter **möchte ich** aus einem Fund mit einem Klick eine Kanban-Karte machen,
> **damit** die Arbeit dort landet, wo wir sie verteilen, statt auf einer Liste, die niemandem
> gehört.

Beide Seiten sind fertig: Der Fund kennt Modul und GUID, die Karte kennt `TargetModuleKey` und
`TargetEntityId`. Es fehlt der Knopf an jedem Fund (Statistik-Seite und Zustandsband) und die
Wahl des Boards samt Spalte. Zwei Dinge halten es sauber: Die Karte bekommt eine **Kennung des
Funds** im Titel oder in der Notiz, damit ein zweiter Klick auf denselben Fund nicht die zehnte
Karte erzeugt — geprüft über die Verknüpfung plus Prüfungsart; und wenn der Fund verschwindet,
wird die Karte **nicht** automatisch geschlossen: Ob etwas erledigt ist, entscheidet die Spalte,
nicht der Prüflauf.

*Aufwand: S · Migration: nein · Format: unverändert*

### F69 — Arten samt Feldern in ein anderes Projekt übernehmen

> **Als** Nutzer mit zwei Projekten **möchte ich** meine mühsam gebaute Item-Art samt Unterarten
> und Feldern in das andere Projekt kopieren, **damit** ich das Schema nicht zweimal von Hand
> aufbaue.

Duplizieren gibt es für ganze Projekte und für einzelne Entitäten — beide bleiben im Projekt.
Für Arten ist der Weg derselbe und kürzer: `ContentType` samt `FieldDefinition`s und
`FieldOption`s serialisieren, GUIDs über `GuidRemap` tauschen, mit neuer `GameProjectId`
zurücklesen. Zwei Dinge sind zu klären: Ein **Feld mit Zielmodul** (`ReferenceModuleKey`) zeigt
weiterhin auf ein Modul, nicht auf eine Entität — das übersteht den Umzug; ein **Vorgabewert oder
eine Bindung**, die auf eine GUID im Quellprojekt zeigt, dagegen nicht und wird geleert statt
mitgenommen. Und die Eltern-Art muss mitkommen oder der `ParentId` fällt weg, sonst entsteht eine
Unterart ohne Vererbungslinie.

*Aufwand: M · Migration: nein · Format: unverändert*

---

## E. Bedienung

### F70 — Darstellung und Sprache je Benutzer

> **Als** Nutzer einer Installation mit mehreren Konten **möchte ich** meine eigene Sprache und
> meine eigene Hell/Dunkel-Wahl haben, **damit** die Einstellung meines Kollegen nicht meine ist.

`AppearanceSelection` und `LanguageSelection` halten beides installationsweit in
`appsettings.Local.json` — eine Entscheidung aus der Zeit, als das Tool eine Person betrieb; seit
den Konten stimmt sie nicht mehr. Zwei Spalten am `AppUser` und ein scopeder Auflöser, der den
Wert des angemeldeten Kontos nimmt und sonst den der Installation (Anmeldeseite, Ersteinrichtung).
Der Haken steckt in der Sprache: Sie wird heute als **Kultur des Prozesses** gesetzt, weil Blazor
Server serverseitig rendert. Je Benutzer muss sie stattdessen je Verbindung gesetzt werden —
`CultureInfo.CurrentUICulture` im Kreis und in der Anfrage, nicht global; die Satelliten-Dateien
und `TranslationCompletenessTests` bleiben unberührt.

*Aufwand: M · Migration: ja · Format: unverändert*

### F71 — Aktives Projekt je Benutzer

> **Als** Nutzer **möchte ich** in Projekt A arbeiten, während mein Kollege in Projekt B ist,
> **damit** wir uns nicht gegenseitig aus dem Bestand werfen.

Dieselbe Wurzel wie F70: Die `ProjectSelection` ist ein Singleton mit `Project:CurrentId` in der
lokalen Konfigurationsdatei, und ein Wechsel gilt für alle Verbindungen — mit dem `forceLoad`
merkt der andere es sofort, aber ungefragt. Eine Spalte `CurrentProjectId` am `AppUser`, der
scopede `ProjectContext` bleibt wie er ist; der Wert der Installation ist die Vorgabe für Konten
ohne eigene Wahl und für alles ohne Anmeldung (Hintergrunddienste laufen ohnehin über alle
Projekte). Zu bedenken: Ein gelöschtes Projekt muss die Wahl der anderen Konten mitnehmen, sonst
landet jemand nach dem Anmelden im Nichts.

*Aufwand: S · Migration: ja · Format: unverändert*

### F72 — Fassungen je Entität und Zurücknehmen

> **Als** Nutzer **möchte ich** eine frühere Fassung einer Entität ansehen und wiederherstellen,
> **damit** ein versehentliches Überschreiben nicht bedeutet, dass ich den ganzen Datensatz neu
> tippe.

Das Änderungsprotokoll notiert bewusst nur die **Namen** der geänderten Eigenschaften — der Wert
eines Feldes kann ein ganzer Beschreibungstext sein, und das Protokoll soll die Datenbank nicht
verdoppeln. Der Papierkorb kann dagegen genau das, was hier fehlt: `EntityDuplication.CaptureAsync`
serialisiert eine Entität samt Kind-Sammlungen, Feldwerten und Bedingungen, `Restore` liest sie
mit den originalen GUIDs zurück. Eine Fassungstabelle nach demselben Muster, geschrieben im
`ChangeLogInterceptor` vor dem Speichern, mit Aufbewahrung als Konfiguration (Anzahl je Entität,
Höchstalter) und **abschaltbar** — sie kostet Platz proportional zur Arbeit, und ob das gewollt
ist, entscheidet der Betreiber. Wiederherstellen ist dann derselbe Weg wie im Papierkorb, nur
über eine vorhandene GUID: löschen und neu anlegen, in einer Transaktion.

*Aufwand: L · Migration: ja · Format: unverändert*

### F73 — Gespeicherte Ansicht in der Modul-Liste anwenden

> **Als** Nutzer **möchte ich** meine gespeicherte Ansicht direkt in der Item-Liste aufrufen,
> **damit** ich für den täglichen Blick auf „alle Waffen im Review“ nicht auf eine andere Seite
> wechseln muss.

Die Filterleiste wurde bewusst **nicht** in zwanzig Listen nachgebaut — die Begründung bleibt
richtig, die Listen sind je Modul eigen (Kachelraster, Tabelle, Zeitstreifen). Anwenden ist aber
etwas anderes als bauen: ein Auswahlfeld im `ModuleHeader` mit den Ansichten dieses Moduls, und
die Liste zeigt statt ihrer Standardabfrage die Treffer aus `SavedViewService`. Das ist eine
Änderung an einer gemeinsamen Komponente plus einer Zeile je Liste, nicht zwanzigmal dieselbe
Leiste. Der Weg zum Bauen bleibt die Ansichten-Seite; die zuletzt gewählte Ansicht je Modul kann
sich der Benutzer merken lassen, wie die Sortierung.

*Aufwand: M · Migration: nein · Format: unverändert*

### F74 — Schnellerfassung in der Liste

> **Als** Nutzer **möchte ich** in der Modul-Liste eine Zeile mit Name und Art anlegen und sofort
> die nächste tippen, **damit** ich fünfzig Platzhalter erfassen kann, ohne fünfzigmal eine Maske
> zu öffnen und zu schließen.

Der Weg „Neu → Maske → Speichern → Zurück“ ist für einen vollständigen Datensatz richtig und für
einen Platzhalter zu lang. Eine Erfassungszeile oben in der Liste (Name, Art, Enter) ruft
denselben Modul-Dienst wie die Maske — Pflichtfelder greifen dabei erst ab F51 sinnvoll, deshalb
gehört die Reihenfolge so herum. Der Fokus bleibt nach dem Speichern im Namensfeld, die eben
angelegte Zeile erscheint markiert darüber. Kein neuer Dienst, kein neues Format; die Absicherung
gegen Dubletten ist dieselbe wie in der Maske.

*Aufwand: S · Migration: nein · Format: unverändert*

### F75 — Fokusmodus für lange Texte

> **Als** Autor **möchte ich** einen Story-Abschnitt oder eine Beschreibung bildschirmfüllend
> schreiben, **damit** mich Modulleiste, Referenzpanel und Anmerkungen beim Schreiben nicht
> anschauen.

Ein Knopf am mehrzeiligen Feld, der es in einen Vollbild-Dialog hebt — mit `MarkdownView` als
Vorschau daneben, weil der Story-Text ohnehin Markdown und Erwähnungen kennt. Der Wert bleibt am
selben Modell, es ist dieselbe Eingabe an einem anderen Ort; gespeichert wird wie sonst über den
Speichern-Knopf der Maske, damit es keinen zweiten Speicherweg gibt. `Esc` schließt den Dialog und
nicht die Maske — die Schicht-Erkennung (`hasOpenLayer`) in `gdm-shortcuts.js` erledigt das
bereits.

*Aufwand: S · Migration: nein · Format: unverändert*

---

## F. Produktion und Assets

### F76 — Asset-Rollen je Entität

> **Als** Nutzer **möchte ich** einer Entität mehrere Bilder mit fester Bedeutung geben (Icon,
> Porträt, Weltmodell, Karten-Symbol), **damit** die Engine weiß, welches sie wofür nimmt.

Heute gibt es je Entität genau eine ausgezeichnete Datei (`IsPrimary`), alles Weitere ist eine
namenlose Sammlung. Eine Spalte `RoleKey` am `Asset` — Freitext mit Vorschlägen, wie der
`GroupName` der Felder, statt einer Rollentabelle: Eine Rolle ist eine Beschriftung und trägt
nichts weiter, und als Text geht sie ohne Zutun durch Export, Import und Duplizieren. `IsPrimary`
bleibt daneben bestehen (die Listen zeigen weiterhin ein Icon); eine Rolle ist je Entität
eindeutig, damit „Porträt“ nicht zweimal vergeben wird. Der Nutzen liegt am Ende im Export: Die
Engine sucht nach der Rolle statt nach der Reihenfolge.

*Aufwand: M · Migration: ja · Format: +1*

### F77 — Dateien aus dem Speicher übernehmen

> **Als** Grafiker **möchte ich** hundert PNGs in den Asset-Ordner legen und im Tool „übernehmen“
> drücken, **damit** ich sie nicht einzeln durch den Upload-Dialog schicke.

Die Gegenrichtung gibt es schon: `FindOrphanedFilesAsync` findet Dateien ohne Zeile und bietet an,
sie zu löschen. Dieselbe Liste kann auch „aufnehmen“ — Zeile anlegen, Maße über den
`ImageDimensionReader` lesen, danach die vorhandene Namenszuordnung (`SuggestOwnersAsync`) laufen
lassen. Vier Dinge übernimmt man dabei aus dem Upload-Weg unverändert: Größenprüfung
(`Assets:MaxFileSizeBytes`), erlaubte Typen, das Schreibrecht über den `PermissionGuard` und die
Regel, dass **nichts stillschweigend** zugeordnet wird. Der Schlüssel der Datei bleibt, wie er
ist — er ist der Pfad, unter dem sie schon liegt.

*Aufwand: S · Migration: nein · Format: unverändert*

### F78 — Dateinamen-Konventionen als Regel

> **Als** Team **möchten wir** festlegen, dass NPC-Sprites `npc_*.png` heißen, und gemeldet
> bekommen, was davon abweicht, **damit** der Import in die Engine nicht an einer Datei namens
> `Neu (2).png` hängt.

Die eigenen Health-Check-Regeln aus F18 sind der richtige Ort: ein weiterer `ContentRuleCheck`
(„Dateiname passt nicht zum Muster“) mit dem Muster als Angabe der Regel — die Spalte dafür gibt
es, und ungültige Muster weist `ContentTypeService.Validate` bereits beim Speichern ab, dieselbe
Prüfung lässt sich hier wiederverwenden. Geprüft wird der ursprüngliche Dateiname des Assets, nicht
der `StorageKey` (der ist eine GUID und sagt nichts). Ein kaputtes Muster gilt wie beim Feld als
erfüllt — es ist ein Fehler an der Regel, nicht am Bestand.

*Aufwand: S · Migration: nein (neuer Enum-Wert) · Format: unverändert*

### F79 — Dauer und Format aus Audiodateien lesen

> **Als** Nutzer **möchte ich** an einer Audiodatei sofort Länge, Abtastrate und Kanäle sehen,
> **damit** ich einen versehentlich in Mono aufgenommenen Sprechertake finde, bevor er im Spiel
> landet.

Für Bilder gibt es das seit dem `ImageDimensionReader`: Kopfdaten lesen, ohne Bibliothek, für
unbekannte Formate bleiben die Angaben leer. Für WAV (RIFF-Kopf), OGG/Vorbis (Identification
Header) und MP3 (erster Frame-Header) ist derselbe Umfang zu haben — je ein paar Dutzend Zeilen,
dieselbe Abwägung wie beim `Csv`, `Totp` und `CurveExpression`. Angezeigt in der Audio-Liste, im
Vertonungsraster und in der Bibliothek; nützlich wird es mit der Prüfung daneben: „Aufnahme
länger als *n* Sekunden“ oder „Aufnahmen desselben Dialogs mit unterschiedlicher Abtastrate“.

*Aufwand: M · Migration: ja (drei Spalten am Asset) · Format: +1*

---

## G. Import, Export und Integration

### F80 — Export in ein Arbeitsverzeichnis statt in ein ZIP

> **Als** Entwickler mit einem Git-Repository **möchte ich**, dass der Export direkt in einen
> konfigurierten Ordner schreibt, **damit** ich nach jedem Stand nur noch `git diff` tippe statt
> ein Archiv zu entpacken.

Die Ablage je Entität aus F41 macht den Diff lesbar, verpackt ihn aber weiterhin in ein ZIP, das
jemand von Hand über den Arbeitsbaum legen muss. `ExportService` schreibt seine Einträge ohnehin
über eine Abstraktion — statt in den `ZipArchive` gehen sie in ein Verzeichnis. Zwei Dinge
entscheiden über Brauchbarkeit: **Verwaiste Dateien müssen weg**, sonst bleibt ein gelöschtes
Item als Datei liegen und der Diff zeigt es nie — gelöscht wird ausschließlich unterhalb der vom
Export selbst erzeugten Unterordner und nichts sonst; und der Lauf muss **gleiche Dateien nicht
anfassen** (Inhalt vergleichen, dann schreiben), sonst trägt jeder Export einen neuen
Zeitstempel in jede Datei und Git sieht tausend Änderungen.

*Aufwand: M · Migration: nein · Format: unverändert*

### F81 — CSV-Import mit Spaltenzuordnung

> **Als** Nutzer **möchte ich** eine Tabelle einlesen, die nicht aus diesem Tool stammt, und
> selbst sagen, welche Spalte welches Feld ist, **damit** ich meinen alten Item-Bestand aus der
> Tabellenkalkulation nicht abtippe.

`CsvContentService` erwartet heute unsere Spaltennamen (`id`, `name`, `beschreibung`, `art`, dann
je Feldname). Ein Zuordnungsschritt davor ändert nichts am Import selbst: Datei hochladen, Kopfzeile
lesen (`Csv.DetectSeparator` gibt es), je Spalte ein Auswahlfeld auf Stammdaten oder Feld, dann
in dieselbe Import-Strecke. Die Zuordnung als Profil zu speichern lohnt ab dem zweiten Lauf —
dieselbe Ablage wie bei den Export-Profilen, eine Textspalte genügt. Die bestehenden Regeln
bleiben: Der Import aktualisiert und löscht nichts, eine kaputte Zelle ist eine Warnung und
verwirft nicht die Zeile.

*Aufwand: M · Migration: ja (Profile) · Format: unverändert*

### F82 — XLSX neben CSV

> **Als** Nutzer **möchte ich** die Tabelle als Excel-Datei bekommen, **damit** Umlaute,
> Trennzeichen und Zahlenformate ankommen, ohne dass ich beim Öffnen einen Assistenten bediene.

Das CSV trägt ein BOM und Semikolons genau wegen dieser Probleme — gelöst sind sie damit nicht,
nur abgemildert. Eine `.xlsx` ist ein ZIP aus wenigen XML-Dateien, und ZIPs schreibt das Tool
längst (`ExportService`); für eine Tabelle ohne Formatierung sind es drei Dateien plus die
gemeinsame Zeichenkettenliste. Damit wären auch **mehrere Blätter** möglich — je Modul eines
statt je Modul einer Datei. Gelesen werden muss es nicht zwingend: Der Rückweg kann CSV bleiben,
solange der Zuordnungsschritt aus F81 dasteht; wer XLSX zurückgeben will, exportiert es aus der
Tabellenkalkulation als CSV.

*Aufwand: M · Migration: nein · Format: unverändert*

### F83 — Vorschau eines Engine-Presets

> **Als** Nutzer **möchte ich** an einem Beispieldatensatz sehen, was ein Preset erzeugt, bevor
> ich exportiere, **damit** ich eine falsche Zuordnung nicht erst in der Engine bemerke.

`EngineExportWriter` erzeugt heute erst beim Export, und was herauskommt, sieht man im Archiv.
Dieselbe Klasse kann eine einzelne Entität schreiben und den Text zurückgeben, statt ihn in einen
Eintrag zu legen — die Preset-Maske zeigt ihn daneben (Unity: die generierte Klasse plus die
JSON, Unreal: die CSV-Zeile samt Kopfzeile, Godot: die `.tres`). Nützlich wird es mit zwei
Hinweisen darin: Zuordnungen, deren Feld an der gewählten Art gar nicht existiert, und Zielnamen,
die nach `Identifier` gleich werden — Letzteres lehnt der Dienst schon ab, aber erst beim
Speichern.

*Aufwand: S · Migration: nein · Format: unverändert*

---

## H. Betrieb und Sicherheit

### F85 — Sperre nach Fehlanmeldungen

> **Als** Betreiber **möchte ich**, dass ein Konto nach mehreren Fehlversuchen für kurze Zeit
> gesperrt wird, **damit** ein Wörterbuchangriff auf das offen im Netz stehende Tool nicht
> beliebig oft raten kann.

Die Anmeldung prüft heute nur den Hash und die Sperre des Kontos; wer falsch rät, darf sofort
wieder. Zwei Spalten am `AppUser` (`FailedLoginCount`, `LockedUntilUtc`) und drei Zeilen in
`UserService.AuthenticateAsync` — hochzählen bei falschem Passwort, zurücksetzen bei Erfolg,
sperren ab einer Schwelle für eine wachsende Frist. Die Schwelle ist Konfiguration wie die
Passwortrichtlinie und aus demselben Grund. Zwei Feinheiten: Der zweite Faktor gehört in dieselbe
Zählung (sonst ist er das offene Tor), und die Meldung an der Oberfläche muss **gleich** bleiben,
egal ob Konto oder Passwort falsch war — sonst verrät sie, welche Anmeldenamen es gibt.

*Aufwand: S · Migration: ja · Format: unverändert*

### F86 — Sitzungen beenden

> **Als** Nutzer **möchte ich** „auf allen Geräten abmelden“ drücken können, **damit** ein
> vergessener Browser im Büro nicht wochenlang angemeldet bleibt.

Das Anmelde-Cookie ist heute bis zu seinem Ablauf gültig, und eine Rechteänderung greift
ausdrücklich erst bei der nächsten Anmeldung. Ein `SecurityStamp` am `AppUser`, der als Anspruch
ins Cookie wandert und bei jeder Anfrage gegen die Datenbank geprüft wird
(`CookieAuthenticationEvents.OnValidatePrincipal`, mit kurzer Zwischenspeicherung), löst beides
auf einmal: Beim Ändern des Stempels fliegen alle Sitzungen heraus — nach dem Abmelden überall,
nach einem Passwortwechsel, nach dem Entzug von Rechten. Eine Liste der einzelnen Sitzungen
bräuchte eine Tabelle und ist der teurere, seltener gebrauchte Teil; der Stempel ist die
Neunzig-Prozent-Lösung.

*Aufwand: M · Migration: ja · Format: unverändert*

### F87 — Geheimnisse verschlüsselt ablegen

> **Als** Betreiber **möchte ich**, dass Webhook-Geheimnisse und TOTP-Schlüssel nicht im Klartext
> in der Datenbank stehen, **damit** eine weitergegebene Datenbanksicherung nicht gleich die
> zweite Stufe der Anmeldung mitliefert.

Beide stehen bewusst im Klartext, weil das Tool mit ihnen *rechnen* muss — anders als bei einem
Passwort hilft ein Hash nicht. Verschlüsselung schon: ASP.NET DataProtection ist im Projekt
ohnehin vorhanden, `IDataProtector` verschlüsselt beim Schreiben und entschlüsselt beim Lesen,
gekapselt an genau zwei Stellen (`WebhookService`, `UserService`). Ehrlich dazugesagt: Der
Schlüsselring liegt dann neben der Datenbank, und wer beides hat, hat auch beides — der Gewinn
ist die **weitergegebene Sicherung**, nicht der übernommene Server. Bestandswerte werden beim
ersten Lesen erkannt (Präfix) und beim nächsten Speichern umgestellt, damit niemand migrieren
muss; und der Umzug einer Installation braucht dann den Schlüsselring im Sicherungsarchiv aus F44.

*Aufwand: M · Migration: nein · Format: unverändert*

### F88 — Hintergrunddienste sichtbar machen

> **Als** Betreiber **möchte ich** sehen, wann die Wartung zuletzt lief, ob der Zeitplan-Export
> etwas angelegt hat und woran der Webhook-Versand scheitert, **damit** ich nicht im Log suchen
> muss, ob überhaupt etwas passiert.

Die Hälfte steht bereits: Der `BackgroundRunTracker` hält Dauer und Ergebnis des letzten Laufs
je Dienst im Arbeitsspeicher (`ChangeLogMaintenance`, `ScheduledExportSnapshots`,
`WebhookDispatcher`, `MailDigest`), und `/api/v1/metrics` liefert die Werte an eine Überwachung
aus. Was fehlt, ist die Ansicht für den Betreiber ohne Prometheus: eine Seite unter
„Einstellungen“ mit Zeitpunkt, Ergebnis und **nächstem geplanten Lauf** — Letzteren kennt der
Tracker heute nicht, er müsste vom jeweiligen Dienst mitgemeldet werden. Dazu beim Zeitplan-Export
„nichts geändert, übersprungen“, denn genau das ist der Fall, den man sonst für einen Fehler
hält — der `ScheduledExportSnapshots` unterscheidet ihn bisher nicht vom erfolgreichen Lauf.

*Aufwand: S · Migration: nein · Format: unverändert*

---

## Reihenfolge-Vorschlag

Wenn zuerst das gemacht werden soll, was am meisten fehlt und am wenigsten kostet:

1. **F70 + F71** — Darstellung, Sprache und aktives Projekt je Benutzer. Zwei Entscheidungen aus
   der Einzelplatz-Zeit, die seit den Konten schlicht falsch sind; alles andere in Abschnitt D
   setzt voraus, dass mehrere Leute gleichzeitig sinnvoll arbeiten können.
2. **F85 + F86** — Fehlanmeldungen und Sitzungen. Das Tool steht self-hosted im Netz, und beides
   ist eine Handvoll Zeilen an einer Stelle, an der man später ungern nachbessert.
3. **F51 + F50** — Pflicht ab Stand und Vorgabewerte. Der Bearbeitungsstand ist da, aber die
   Pflichtprüfung nimmt ihn noch nicht zur Kenntnis; zusammen machen sie den Entwurf erst
   praktikabel — und F74 hängt daran.
4. **F61 + F60 + F62** — Vergleich, Verteilung, Ausreißer. Der Bestand ist vollständig
   auswertbar, die Auswertung ist die billigste Wirkung je Zeile Code.
5. **F66 + F67 + F68** — Erwähnungen, Fäden, Aufgabe aus einem Fund. Drei kleine Griffe, die aus
   den vorhandenen Werkzeugen (Feed, Anmerkungen, Kanban) eine Zusammenarbeit machen.
6. **F49 + F76** — Asset-Referenzfeld und Asset-Rollen. Die letzte große Lücke zwischen Bestand
   und Engine: Alles außer dem Icon ist heute unbenannt.
7. **F80** — Export in ein Arbeitsverzeichnis. Der git-freundliche Export ist gebaut, aber der
   letzte Handgriff fehlt, und ohne ihn benutzt ihn niemand.
8. **F53 + F54** — Bedingungsvorlagen und Attribut-Katalog. Beides sind Eingriffe in tragende
   Teile; sie lohnen erst, wenn ein Projekt groß genug ist, dass die Wiederholung wehtut.
9. Alles Weitere nach Bedarf.
