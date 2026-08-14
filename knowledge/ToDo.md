# ToDo

Stand 13.08.2026 — **die offenen Punkte aus dem Konzept sind abgearbeitet.** Dazugekommen sind Benutzeranmeldung und Änderungsprotokoll samt Schreibkonflikt-Erkennung, der Freischaltungs-Graph (Tech-Tree), das Welt-Modul für Tageszeit/Wetter/Biome und der Feldtyp „Formel/Kurve“ für Levelkurven und Stat-/Schadensformeln (siehe „Erledigt“). Was hier bleibt, sind gesammelte Ideen; Details zu den Anforderungen stehen in [Konzept.md](Konzept.md).

## Ideen / kleinere Verbesserungen

### Inhaltspflege & Editor-Komfort

- [ ] **Massenbearbeitung.** Mehrere Entitäten markieren und gemeinsam Art zuweisen, Tags vergeben oder einen Feldwert setzen — bei hunderten Items lohnt das schnell.
- [ ] **CSV-Import/-Export je Modul.** Balancing wird oft in Tabellen gepflegt; ein Spalten-Mapping auf die Felder der Art (Import aktualisiert über die GUID- oder Namensspalte) würde den Weg Tabelle ↔ Tool schließen.
- [ ] **Lokalisierung der Spielinhalte.** Item-Namen, Beschreibungen, Dialog- und Quest-Texte in mehreren Sprachen pflegen und mit exportieren. Das größte der Ideen-Themen (eigene Übersetzungstabelle je Text, Sprachwahl im Export, Fortschrittsanzeige „was ist noch unübersetzt“) — für jedes Spiel mit mehr als einer Sprache aber zentral.
- [ ] **Fraktionsgebiete als Polygone.** Das Konzept nennt „Gebiete der Fraktionen einzeichnen“; der Kreis-Bereich der Marker ist dafür nur eine Näherung. Polygon-Zeichnen im Karten-Editor (Punktliste statt Radius am `MapMarker`).
- [ ] **Änderungsprotokoll je Entität in der Maske.** `ChangeLogService.GetForEntityAsync` liefert die Geschichte einer einzelnen Entität bereits — es fehlt nur ein Abschnitt neben der Referenzansicht, der sie zeigt.
- [ ] **Kurven vergleichen.** Zwei Levelkurven übereinander zeichnen (Spieler gegen Gegner, Klasse A gegen B) — das Diagramm kann heute nur eine.

### Betrieb & Sicherheit

- [ ] **Aufräumen alter Exportstände.** Seit das Sicherheitsnetz bei jedem ersetzenden Import und jedem Projektlöschen einen Stand anlegt, wächst das Verzeichnis von allein. Sinnvoll wäre eine Obergrenze je Projekt (die ältesten fallen weg) oder ein Höchstalter, konfigurierbar neben `Exports:StoragePath`.
- [ ] **Aufräumen des Änderungsprotokolls.** Dasselbe eine Ebene tiefer: Nach einem Jahr Arbeit stehen dort sehr viele Zeilen. Ein Höchstalter oder eine Obergrenze je Projekt wäre die passende Antwort.
- [ ] **Englische Oberfläche.** Die resx-Struktur ist darauf vorbereitet (neutral = Deutsch, Satelliten-resx je Sprache plus Sprachwahl) — reine Übersetzungsarbeit, kein Umbau.

### Richtung Engine-Anbindung

- [ ] **Engine-nativere Exporte** (später): z. B. ScriptableObject-Generierung für Unity, DataTable-taugliche Dateien für Unreal, Godot-Ressourcen; ggf. weitere Engines.
- [ ] **Read-only-HTTP-API.** Die Vorstufe dazu: Inhalte je Projekt als JSON-Endpunkte (dieselben Serialisierungsregeln wie der Export), damit ein Unity-/Godot-Editor-Plugin den Stand direkt ziehen kann statt über das ZIP zu gehen. Mit der Benutzeranmeldung stünde jetzt auch die Grundlage für API-Schlüssel.
