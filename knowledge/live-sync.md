# Live-Sync-Protokoll (Tool ↔ Engine-Editor)

Der Live-Sync meldet verbundenen Engine-Editoren, dass sich Inhalte geändert haben — der
Editor lädt daraufhin die betroffenen Module über die lesende HTTP-API nach. Das Protokoll
transportiert **Ereignisse, keine Inhalte**: Die Inhalte kommen immer aus
`GET /api/v1/projects/{projectId}/modules/{moduleKey}`, derselben Quelle wie beim Export.

## Transport

**Server-Sent Events (SSE)** über `GET /api/v1/sync/events`.

- SSE statt WebSocket: Die Richtung ist nur eine (das Tool meldet, der Editor lädt nach),
  und ein gewöhnliches GET läuft durch denselben API-Schlüssel-Filter wie der Rest von
  `/api/v1` — Header `X-API-Key` oder `Authorization: Bearer`.
- Ein projektgebundener Schlüssel bekommt nur Ereignisse seines Projekts.
- Alle 15 Sekunden schickt der Server einen Kommentar (`: ping`) als Lebenszeichen.
- Ereignisse werden **gebündelt**: Wer eine Maske speichert, erzeugt mehrere Einträge —
  daraus wird eine Nachricht (Sammelfenster ~300 ms).

## Versionierung

Jede Nachricht trägt `protocolVersion` (derzeit **1**, `SyncProtocol.Version` im Code) —
analog zur `FormatVersion` des Exports: Bei jeder Format-Änderung wird sie erhöht.

**Regel für den Client:** Beim `hello` die Version prüfen. Ist sie unbekannt (größer als
die eigene), die Verbindung trennen und dem Nutzer sagen, dass das Paket neu erzeugt werden
muss — nicht raten, was neue Felder bedeuten. Felder, die ein Client nicht kennt, darf er
innerhalb derselben Version ignorieren (additive Änderungen erhöhen die Version nicht).

## Ereignisarten

### `hello` — die Eröffnung jeder Verbindung

```
event: hello
data: {"protocolVersion":1,"fullSyncRequired":true,"serverTimeUtc":"2026-08-18T12:00:00Z"}
```

`fullSyncRequired` ist immer `true`: Was zwischen Abbruch und Wiederverbinden geschah, hat
niemand aufgehoben — die Antwort auf jede neue Verbindung ist der **Voll-Abgleich** (alle
interessierenden Module einmal über die lesende API laden). Dasselbe gilt nach einem
Neustart des Tools: Die Ereignisse liegen nur im Arbeitsspeicher.

### `changes` — gebündelte Änderungen

```
event: changes
data: {"protocolVersion":1,"events":[
  {"gameProjectId":"6f9d…","moduleKey":"items","entityId":"3f2a…",
   "entityName":"Eisenschwert","action":"Updated","occurredAtUtc":"2026-08-18T12:01:30Z"},
  {"gameProjectId":"6f9d…","moduleKey":"items","entityId":"77c1…",
   "entityName":"Schild","action":"Deleted","occurredAtUtc":"2026-08-18T12:01:31Z"}
]}
```

- `action`: `Created`, `Updated` oder `Deleted` — als Text, damit das Protokoll ohne die
  Enum-Werte des Tools lesbar bleibt.
- Der empfohlene Umgang ist **je Modul**, nicht je Entität: die betroffenen `moduleKey`
  deduplizieren und je Modul die Inhaltsdatei neu laden. Das ist billiger als Einzelabrufe
  und immun gegen verpasste Ereignisse.
- **Sonderfall `moduleKey: "changelog"`**: Sammeleinträge des Tools (Import, Serien-Anlage,
  Projekt-Aktionen). Für den Editor heißen sie: Es hat sich potenziell alles geändert —
  Voll-Abgleich, wie beim `hello`.

## Verlust und Wiederanlauf

Die Warteschlange je Verbindung ist begrenzt (1000, Ältestes fällt zuerst). Ein Editor, der
nicht hinterherkommt, verliert Ereignisse — und behandelt das wie einen Neuaufbau: trennen,
neu verbinden, Voll-Abgleich. Es gibt bewusst kein „Replay ab Zeitstempel“: Der
Voll-Abgleich über die lesende API ist immer korrekt und fast immer billig genug.

## Sicherheit

- Anmeldung ausschließlich über den API-Schlüssel — kein Cookie, kein zweiter Weg.
- Der Endpunkt liefert nur Metadaten (Modul, GUID, Name, Aktion); die Inhalte selbst
  verlangen denselben Schlüssel über die lesende API.
