# ToDo

Stand 13.08.2026 — **die offenen Punkte aus dem Konzept sind abgearbeitet.** Dazugekommen sind Benutzeranmeldung und Änderungsprotokoll samt Schreibkonflikt-Erkennung, der Freischaltungs-Graph (Tech-Tree), das Welt-Modul für Tageszeit/Wetter/Biome und der Feldtyp „Formel/Kurve“ für Levelkurven und Stat-/Schadensformeln (siehe „Erledigt“). Was hier bleibt, sind gesammelte Ideen; Details zu den Anforderungen stehen in [Konzept.md](Konzept.md).

## Ideen / kleinere Verbesserungen

### Inhaltspflege & Editor-Komfort

- [ ] **Massenbearbeitung.** Mehrere Entitäten markieren und gemeinsam Art zuweisen, Tags vergeben oder einen Feldwert setzen — bei hunderten Items lohnt das schnell.
- [ ] **CSV-Import/-Export je Modul.** Balancing wird oft in Tabellen gepflegt; ein Spalten-Mapping auf die Felder der Art (Import aktualisiert über die GUID- oder Namensspalte) würde den Weg Tabelle ↔ Tool schließen.
- [ ] **Lokalisierung der Spielinhalte.** Item-Namen, Beschreibungen, Dialog- und Quest-Texte in mehreren Sprachen pflegen und mit exportieren. Das größte der Ideen-Themen (eigene Übersetzungstabelle je Text, Sprachwahl im Export, Fortschrittsanzeige „was ist noch unübersetzt“) — für jedes Spiel mit mehr als einer Sprache aber zentral.
- [ ] **Änderungsprotokoll je Entität in der Maske.** `ChangeLogService.GetForEntityAsync` liefert die Geschichte einer einzelnen Entität bereits — es fehlt nur ein Abschnitt neben der Referenzansicht, der sie zeigt.
- [ ] **Kurven vergleichen.** Zwei Levelkurven übereinander zeichnen (Spieler gegen Gegner, Klasse A gegen B) — das Diagramm kann heute nur eine.

### Betrieb & Sicherheit

- [ ] **Aufräumen alter Exportstände.** Seit das Sicherheitsnetz bei jedem ersetzenden Import und jedem Projektlöschen einen Stand anlegt, wächst das Verzeichnis von allein. Sinnvoll wäre eine Obergrenze je Projekt (die ältesten fallen weg) oder ein Höchstalter, konfigurierbar neben `Exports:StoragePath`.
- [ ] **Aufräumen des Änderungsprotokolls.** Dasselbe eine Ebene tiefer: Nach einem Jahr Arbeit stehen dort sehr viele Zeilen. Ein Höchstalter oder eine Obergrenze je Projekt wäre die passende Antwort.
- [ ] **Englische Oberfläche.** Die resx-Struktur ist darauf vorbereitet (neutral = Deutsch, Satelliten-resx je Sprache plus Sprachwahl) — reine Übersetzungsarbeit, kein Umbau.

### Richtung Engine-Anbindung

- [ ] **Engine-nativere Exporte** (später): z. B. ScriptableObject-Generierung für Unity, DataTable-taugliche Dateien für Unreal, Godot-Ressourcen; ggf. weitere Engines.
- [ ] **Read-only-HTTP-API.** Die Vorstufe dazu: Inhalte je Projekt als JSON-Endpunkte (dieselben Serialisierungsregeln wie der Export), damit ein Unity-/Godot-Editor-Plugin den Stand direkt ziehen kann statt über das ZIP zu gehen. Mit der Benutzeranmeldung stünde jetzt auch die Grundlage für API-Schlüssel.


### Erweiterung bestehender Features

#### Allgemein

- Shortcuts für bestimte Funktionen

#### Karten

- Layer auf der Karte für das einfügen von objekten auf karten
  - Aktivieren und deaktivieren von Layern
  - Ein- und ausblenden von Layern

#### NPCs

- Beziehungen zwischen NPCs, können durch den anwender frei definiert werden auch die bezeichnungen
  - Ist Vater Von <--> Ist Sohn Von
  - Ist Bruder Von <--> Ist Bruder von
  - Ist Bruder Von <--> Ist Schwester von
  - ISt Verbündeter von <--> Ist verbündeter von
  - Ist vorgesetzter von <--> ist untergebener von
  - Außerdem kann angegeben werden ob die Beziehenung Freundlich, Neutral oder Feindlich ist

- NPC Option, Ist ein NPC ein einzigartiger NPC welcher zb in einerm DOrf herunläuft oder ist es eher ein mob wie ein waschbär welcher immer wieder und mehrfach spawnen kann.

Hier können mit komma getrennt werte eingegeben und mit enter bestätigt werden
- Vorlieben
- Persöhnlichkeit
- Wensenszüge (hier kann zu jedem wert ein wert eingetragen werden von 0 bis 10, die Werte werden als gestrichelte balken dargetsellt. Der Nutzer kann auf die balken klicken sie sind immer von links aus gefüllt und zeigen visuell dem numerischen wert von 0 bis 10) 
  - Empathie
  - Impulsivität
  - Loyalität
  - Mut
  - Ehrlichkeit
  - Dominanz
  - Geduld
  - Misstrauen
  - Risikobereitschaft
  - Mitgefühl

#### Story Modul

- Erweitern der Optionen zu einem Abschnitt
  - Typ
  - Stimmung
  - Datum
  - Dauer
  - Ort
  - Verknüpfter Karten Marker oder Position auf karte
  - Bilder/Sprites zur Scene
  - Verknüpfungen zu anderen Scenen
- Verschieben von Scenen im ABlauf per Drag and Drop

#### Neuen Module

- ToDo (Mehrere Kanban Boards zur projektverwaltung die der Benutzer nach belieben anlegen und verwalten und löschen kann)
- Whiteboard (Hier können Nutzer zusammen an einem Projekt arbeiten und zeichnen, Notizen anheften, etc, inspiriert bei Miro)
- Vrbindungen (In diesem Modul werden NPCs und Fraktionen angezeigt wie wer mit wem verknüpft ist. Dafür werden die Bilder der NPCs verwendet und als runde rofilbilder mit ihrem namen darunter angezeigt und dann striche zu den jeweiligen verknüpfungen. Die Runden bilder haben eine Farbliche umrandung je nachdem in welcher fraktion sie sind, oder fraktionslos sidn)

#### Grundumbau des SPieler Moduls

- Der Spieler wird zukünftig als NPC behandelt und kein eigenes Modul mehr
- Das Spieler Modul wird zum SkillTree Modul
- Das Modul ist nurnoch zum administrieren von Skilltrees 