# GameDevManager

## Die Idee

Ich habe ein Dashboard mit Cards, welche ich selber konfigurieren kann.
Dazu habe ich noch eine Topbar, in welcher Icons zu sehen sind für die verschiedenen Module wie z. B. Items, Charaktere, Dörfer, Karten, usw.
Die Topbar zeigt immer alle Module und highlightet das Modul, in welchem ich mich gerade befinde.

## Zielgruppe

Indie Game Developer, welche eine Übersicht und Management für ihr Spiel benötigen. Mit dieser Software lassen sich verschiedene Bereiche abdecken und diese sauber planen.
Alles, was eher fachlich und nicht technisch ist, kann hiermit verwaltet werden.

## Dashboard

Hier werden die Module als Cards angezeigt. Der Nutzer kann einstellen, welche Module angezeigt werden und wie sie angeordnet sind.
Auf dem Dashboard wird eine Card immer fest angezeigt (**Import/Export**).

### Zukünftig

Über den Import/Export kann zum einen einfaches JSON zusammen mit den Images, Sounds, VFX usw. als ZIP exportiert werden oder ein direkter Export aus dem GameDevManager in die Entwicklungsumgebung wie Unity, Unreal Engine und Godot erfolgen. In noch weiterer Zukunft eventuell auch weitere.

- Referenzierung immer über GUIDs.
- Exporte müssen immer versioniert und diffbar sein, um zu sehen, was ein Content-Update verändert hat.

## Beschreibung der Module

### Item-Modul

Hier möchte ich Items definieren können, also den Namen, ein Bild/Sprite, eventuelle Werte für Ausrüstung.

- Der Nutzer soll die Möglichkeit haben, bestimmte Item-Arten zu definieren (z. B. Waffe oder Rüstung) und zu einer Item-Art die Felder, welche befüllt werden können, selber definieren können.
- Exotische Items haben einzigartige Funktionen oder Werte. Hierfür muss der Nutzer die Möglichkeit haben, für einzelne Items eigene Felder zu definieren.

### Crafting-Modul

Baut auf dem Item-Modul auf. Hier können z. B. definierte Items wie Holz oder Stein zu einem Crafting-Rezept zusammengeführt werden.

> **Beispiel:** 3x Holz + 5x Kohle = Fackel

- Zum Schluss soll dieses Rezept in einer Liste auftauchen, in der man nach Items filtern kann.
- Da jedes Item getrackt wird (was man benötigt, um was zu machen), sollte man sich auch ganze Crafting-Trees als Graphen anzeigen lassen können für mehrstufige Rezepte.

### Währungsmodul

In diesem Modul können Währungen im Spiel festgelegt werden, in einer beliebigen Anzahl an Variationen.
Diese Währungen können dann von Händlern entgegengenommen werden.

### NPC-Modul

Hier können einzelne NPCs konfiguriert werden, mit Namen etc.

- Ähnlich zum Item-Modul soll der Nutzer auch hier Arten definieren können mit selbst definierten Feldern, die gefüllt werden können.
- Manche NPCs können einzigartige Werte besitzen, deshalb sollte jeder NPC unabhängig seiner Art auch noch Zusatzfelder definieren können.
- Außerdem kann angegeben werden, ob ein NPC entweder Händler, Quest, beides oder gar nichts ist.

**Händler:**

- Sollte ein NPC ein Händler sein, dann sollte der Nutzer die Items, die er zum Verkauf anbietet, auch konfigurieren können.
- Der Nutzer kann auch konfigurieren, in welcher Währung und zu welchem Preis ein Händler Ware verkauft/ankauft.
- Dazu soll zu einem Händler auch angegeben werden können, wie hoch der Lagerbestand ist und wie hoch die Auffüllzeiten sind, ab wann die Items wieder verfügbar sind.
- Manche Shops und teilweise auch nur Items aus einem Shop sind nur nach einer Bedingung verfügbar. Der Nutzer soll angeben können, welche Bedingung/Bedingungen erfüllt sein müssen.

**NPCs und Mobs:**

