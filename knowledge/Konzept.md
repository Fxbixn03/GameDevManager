# GameDevManager — Konzept

Dieses Dokument ist die **fachliche Quelle der Wahrheit**: Was das Tool leisten soll und nach
welchen Regeln. Es beschreibt keine Technik — wie es gebaut ist, steht in
[CLAUDE.md](../CLAUDE.md); was darüber hinaus noch kommen könnte, in [ToDo.md](ToDo.md).

Alles, was hier steht, ist umgesetzt. Das Dokument ist im Präsens geschrieben, weil eine
Anforderung, die erfüllt ist, eine Festlegung ist — und Festlegungen gelten weiter. Absichten,
die noch keine Umsetzung haben, gehören nicht hierher, sondern in die ToDo.

**Fortschreibung:** Wer ein Modul ergänzt, ergänzt hier einen Absatz unter der passenden Gruppe.
Wer eine der Leitplanken bricht, muss sie hier ändern — nicht umgehen. Zahlenangaben (wie viele
Module, wie viele Feldtypen) stehen bewusst nirgends im Text; sie wären nach dem nächsten Modul
falsch.

---

## Die Idee

Ein selbst gehostetes Verwaltungstool für das, was an einem Spiel **fachlich** ist und nicht
technisch: Items, Figuren, Orte, Erzählung, Fortschritt. Ein strukturiertes Wiki, das die Inhalte
nicht als Fließtext hält, sondern als verknüpfte Entitäten — und sie am Ende in die Game Engine
exportiert.

Die Oberfläche besteht aus einem Dashboard, das den Projektstand zeigt, und einer Modulleiste, die
von jeder Seite aus in jedes Modul führt und das gerade geöffnete hervorhebt.

## Zielgruppe

Indie-Entwickler, die eine Übersicht über ihr Spiel brauchen und es sauber planen wollen — allein
oder in einem kleinen Team. Das Tool wird selbst betrieben, auf dem eigenen Rechner oder Server.

## Abgrenzung — was das Tool nicht ist

Diese Grenzen sind Absicht und die Begründung dafür, warum bestimmte Wünsche abgelehnt werden:

- **Keine Engine und kein Editor.** Das Tool beschreibt Inhalte, es führt sie nicht aus. Es gibt
  keinen Level-Editor, keine Physik, keine Skripte, die im Spiel laufen.
- **Kein Asset-Werkzeug.** Bilder und Audio werden verwaltet, nicht bearbeitet.
- **Keine Spiellogik.** Bedingungen werden erfasst und geprüft, soweit das ohne laufendes Spiel
  möglich ist — ausgewertet werden sie im Spiel.
- **Kein Ersatz für die Versionsverwaltung des Codes.** Nachvollziehbar sind Inhaltsänderungen
  über das Änderungsprotokoll und die Exportstände, nicht der Quellcode des Spiels.

---

## Leitplanken

Diese Regeln gelten in **jedem** Modul, auch in jedem künftigen. Sie sind der Grund, warum ein
neues Modul wenig kostet.

### Eigene Arten und eigene Felder

Das Schema ist nutzerdefiniert, nicht fest vorgegeben. In fast jedem Modul legt der Nutzer eigene
**Arten** an (Item-Art „Waffe", NPC-Art „Händler") und definiert je Art die Felder, die gefüllt
werden können.

- Arten lassen sich **ineinander stecken**: „Waffe" mit den Unterarten „Nahkampf", „Fernkampf",
  „Magie". Eine Unterart erbt die Felder ihrer übergeordneten Arten und ergänzt eigene.
- Einzelne Entitäten können **zusätzliche Felder nur für sich** bekommen — für das exotische Item
  mit der einzigartigen Wirkung, das keine eigene Art rechtfertigt.
- Wo eine Angabe das Tool selbst auswerten muss, ist sie **kein** benutzerdefiniertes Feld,
  sondern fest vorgesehen: die Preise eines Händlers, die Wahrscheinlichkeit eines Loot-Eintrags,
  die Farbe einer Seltenheit. Alles Übrige — Herstellungsdauer, Stack-Größe, Haltbarkeit,
  Respawn-Zeit, Mindestlevel — definiert der Nutzer als Feld an der Art. Das ist die Antwort auf
  die meisten „könnte man nicht noch … dazunehmen"-Fragen.

### Referenzen ausschließlich über GUIDs