- Im NPC-Modul sollten NPCs und Mobs (gegnerische Entitäten) zusammen sein, aber man soll zwischen ihnen filtern können.
- Der Nutzer soll angeben können, wo welcher NPC/Mob spawnt. Manche NPCs gibt es nur einmal und haben einen festen Spawn, andere sind auch nur einmal vorhanden, aber zufällig auf der Karte.
- Andere NPCs/Mobs gibt es häufiger und spawnen nur in bestimmten Bereichen. Der Nutzer soll angeben können, wo welcher NPC/Mob auf der Karte spawnt, und kann sich dies in der Karte anzeigen lassen.

### Fraktionsmodul

Dieses Modul baut auf dem NPC-Modul auf. Hier können Fraktionen definiert werden.

- Der Nutzer soll auch hier wieder Arten mit bestimmten Feldern definieren können und jede angelegte Fraktion soll auch wieder eigene Felder ergänzen können.
- Ein NPC kann zu einer Fraktion hinzugefügt werden. Dies wird dann auch zu einem NPC in dem NPC-Modul angezeigt.
- Eine Fraktion kann Rollen an NPCs vergeben, welchen Rang sie in einer Fraktion haben.

### Diplomatie-Modul

Hier können diplomatische Angelegenheiten zwischen Fraktionen definiert werden und auch die Freundschaften/Allianzen oder auch Feindschaften zwischen Dörfern als Graphen angezeigt werden.

### Karten-Modul

Hier kann der Nutzer seine Karten anlegen, wie z. B. die Welt-Map oder auch Maps von Höhlen und Häusern.

- Hochgeladenes Format sind Image-Dateien.
- Dieses Modul baut auf den bisherigen Modulen auf.
- Hier können auf den einzelnen Karten jeweils Positionen von NPCs oder Fraktionen markiert werden und auch die Gebiete der Fraktionen eingezeichnet werden.
- Hier sollen auch einzelne Karten verknüpft werden. Z. B. eine Karte eines Haus-Innenraums kann auf der Weltkarte verlinkt werden, um diese dann mit einem Klick wechseln zu können.

### Dialogmodul

Dieses Modul baut auf dem NPC-Modul auf. Hier können Dialoge definiert werden, die entweder als Sprechblasen zufällig in der Open World bei NPCs zu sehen sind.

- Aber auch Dialoge mit dem Spieler definieren. Hier können dann Texte und Antwortmöglichkeiten zu einem NPC definiert werden.
- Ein Dialog kann zwischen einem NPC und dem Spieler sein, aber auch mehrere NPCs miteinander oder auch mehrere NPCs + Spieler.
- Manche Dialoge sind nur nach einer Bedingung verfügbar. Der Nutzer soll angeben können, welche Bedingung/Bedingungen erfüllt sein müssen.

### Story-Modul

In diesem Modul kann die Storyline definiert werden. Hier kann die Story geschrieben werden und in einem Zeitstreifen angezeigt werden.
Der Nutzer kann angeben, welche NPCs beteiligt sind und welche Fraktionen/Dörfer und Locations auf der Karte. Diese Entitäten werden dann per Referenz verknüpft.

### Quest-Modul

Dieses Modul baut auf dem Story-Modul auf. Hier können Quests/Missionen definiert werden, die der Spieler von NPCs erhalten kann.
Diese können an die Story angelehnt werden und auch mit dieser und den NPCs verknüpft.

Es wird unterschieden zwischen:

- **Hauptmission** — definiert den Storyverlauf
- **Nebenmission** — Nebenhandlungen, die kleine Belohnungen bieten
- **Events** — treten zufällig auf

Der Nutzer soll zusätzlich die Möglichkeit haben, eigene Arten mit eigenen Feldern zu definieren.

- Eine Quest kann zu einem Dialog verknüpft sein, da diese über eine Interaktion mit einem NPC stammen kann.
- Manche Quests sind nur nach einer Bedingung verfügbar. Der Nutzer soll angeben können, welche Bedingung/Bedingungen erfüllt sein müssen.

### Asset-/Sprite-Bibliotheks-Modul

Hier werden alle Sprites zu allen Entitäten aus allen Modulen nach Modul gruppiert angezeigt. Es gibt Filter, um nach Modulen zu suchen oder auch um Items o. Ä. zu filtern.

- Hier können unter anderem auch die Sprites hochgeladen oder gelöscht werden.
- Geltend für alle Entitäten aller Module: Jede Entität kann mehrere Sprites besitzen, z. B. für Animationen. In der Asset-Bibliothek kann auch ein primärer Sprite angegeben werden, welcher dann als Icon in den Modulen angezeigt wird.
- Auch Assets, welche nur für dieses Management-Tool verwendet werden, können hier hochgeladen werden, wie z. B. Marker für die Karten/Maps.
- Pro Sprite zu einer Entität können Tags vergeben werden wie Prio, Animation, Alternative Design, usw. Hier gibt es per Default keine Vorgaben, sie können aber vom Nutzer selber definiert werden.

### Spieler-Modul

Hier kann die Spielerfigur definiert werden. Zusätzlich können hier Skilltrees definiert und verwaltet werden.

- Skilltrees brauchen eine Grundlage, wie man diese als Spieler erreicht — ob dafür Punkte oder Ressourcen ausgegeben werden müssen — und was dieser Skill macht und heißt.
- Zusätzlich soll der Nutzer eigene Felder für Skills definieren können.

### Klassen-Modul

Der Nutzer soll Klassen definieren können, welche dann auf die Spielerfigur und die NPCs gemappt werden können.
Klassen haben jeweils besondere Fähigkeiten, Namen, passive Fähigkeiten und so weiter. Der Nutzer soll eigene Felder definieren können.

### Changelog

Es werden laufend Änderungen stattfinden. Damit vereinfacht wird, dies nachzuvollziehen, soll geloggt werden, welcher angemeldete Benutzer welche Änderungen getan hat.

### Loot-Table-Modul

Loot-Tables können hier definiert werden: welche Items zu welcher Wahrscheinlichkeit in welcher Quantität gedroppt werden.

- Diese Loot-Tables sollen dann im NPC-Modul auswählbar sein (welcher NPC welchen Loot-Table hat).
- Das Loot-Table-Modul nutzt die echten Item-Entitäten, um so eine direkte Verknüpfung zwischen Items und Loot-Table zu haben.

### Effekt-Modul

In diesem Modul sollen Effekte und deren Wirkung definiert werden können.

> **Beispiel:** Verbrennung — das Ziel erleidet X Brandschaden für X Sekunden, wenn betroffen.

Diese Effekte können dann Items zugewiesen werden, wie z. B. einem Feuerschwert.

### Achievement-Modul

Hier können Achievements definiert werden, welche der Spieler erreichen kann, z. B. sowas wie die Steam-Achievements.

### Sammelobjekte-Modul

Hier können Sammelobjekte definiert werden, wie Statuen, die der Spieler sammeln kann, oder Notizen, etc.
Hier sollen auf jeden Fall eigene Felder vom Nutzer definiert werden können.

### Event-Modul

Events aus dem Quest-Modul herauslösen und Events noch anpassbarer machen:

- Welche Mobs spawnen beim Event?
- Was ist der Loot-Table als Belohnung?
- Wie hoch ist die Wahrscheinlichkeit?
- Wo auf der Karte kann dieses Event passieren? Nur in Höhlen oder überall?

### Tag-Modul

Lässt Tags/Labels/Stichwörter in einem eigenen Modul definieren, bei welchen dann eingestellt werden kann, in welchen anderen Modulen und Bereichen sie verfügbar sind.

### SFX-/Audio-Modul

*(noch offen)*

### Cutscene-Modul

*(noch offen)*

### Statistik-Modul

Zeigt z. B. die Anzahl der angelegten Items an, oder wie viele NPCs es gibt, oder wie viele NPCs feindlich sind, usw.

**Erweiterung um Health Checks:**

- Zyklische Rezepte
- Items ohne jede Bezugsquelle (toter Content)
- Quests ohne Abschlussbedingung
- Dialog-Sackgassen
- Loot-Wahrscheinlichkeiten über 100 %
- Verwaiste Sprites
- Unerfüllbare Bedingungen

## Bedingungssystem

Wir brauchen ein einheitliches System, welches über alle Module hinweg verknüpfbar ist, um so sämtliche Bedingungen an Story und Quests binden zu können.

## Weiteres inhaltlich

- Stat- und Schadensformeln
- Levelkurven
- Crafting-Stationen und Zeitdauer
- Tech-Tree/Freischaltungen
- Stack-Größen und Haltbarkeit
- Respawn-Zeiten
- Tageszeit/Wetter/Biome
- Globale Suche über alle Entitäten

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