Entitäten verweisen aufeinander über ihre GUID, nie über den Namen. Ein Umbenennen bricht deshalb
nichts, und derselbe Verweis trägt über Modulgrenzen hinweg.

### Ein Bedingungssystem für alles

Bedingungen laufen über ein einziges, modulübergreifendes System, das überall gleich funktioniert
und gleich aussieht — siehe unten.

### Werkzeug-Daten und Spielinhalte sind getrennt

Was das Spiel beschreibt, gehört in den Export. Was die **Arbeit am Spiel** beschreibt — Kanban-
Karten, Whiteboards, das Änderungsprotokoll, Dashboard-Anordnung, Benutzer und ihre Rechte —
bleibt draußen und übersteht auch einen ersetzenden Import. Bei jeder neuen Datenart ist das die
erste zu beantwortende Frage.

### Versionierte, diffbare Exporte

Ein Export muss nachvollziehbar machen, was ein Content-Update verändert hat. Derselbe Stand
ergibt deshalb denselben Export — Byte für Byte, unabhängig von Rechner und Spracheinstellung.

### Nachvollziehbarkeit

Jede Änderung wird mit Benutzer, Zeitpunkt und geänderten Angaben protokolliert. Auch Löschungen —
gerade die.

### Selbst gehostet, ohne Außenverbindung

Das Tool läuft ohne Internet. Keine externen Dienste, keine CDNs, keine Telemetrie. Die Datenbank
wählt der Betreiber (SQL Server, PostgreSQL, MySQL oder SQLite); alle verhalten sich gleich.

---

## Der Rahmen

### Projekte

Alle Inhalte hängen an einem **Projekt** — ein Spiel, ein Prototyp, ein Add-on. Es lassen sich
beliebig viele anlegen; eines ist das aktive, und es wird über die Kopfleiste gewechselt. Ein
Projekt lässt sich kopieren (alles inklusive Dateien, mit neuen GUIDs), exportieren, importieren
und löschen. Vor dem Löschen und vor einem ersetzenden Import legt das Tool selbsttätig einen
Exportstand als Sicherheitsnetz an.

### Anmeldung, Benutzer und Berechtigungen

Das Tool liegt hinter einer Anmeldung — anders ließe sich nicht protokollieren, wer etwas geändert
hat. Ein ausgeliefertes Standardkonto gibt es nicht: Beim ersten Start wird das erste Konto
angelegt, und es ist Verwalter.

Je Benutzer wird eingestellt, was er darf:

- **Lesen oder auch schreiben.**
- **Welche Module er sieht** — gesperrte Module verschwinden aus Modulleiste, Suche und Dashboard
  und sind auch über die eingetippte Adresse nicht erreichbar.
- **Ob Export und Import offenstehen** — getrennt, denn Herausgeben und Überschreiben sind zwei
  verschiedene Vertrauensfragen.
- **Verwalter** dürfen alles, verwalten die Benutzer und die Schlüssel der Programmierschnittstelle
  und dürfen das Änderungsprotokoll kürzen. Der letzte Verwalter kann sich nicht selbst
  entmachten, sperren oder löschen.

Die Passwortregeln (Mindestlänge, Ziffer, Sonderzeichen) stellt der Verwalter ein. Wer allein
arbeitet, kann Passwörter auch ganz abschalten; die Anmeldung mit dem Namen bleibt, damit das
Protokoll seinen Urheber behält.

### Dashboard

Das Dashboard beantwortet die Frage „Wie steht mein Projekt da?" — es **wiederholt bewusst nicht
die Navigation**, denn die Modulleiste erreicht ohnehin jedes Modul von jeder Seite. Statt einer
Kachel je Modul stehen dort Bänder, jedes mit einer eigenen Frage:

- **Projektleiste** — Name, Gesamtzahl der Inhalte, Zustand als eine Zahl, Zeitpunkt des letzten
  Exportstands.
- **Weiterarbeiten** — die zuletzt bearbeiteten Einträge quer über alle Module.
- **Zustand** — die Health Checks als Fundzahl mit Sprungziel.
- **Inhaltsbestand** — jedes Modul mit seiner Anzahl, gruppiert nach Arbeitsfeld. Auskunft und
  Absprung in einem.
- **Datenbank** — Provider und Verbindung. Einrichtungsdiagnose, deshalb standardmäßig aus.

Welche Bänder erscheinen und in welcher Reihenfolge, stellt der Nutzer je Projekt ein. Ein leeres
Projekt bekommt stattdessen die Einstiege, mit denen man anfängt.

### Modulleiste

Die Modulleiste steht auf jeder Seite und ist der Weg, der **jedes Modul von überall** erreicht;
das geöffnete ist hervorgehoben. Zwei Zugeständnisse an die Wirklichkeit, die diesen Satz nicht
aufgeben:

- Die **Reihenfolge** ist einstellbar.
- Was **nicht mehr hineinpasst**, sammelt ein Aufklapp-Knopf am Ende der Leiste. Erreichbar
  bleibt alles; nur nicht alles gleichzeitig sichtbar.

Module, die ein Benutzer nicht sehen darf, stehen nicht in der Leiste — sie sind für ihn nicht
vorhanden.

### Globale Suche

Ein Suchfeld in der Kopfleiste durchsucht alle Module auf einmal: Namen und Beschreibungen, die
Textwerte der benutzerdefinierten Felder, gesprochene Dialogzeilen, dazu Assets und Arten. Eine
eingefügte GUID führt direkt zu ihrer Entität. Ein Treffer, der nicht am Namen liegt, sagt dazu,
woran er liegt.

### Oberfläche

Deutsch und Englisch, umstellbar in den Einstellungen; dazu ein helles und ein dunkles
Erscheinungsbild. Häufige Handgriffe sind über Tastatur erreichbar (Suche, Speichern, Modulwechsel),
und wo eine eigene Auswahl danebensteht, gibt es ein Rechtsklick-Menü — überall sonst bleibt das
Menü des Browsers stehen, weil es Kopieren, Einfügen und Rechtschreibprüfung anbietet.

---

## Die Module

Die Module sind nach Arbeitsfeldern gruppiert. Wo unten nichts anderes steht, gilt für jedes:
eigene Arten mit eigenen Feldern, individuelle Felder je Eintrag, mehrere Sprites mit einem
primären als Icon, Tags, Bedingungen, Referenzansicht, Kopieren, CSV-Austausch und Export.

### Welt

**Karten** — Welt-, Höhlen- und Innenraumkarten als Bilddateien. Auf einer Karte werden Punkte,
kreisförmige Bereiche und **Polygon-Gebiete** eingezeichnet; jede Markierung kann auf eine beliebige
Entität zeigen — den Spawn-Ort eines NPCs, das Gebiet einer Fraktion, den Schauplatz eines
Story-Abschnitts. Karten lassen sich untereinander verknüpfen: Das Haus auf der Weltkarte führt mit
einem Klick auf die Karte seines Innenraums. Markierungen liegen auf **Ebenen**, die sich einzeln
ein- und ausblenden lassen. Positionen sind relativ gespeichert und sitzen deshalb in jeder
Darstellungsgröße richtig — auch wenn dasselbe Bild später in höherer Auflösung neu hochgeladen wird.

**Fraktionen** — Gruppen, denen NPCs angehören. Je Mitglied wird die **Rolle** festgehalten, die es
in der Fraktion hat (Anführer, Späher, Handelsmeister); die Zugehörigkeit ist auch am NPC zu sehen.

**Diplomatie** — die Verhältnisse zwischen Fraktionen: Allianzen, Freundschaften, Feindschaften,
dargestellt als Graph.

**Welt** — Tageszeiten, Wetterlagen und Biome. Ein benannter Zustand mit Reihenfolge und Farbe, an
dem Bedingungen hängen („nur nachts", „nicht bei Regen", „nur in der Wüste"). Alles Weitere — die
Dauer einer Tageszeit, die Sichtweite bei Nebel — ist ein Feld der Art.

### Inhalte

**Items** — der Ausgangspunkt: Name, Beschreibung, Sprite, dazu die Werte, die der Nutzer über
Arten und Felder selbst festlegt. Für exotische Items gibt es Felder nur für dieses eine Item.

**Crafting** — Rezepte auf Basis der Items: benötigte Items, Ziel-Items, Rezept-Art. *3× Holz +
5× Kohle = Fackel.* Mehrere Ziele sind der Normalfall für Nebenprodukte („1× Barren + 2× Schlacke").
Ein Rezept trägt keinen eigenen Namen — er entsteht aus seinen Zielen. Weil jedes Item verfolgt
wird, lassen sich mehrstufige Rezepte als **Crafting-Tree** aufklappen und auf ihre Grundstoffe
herunterrechnen, die Ausbeute je Stufe eingerechnet. Was ein Rezept sonst noch braucht — Werkbank,
Dauer, Mindestlevel — sind Felder der Rezept-Art.

**Währungen** — beliebig viele nebeneinander, jede mit ihrem Symbol. Händler nehmen sie entgegen.

**Seltenheiten** — Gewöhnlich, Selten, Episch, … Einmal je Projekt festgelegt mit Name, Farbe und
Rang, danach über ein Feld in jedem Modul verwendbar. Das einzige Modul **ohne** eigene Arten und
Felder: Eine Seltenheit ist ein Nachschlagewert, und ihre Farbe muss jede Ansicht zuverlässig
finden.

**Loot-Tables** — welche Items mit welcher Wahrscheinlichkeit in welcher Menge fallen, auf den
echten Item-Entitäten. Dasselbe Item darf mehrfach vorkommen — „zu 50 % eine Münze, zu 5 % gleich
zwanzig" ist ein üblicher Fall. Zwei Auswertungsarten, weil die Prozentzahlen je nach Verfahren
etwas anderes bedeuten: **einzeln gewürfelt** (eine Summe über 100 % ist normal) oder **ein
Treffer aus allen** (dort wären die hinteren Einträge über 100 % hinaus unerreichbar). NPCs und
Events wählen eine Loot-Table aus.

### Figuren

**NPCs** — Figuren und Gegner in einem Modul, unterschieden über eine Filter-Angabe statt über die
Art, damit beides nebeneinander gepflegt und getrennt betrachtet werden kann. Ein NPC ist
**Händler**, **Questgeber**, beides oder nichts davon.

- Als Händler führt er Waren: Item, Währung, Verkaufs- und Ankaufspreis, Lagerbestand und
  Auffüllzeit. Das Angebot als Ganzes **und jeder einzelne Posten** kann an eine Bedingung geknüpft
  sein.
- Sein Vorkommen auf der Karte kommt aus dem Karten-Modul, sein Loot aus dem Loot-Modul.
- **Einzigartig** ist ein Schalter für die Figur, die es nur einmal gibt.
- **Beziehungen** verbinden NPCs untereinander. Die Beziehungsart ist ein frei definierbares
  Bezeichnungspaar für Hin- und Rückrichtung („Vater" / „Kind", „Mentor" / „Schüler").
- **Vorlieben, Persönlichkeit und Wesenszüge** beschreiben die Figur als Figur — Stichwortlisten
  und Regler, nicht Freitext, damit sich danach vergleichen lässt.

**Skilltrees** — Skilltrees und die Skills darin: wie ein Skill heißt, was er bewirkt, was er
kostet (Skillpunkte **oder** eine Menge eines Items) und welcher Skill ihm vorausgeht — daraus
entsteht der Baum. Dazu eigene Arten und Felder. **Die Spielerfigur ist ein NPC** und steht im
NPC-Modul: Sie hat Beziehungen, Dialoge, Fraktionen und Auftritte in der Story wie jede andere
Figur — sie doppelt zu führen hieße, jede dieser Verknüpfungen zweimal zu bauen.

**Klassen** — Klassen für Spielerfiguren und NPCs, mit ihren Fähigkeiten, passiven Fähigkeiten und
eigenen Feldern.

**Effekte** — Wirkungen und ihre Beschreibung. *Verbrennung — das Ziel erleidet X Brandschaden für
X Sekunden.* Effekte werden Items, Skills und Klassen über Referenzfelder zugewiesen.

### Erzählung

**Dialoge** — zwei Formen: **Sprechblasen**, deren Zeilen unabhängig nebeneinander stehen und
zufällig erscheinen, und **Gespräche** mit Verlauf, Antwortmöglichkeiten und Verzweigungen.
Beteiligt sind beliebig viele NPCs und wahlweise der Spieler — damit sind NPC + Spieler, mehrere
NPCs untereinander und mehrere NPCs + Spieler abgedeckt. Ein Gespräch lässt sich als Graph
anzeigen; Zeilen, die von keiner Antwort erreicht werden, fallen dort auf. Dialoge können an
Bedingungen hängen.

**Story** — die Storyline als Zeitstreifen, mit Reihenfolge per Drag & Drop. Je Abschnitt: der
Text, die beteiligten NPCs, Fraktionen und der Ort auf der Karte, dazu Stimmung, Spielzeitpunkt,
Dauer und Schauplatz. Abschnitte lassen sich **untereinander verknüpfen** (Vorgeschichte,
Parallelhandlung, Folge).

**Quests** — Haupt- und Nebenmissionen, angelehnt an die Story und mit ihr, den NPCs und den
Dialogen verknüpft, aus denen sie stammen. Verfügbarkeit und Abschluss sind zwei getrennte
Bedingungssätze, weil beide gleichzeitig an derselben Quest hängen.

**Events** — was zufällig geschieht, aus dem Quest-Modul herausgelöst und eigenständig: welche
Mobs in welcher Zahl auftauchen, welche Loot-Table die Belohnung ist und wie wahrscheinlich es ist.
Wo es geschehen kann, wird im Karten-Modul markiert — eine Markierung, die auf das Event zeigt,
statt einer zweiten Ortsangabe am Event selbst.

**Cutscenes** — als Storyboard aus Einstellungen, verknüpft mit dem Story-Abschnitt und dem Dialog,
zu dem sie gehören.

### Fortschritt

**Achievements** — Erfolge nach Art der Steam-Achievements, jeder mit der Bedingung, unter der er
freigeschaltet wird, und einem Schalter für die verborgenen.

**Sammelobjekte** — Statuen, Notizen und alles andere, was der Spieler zusammensucht.

### Produktion

**Assets** — die Sprite-Bibliothek über alle Module, nach Modul gruppiert und filterbar. Hier wird
hochgeladen und gelöscht. Jede Entität darf **mehrere** Sprites haben (Animationsphasen,
alternative Entwürfe); eines davon ist das primäre und erscheint als Icon in den Modul-Listen.
Auch Dateien, die nur das Tool selbst braucht — Kartenmarker etwa —, liegen hier. Je Sprite lassen
sich Stichwörter vergeben (Prio, Animation, Alternative); vorgegeben ist keines.

**Tags** — modulübergreifende Labels. Je Tag wird eingestellt, in welchen Modulen es zur Verfügung
steht.

**SFX/Audio** — Sounds und Musik mit ihren Audiodateien.

### Auswertung

Module ohne eigene Inhalte: Sie zeigen, was die anderen Module ohnehin schon tragen. Deshalb
stehen sie nicht im Inhaltsbestand und können nicht veralten.

**Statistik** — Kennzahlen über alle Module und die Health Checks (siehe unten).

**Freischaltungen** — der Tech-Tree: was was freischaltet, gelesen aus dem Bedingungssystem und
gezeichnet als Graph. Eine eigene Datenhaltung hätte dieselbe Aussage ein zweites Mal gespeichert
und wäre ab der ersten Änderung falsch.

**Verbindungen** — NPCs und ihre Beziehungen als Netz, eingefärbt nach Fraktion.

**Änderungen** — das Änderungsprotokoll: wer wann was geändert hat, über alle Module und je
Entität in deren Maske. Ein Import ist **ein** Eintrag und nicht tausend. Wie lange und wie viel
aufbewahrt wird, stellt der Verwalter ein.

**Massenbearbeitung** — viele Einträge auf einmal ändern: Art zuweisen, Tags vergeben oder
entziehen, einen Feldwert setzen oder leeren. Eine Seite für alle Module statt einer Mehrfachauswahl
in jeder Liste. Ein Wert landet nur dort, wo das Feld gilt; der Rest wird als übersprungen gemeldet
statt stillschweigend geschrieben.

**Lokalisierung** — siehe unten.

**Engine-Presets** — siehe unten.

**ToDo** und **Whiteboard** — die Arbeit am Spiel, nicht das Spiel: Kanban-Boards und
Skizzenflächen, mehrere je Projekt, mit sofortiger Aktualisierung für alle, die gerade zusehen.
Werkzeug-Daten, also nicht im Export.

---

## Felder

Ein Feld hat einen Namen, einen Datentyp und wahlweise die Kennzeichnung als **Pflichtfeld**. Zur
Auswahl stehen: einzeiliger Text, mehrzeiliger Text, ganze Zahl, Kommazahl, Ja/Nein, Datum,
Auswahlliste, Farbe, **Verweis auf eine andere Entität** (das Zielmodul steht am Feld), **Verweis
auf eine Seltenheit** und **Formel/Kurve**.

Zwei Besonderheiten:

- **Stichwortliste** — ein Textfeld lässt sich auf „Liste" umstellen: die Elemente eines Zaubers,
  die Schadensarten einer Waffe. Erfasst wird sie als Kette von Stichwörtern statt als Freitext,
  bleibt aber Text und damit auffindbar.
- **Formel/Kurve** — für Stat- und Schadensformeln und für Levelkurven: ein Ausdruck über `x`
  (`100 * x ^ 1.5`), eine Spanne und eine Wertetabelle. Beides zusammen, nicht entweder-oder:
  Einzelne Stufen lassen sich überschreiben, ohne die Formel zu verlieren — der Boss auf Stufe 50,
  der einen Sprung bekommt. Eine Vorschau zeichnet die Kurve, und Kurven aus dem ganzen Projekt
  lassen sich zum Vergleich darüberlegen.

---

## Bedingungssystem

Ein einziges System, über alle Module hinweg verknüpfbar, damit sich Bedingungen an Story, Quests,
Dialoge, Händler und alles Weitere binden lassen. Es sieht überall gleich aus und funktioniert
überall gleich.

Ein Bedingungssatz hängt an einer Entität — **oder an einem Teil davon**: an einem einzelnen
Händler-Posten, nicht nur am Angebot als Ganzem. Mehrere Sätze an derselben Entität werden über
ihren Zweck unterschieden: „ist verfügbar, wenn …", „das Warenangebot erscheint, wenn …", „gilt
als abgeschlossen, wenn …", „wird freigeschaltet, wenn …". Innerhalb eines Satzes gilt entweder
**alles** oder **eines**.

Eine einzelne Bedingung ist eine der folgenden Arten: Item besitzen, Währung besitzen, Zustand
einer Quest, NPC besiegt, gesetzter Schalter, Spielerlevel, Tageszeit, Wetter, Biom,
freigeschaltet — dazu **frei formuliert** für alles, was das Tool noch nicht kennt. Je nach Art
tragen ein Vergleich mit einer Zahl, ein Ja/Nein oder der Verweis auf eine andere Entität die
Aussage; das Zielmodul ergibt sich meist aus der Art selbst, nur bei „freigeschaltet" wird es
gewählt — freischalten lässt sich ein Skill, ein Rezept, ein Gebiet.

Ein leerer Satz wird nicht gespeichert: „keine Bedingung" soll nichts hinterlassen.

---

## Health Checks

Prüfungen, die den Bestand auf Widersprüche und toten Inhalt durchsehen. Sie stehen auf der
Statistik-Seite und als Zusammenfassung auf dem Dashboard:

- Zyklische Rezepte
- Items ohne jede Bezugsquelle (toter Content)
- Quests ohne Abschlussbedingung
- Dialog-Sackgassen
- Loot-Wahrscheinlichkeiten über 100 % — nur dort, wo sie tatsächlich unerreichbare Einträge
  bedeuten
- Verwaiste Sprites
- Unerfüllbare Bedingungen
- Ringe im Freischaltungs-Graphen

Ein Health Check **verbietet nichts**. Er meldet, was auffällt; gespeichert und exportiert wird
trotzdem. Beim Umbauen ist ein Zwischenstand regelmäßig unstimmig, und ein Tool, das ihn nicht
sichern lässt, steht im Weg. Aus demselben Grund meldet ein Check nur, was sich ohne Kenntnis des
laufenden Spiels sicher feststellen lässt.

---

## Import, Export und Integration

### Export

Der komplette Projektstand als **ZIP**: die Inhalte als JSON, je Modul eine Datei, dazu die
Dateien — Bilder, Sounds und alles Weitere. Wahlweise neutral oder im Ordner-Layout von **Unity**,
**Unreal Engine** oder **Godot**; der Inhalt ist derselbe, nur der Ort im Archiv unterscheidet
sich.

Das Format trägt eine **Versionsnummer**, und alle Listen sind stabil sortiert — derselbe Stand
ergibt denselben Export. Nur so ist ein Diff aussagekräftig.

### Engine-Presets

Ein Preset ist ein Bauplan: „so sieht ein NPC in Unity aus". Es legt fest, wie ein Eintrag eines
Moduls (wahlweise einer bestimmten Art) in der Engine heißt und welche Eigenschaft sich aus
welcher Quelle füllt — Name, Beschreibung, ein Feld, ein fester Wert, die GUID, die Art oder der
Dateiname des Icons.

Beim Export in eine Engine entstehen daraus zusätzlich **engine-native Dateien**: für Unity eine
Klasse samt Daten je Eintrag, für Unreal eine DataTable-taugliche CSV, für Godot je Eintrag eine
Ressourcendatei. Sie ergänzen den neutralen Inhalt, sie ersetzen ihn nicht — wer die Presets nicht
pflegt, bekommt weiterhin alles.

### Exportstände und Diff

Exporte lassen sich aufbewahren. Zwei Stände — oder ein Stand gegen den aktuellen Bestand —
werden Entität für Entität verglichen: neu, entfernt, geändert samt der geänderten Angaben. Damit
ist beantwortbar, was ein Content-Update verändert hat.

Wie viele Stände und wie lange aufbewahrt werden, ist einstellbar; der jüngste bleibt in jedem
Fall stehen.

### Import

Ein Export-ZIP lässt sich wieder einlesen — für den Umzug eines Projekts und für die
Wiederherstellung. Der Import stellt immer einen **vollständigen** Projektstand her und ist
bewusst kein Teil-Zusammenführen: Entweder ist das Ziel leer, oder der Bestand wird vorher
ersetzt. Vorher entsteht selbsttätig ein Exportstand.

### CSV je Modul

Für die Pflege von Zahlenwerten in einem Tabellenprogramm: ein Modulbestand als Tabelle heraus und
wieder herein. Der CSV-Import **aktualisiert, er ersetzt nicht** — was in keiner Zeile steht,
bleibt unangetastet, denn eine Tabelle ist ein Ausschnitt und ein Ausschnitt darf nichts löschen.
Eine leere Zelle löscht dagegen den Wert; sonst ließe er sich über die Tabelle nie zurücknehmen.

### Lesende Programmierschnittstelle

Eine HTTP-Schnittstelle liefert die Inhalte eines Projekts an eigene Werkzeuge und Engine-Plugins,
angemeldet über einen **API-Schlüssel** statt über die Anmeldung im Browser — ein Plugin hat
keinen Browser. Schlüssel werden vom Verwalter vergeben, lassen sich auf ein Projekt beschränken,
stehen im Klartext genau einmal da und sind **nur lesend**: Ein schreibender Zugang wäre ein
zweiter Weg an Rechteprüfung, Änderungsprotokoll und Konflikterkennung vorbei.

### Lokalisierung der Spielinhalte

Je Projekt werden die Sprachen festgelegt; eine davon ist die **Ausgangssprache** — ihre Texte
stehen dort, wo sie ohnehin stehen, und sind keine Übersetzung. Übersetzt werden Namen,
Beschreibungen und Textfelder; Zahlen, Schalter, Verweise und Stichwortlisten bleiben draußen, weil
sie in jeder Sprache dieselben sind.

Zu jeder Übersetzung wird der Ausgangstext festgehalten, wie er beim Übersetzen aussah. Ändert
sich das Original, gilt die Übersetzung als **veraltet**, statt still falsch zu bleiben. Der
Fortschritt zählt „fehlt" und „veraltet" getrennt.

Im Export liegt neben dem Bestand je Sprache eine fertige Zeichenketten-Tabelle. Die Sprachwahl
fällt damit im Spiel und nicht im Export.

---

## Referenzansicht

Bei jeder Entität — wie „Find All References" in Visual Studio:

> **Eisenschwert** — wird benutzt von:
>
> - ✓ Händler
> - ✓ Quest
> - ✓ Crafting
> - ✓ Loot
> - ✓ NPC
> - ✓ Story

Sie ist keine Bequemlichkeit, sondern die Voraussetzung dafür, überhaupt etwas löschen zu können:
Ohne sie ist bei keinem Eintrag zu beantworten, was daran hängt. Ein neues Modul, das auf fremde
Entitäten verweist, muss deshalb in dieser Ansicht auftauchen.

---

## Zusammenarbeit

Mehrere Personen arbeiten am selben Bestand. Zwei Zusagen dazu:

- **Kein stilles Überschreiben.** Wurde ein Eintrag zwischenzeitlich anderswo geändert, meldet das
  Speichern einen Konflikt, statt die fremde Änderung zu verwerfen.
- **Nachvollziehbarkeit statt Sperren.** Einträge werden nicht gegeneinander verriegelt; wer wann
  was geändert hat, steht im Protokoll — auch an der Entität selbst.
