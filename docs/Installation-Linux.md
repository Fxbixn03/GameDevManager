# GameDevManager unter Linux installieren und einrichten

Diese Anleitung führt von einem frischen Linux-System bis zur laufenden Installation hinter
einem Reverse Proxy. Sie ist für den Fall geschrieben, für den das Tool gedacht ist: **self-hosted
von einer Person oder einem kleinen Team**, auf einem Heimserver, einer VM oder einem
Raspberry Pi.

Am Ende läuft GameDevManager als **systemd-Dienst**, startet mit dem System, liegt hinter
HTTPS und ist im Browser erreichbar.

> **Zeitaufwand:** rund 20 Minuten mit SQLite, rund 40 Minuten mit PostgreSQL und TLS.

---

## Inhalt

1. [Überblick](#1-überblick)
2. [Voraussetzungen](#2-voraussetzungen)
3. [.NET 10 installieren](#3-net-10-installieren)
4. [Quellcode holen und veröffentlichen](#4-quellcode-holen-und-veröffentlichen)
5. [Dienstbenutzer und Verzeichnisse](#5-dienstbenutzer-und-verzeichnisse)
6. [Datenbank wählen und einrichten](#6-datenbank-wählen-und-einrichten)
7. [Konfiguration schreiben](#7-konfiguration-schreiben)
8. [Erster Probelauf](#8-erster-probelauf)
9. [Als systemd-Dienst einrichten](#9-als-systemd-dienst-einrichten)
10. [Firewall](#10-firewall)
11. [Reverse Proxy und HTTPS](#11-reverse-proxy-und-https)
12. [Ersteinrichtung im Browser](#12-ersteinrichtung-im-browser)
13. [Betrieb: Aktualisieren, Sichern, Protokolle](#13-betrieb-aktualisieren-sichern-protokolle)
14. [Alle Konfigurationsschlüssel](#14-alle-konfigurationsschlüssel)
15. [Fehlersuche](#15-fehlersuche)

---

## 1. Überblick

GameDevManager ist eine **Blazor-Server-Anwendung** auf .NET 10. Das heißt für den Betrieb:

- Es ist ein normaler Webserver-Prozess, der einen Port belegt — keine Desktop-Anwendung.
- Die Oberfläche hält eine **dauerhafte WebSocket-Verbindung** zum Server. Das ist der wichtigste
  Punkt für den Reverse Proxy weiter unten: Ohne durchgereichte WebSockets bleibt die Seite weiß.
- Die Datenbank ist frei wählbar (SQLite, PostgreSQL, MySQL, SQL Server). Das Schema legt die
  Anwendung beim ersten Start selbst an.
- Hochgeladene Dateien (Sprites, Audio, Cutscenes) liegen **nicht** in der Datenbank, sondern im
  Dateisystem. Sie brauchen deshalb ein eigenes Verzeichnis und einen eigenen Platz in der Sicherung.

Das Verzeichnis-Layout, das diese Anleitung anlegt:

```
/opt/gamedevmanager/            Anwendung (aus dotnet publish)
├── GameDevManager.Web          die ausführbare Datei
├── appsettings.json            ausgelieferte Vorgaben — nicht bearbeiten
├── appsettings.Local.json      deine Einstellungen (wird auch von der Oberfläche geschrieben)
├── assets/                     hochgeladene Sprites, Audio, Videos
├── exports/                    aufbewahrte Exportstände
└── gamedevmanager.db           nur bei SQLite
```

---

## 2. Voraussetzungen

| | |
|---|---|
| **Betriebssystem** | Jede aktuelle Distribution mit systemd (Debian, Ubuntu, Fedora, Arch, openSUSE …) |
| **Architektur** | x64 oder arm64 (Raspberry Pi 4/5 mit 64-Bit-System funktioniert) |
| **Arbeitsspeicher** | 512 MB reichen für SQLite; 1 GB, wenn PostgreSQL auf derselben Maschine läuft |
| **Plattenplatz** | 1 GB für Anwendung und Runtime großzügig gerechnet, dazu was deine Assets brauchen |
| **Zugriff** | ein Konto mit `sudo` |

Für den Bau aus dem Quellcode zusätzlich `git` und das **.NET-SDK**. Wer nur betreiben will,
kann [Variante B in Schritt 4](#variante-b-eigenständig-ohne-net-auf-dem-server) nutzen und braucht
auf dem Server gar kein .NET.

---

## 3. .NET 10 installieren

Auf dem **Build-Rechner** wird das SDK gebraucht, auf dem **Server** genügt die
ASP.NET-Core-Runtime. Sind beide dieselbe Maschine, reicht das SDK — es bringt die Runtime mit.

### Arch / CachyOS / Manjaro

```bash
sudo pacman -S dotnet-sdk aspnet-runtime
```

### Debian / Ubuntu

```bash
# Microsoft-Paketquelle eintragen (einmalig)
wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O /tmp/msprod.deb
sudo dpkg -i /tmp/msprod.deb && rm /tmp/msprod.deb
sudo apt update

sudo apt install -y dotnet-sdk-10.0        # Build-Rechner
sudo apt install -y aspnetcore-runtime-10.0 # reiner Server
```

Für Ubuntu die passende Zeile der eigenen Version nehmen, etwa
`https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb`.

### Fedora / RHEL

```bash
sudo dnf install -y dotnet-sdk-10.0        # bzw. aspnetcore-runtime-10.0
```

### Jede Distribution (ohne Paketverwaltung)

Wenn die Distribution .NET 10 noch nicht anbietet, installiert das offizielle Skript es ins
Benutzerverzeichnis:

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 10.0                    # SDK
# oder nur die Runtime für den Server:
/tmp/dotnet-install.sh --channel 10.0 --runtime aspnetcore

# in die PATH-Variable aufnehmen
echo 'export DOTNET_ROOT=$HOME/.dotnet'          >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet'           >> ~/.bashrc
source ~/.bashrc
```

### Prüfen

```bash
dotnet --list-sdks       # muss eine 10.x-Zeile zeigen
dotnet --list-runtimes   # muss Microsoft.AspNetCore.App 10.x zeigen
```

> **Wichtig:** Die Anwendung zielt auf `net10.0`. Eine ältere Runtime (8.0, 9.0) startet sie nicht —
> die Fehlermeldung lautet dann sinngemäß „The framework 'Microsoft.AspNetCore.App', version
> '10.0.0' was not found".

---

## 4. Quellcode holen und veröffentlichen

```bash
git clone https://github.com/<dein-konto>/GameDevManager.git ~/src/GameDevManager
cd ~/src/GameDevManager
```

Ein kurzer Testlauf vorweg schadet nicht — er stellt sicher, dass die Toolchain vollständig ist:

```bash
dotnet test GameDevManager.slnx -c Release
```

### Variante A: Rahmenabhängig (empfohlen, wenn .NET auf dem Server liegt)

```bash
sudo mkdir -p /opt/gamedevmanager
sudo chown "$USER" /opt/gamedevmanager

dotnet publish src/GameDevManager.Web/GameDevManager.Web.csproj \
  -c Release \
  -o /opt/gamedevmanager
```

Ergebnis: das kleinere der beiden Pakete, braucht aber die ASP.NET-Core-Runtime 10 auf dem Server.

### Variante B: Eigenständig (ohne .NET auf dem Server)

```bash
dotnet publish src/GameDevManager.Web/GameDevManager.Web.csproj \
  -c Release \
  -r linux-x64 --self-contained true \
  -o /opt/gamedevmanager
```

Für den Raspberry Pi und andere ARM-Geräte `-r linux-arm64`. Das Paket ist deutlich größer, weil
die gesamte Runtime mit darin liegt — dafür läuft es auf einem Server ganz ohne .NET-Installation.
Der Bau selbst braucht weiterhin das SDK; er darf auf einer anderen Maschine stattfinden, das
Ergebnis wird dann einfach kopiert.

Wie groß es am Ende wird, sagt am schnellsten `du -sh /opt/gamedevmanager` nach dem ersten Lauf —
die vier Datenbank-Provider bringen jeweils eigene native Bibliotheken mit, das treibt beide
Varianten über das hinaus, was man bei einer Web-Anwendung erwartet.

> **Beim Aktualisieren:** Der Dienst muss vor einem erneuten `publish` gestoppt sein, sonst
> scheitert das Überschreiben der laufenden Dateien. Siehe [Schritt 13](#aktualisieren).

Nach dem Veröffentlichen prüfen, dass die ausführbare Datei da ist:

```bash
ls -l /opt/gamedevmanager/GameDevManager.Web
```

---

## 5. Dienstbenutzer und Verzeichnisse

Die Anwendung soll nicht als `root` laufen. Ein Systemkonto ohne Anmeldemöglichkeit genügt:

```bash
sudo useradd --system --no-create-home --shell /usr/sbin/nologin gamedev
```

> Auf Arch und Fedora heißt die Shell `/usr/bin/nologin`. Prüfen mit `ls /usr/sbin/nologin
> /usr/bin/nologin 2>/dev/null`.

Verzeichnisse anlegen und übereignen:

```bash
sudo mkdir -p /opt/gamedevmanager/assets /opt/gamedevmanager/exports
sudo chown -R gamedev:gamedev /opt/gamedevmanager
sudo chmod 750 /opt/gamedevmanager
```

> **Das Anwendungsverzeichnis muss für `gamedev` beschreibbar sein.** Die Anwendung legt dort
> `appsettings.Local.json` an — dorthin schreibt die Oberfläche jede Einstellung, die installationsweit
> gilt: Datenbank, Hell/Dunkel-Wahl, Sprache, Reihenfolge der Modulleiste, Passwortrichtlinie und
> das zuletzt geöffnete Projekt. Ein nur lesbares Verzeichnis lässt die Anwendung zwar starten,
> aber keine dieser Einstellungen mehr speichern.

---

## 6. Datenbank wählen und einrichten

Vier Provider stehen zur Wahl. Die Entscheidung ist nicht endgültig — der Bestand lässt sich
später über **Export und Import** in eine andere Datenbank umziehen, alle GUID-Referenzen bleiben
dabei erhalten.

| Provider | Wann sinnvoll |
|---|---|
| **SQLite** | Vorgabe. Eine Person, eine Maschine, keinerlei Einrichtung. Die Datenbank ist eine Datei. |
| **PostgreSQL** | Mehrere Personen arbeiten gleichzeitig, oder es gibt bereits einen Datenbankserver. Die robusteste Wahl. |
| **MySQL / MariaDB** | Wenn ohnehin einer läuft. |
| **SQL Server** | Wenn ohnehin einer läuft. |

### Variante A: SQLite (nichts zu tun)

Es ist nichts einzurichten. Die Datei entsteht beim ersten Start. Ein Hinweis für später: Der
Verbindungspfad wird **relativ zum Arbeitsverzeichnis des Prozesses** aufgelöst — deshalb setzt die
systemd-Unit in Schritt 9 `WorkingDirectory`, und deshalb empfiehlt sich in der Konfiguration
gleich ein absoluter Pfad.

### Variante B: PostgreSQL

```bash
# Installation (Debian/Ubuntu)
sudo apt install -y postgresql
# Arch: sudo pacman -S postgresql && sudo -u postgres initdb -D /var/lib/postgres/data
sudo systemctl enable --now postgresql
```

Datenbank und Konto anlegen — **das Passwort durch ein eigenes ersetzen**:

```bash
sudo -u postgres psql <<'SQL'
CREATE USER gamedev WITH PASSWORD 'HIER-EIN-EIGENES-PASSWORT';
CREATE DATABASE gamedevmanager OWNER gamedev;
SQL
```

Verbindung prüfen:

```bash
PGPASSWORD='HIER-EIN-EIGENES-PASSWORT' psql -h 127.0.0.1 -U gamedev -d gamedevmanager -c '\conninfo'
```

Der Connection String dazu lautet:

```
Host=127.0.0.1;Port=5432;Database=gamedevmanager;Username=gamedev;Password=HIER-EIN-EIGENES-PASSWORT
```

### Variante C: MySQL / MariaDB

```sql
CREATE DATABASE gamedevmanager CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'gamedev'@'localhost' IDENTIFIED BY 'HIER-EIN-EIGENES-PASSWORT';
GRANT ALL PRIVILEGES ON gamedevmanager.* TO 'gamedev'@'localhost';
FLUSH PRIVILEGES;
```

Connection String:

```
Server=127.0.0.1;Port=3306;Database=gamedevmanager;Uid=gamedev;Pwd=HIER-EIN-EIGENES-PASSWORT
```

### Variante D: SQL Server

```
Server=127.0.0.1;Database=GameDevManager;User Id=gamedev;Password=…;TrustServerCertificate=True
```

---

## 7. Konfiguration schreiben

Die Anwendung liest ihre Einstellungen in dieser Reihenfolge, **spätere überschreiben frühere**:

1. `appsettings.json` — die ausgelieferten Vorgaben. **Nicht bearbeiten**, sie wird beim
   Aktualisieren überschrieben.
2. `appsettings.Production.json` — falls vorhanden.
3. Umgebungsvariablen und Startparameter.
4. `appsettings.Local.json` — **hat das letzte Wort**.

Weil `appsettings.Local.json` zuletzt gelesen wird und zugleich die Datei ist, in die die
Oberfläche schreibt, gehört die eigene Konfiguration genau dorthin. Andernfalls stellt man einen
Wert per Umgebungsvariable ein und wundert sich, warum die Datei ihn wieder verdrängt.

Datei anlegen:

```bash
sudo -u gamedev tee /opt/gamedevmanager/appsettings.Local.json > /dev/null <<'JSON'
{
  "Database": {
    "Provider": "Sqlite",
    "AutoMigrate": true
  },
  "ConnectionStrings": {
    "Sqlite": "Data Source=/opt/gamedevmanager/gamedevmanager.db"
  },
  "Assets": {
    "StoragePath": "/opt/gamedevmanager/assets",
    "MaxFileSizeBytes": 20971520
  },
  "Exports": {
    "StoragePath": "/opt/gamedevmanager/exports",
    "MaxPerProject": 20,
    "MaxAgeDays": 0
  },
  "ChangeLog": {
    "MaxAgeDays": 365,
    "MaxPerProject": 0
  },
  "Ui": {
    "Language": "de"
  }
}
JSON

sudo chmod 640 /opt/gamedevmanager/appsettings.Local.json
```

**Für PostgreSQL** stattdessen den `Database`- und `ConnectionStrings`-Block so:

```json
  "Database": {
    "Provider": "PostgreSql",
    "AutoMigrate": true
  },
  "ConnectionStrings": {
    "PostgreSql": "Host=127.0.0.1;Port=5432;Database=gamedevmanager;Username=gamedev;Password=HIER-EIN-EIGENES-PASSWORT"
  },
```

Gültige Werte für `Database:Provider` sind `Sqlite`, `PostgreSql`, `MySql` und `SqlServer` —
der Schlüssel unter `ConnectionStrings` muss **genau so heißen wie der Provider**.

> Die Datei enthält ein Datenbankpasswort. `chmod 640` und der Besitz durch `gamedev` sorgen dafür,
> dass sie kein anderes Konto auf dem System lesen kann.

`AutoMigrate: true` heißt: Die Anwendung bringt das Datenbankschema bei jedem Start auf den
aktuellen Stand. Für eine self-hosted Installation ist das genau richtig — nach einem Update
entfällt jeder Handgriff an der Datenbank.

---

## 8. Erster Probelauf

Vor dem Dienst einmal von Hand starten. So sind Fehlermeldungen direkt sichtbar statt im Journal:

```bash
sudo -u gamedev env \
  ASPNETCORE_ENVIRONMENT=Production \
  ASPNETCORE_URLS=http://127.0.0.1:5000 \
  /opt/gamedevmanager/GameDevManager.Web --contentRoot /opt/gamedevmanager
```

Erwartete Ausgabe, sinngemäß:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://127.0.0.1:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

In einer zweiten Sitzung prüfen:

```bash
curl -I http://127.0.0.1:5000/
```

Eine Antwort `HTTP/1.1 200 OK` oder eine Weiterleitung auf `/konto/einrichten` bedeutet: Es läuft.
Danach mit `Strg+C` beenden.

> **Eine Warnung ist hier normal:** „Failed to determine the https port for redirect." Die
> Anwendung schaltet außerhalb der Entwicklung eine HTTPS-Weiterleitung ein; da hier nur ein
> HTTP-Port gebunden ist, findet sie kein Ziel und leitet folgerichtig nicht weiter. Sobald TLS
> über den Reverse Proxy läuft ([Schritt 11](#11-reverse-proxy-und-https)), ist das der gewollte
> Aufbau — die Warnung bleibt und ist folgenlos.

---

## 9. Als systemd-Dienst einrichten

```bash
sudo tee /etc/systemd/system/gamedevmanager.service > /dev/null <<'UNIT'
[Unit]
Description=GameDevManager
Documentation=https://github.com/Fxbixn03/GameDevManager
After=network-online.target
Wants=network-online.target
# Bei PostgreSQL/MySQL auf derselben Maschine zusätzlich:
# After=postgresql.service
# Requires=postgresql.service

[Service]
# Type=simple und nicht notify: Die Anwendung meldet dem Dienstverwalter
# keinen Bereitschaftszustand zurück.
Type=simple
User=gamedev
Group=gamedev

WorkingDirectory=/opt/gamedevmanager
ExecStart=/opt/gamedevmanager/GameDevManager.Web --contentRoot /opt/gamedevmanager

Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

Restart=on-failure
RestartSec=5
# Blazor Server hält offene Verbindungen — beim Stoppen etwas Zeit lassen.
TimeoutStopSec=30
KillSignal=SIGINT

# Absicherung. ReadWritePaths ist Pflicht: Die Anwendung schreibt
# appsettings.Local.json, assets/, exports/ und ggf. die SQLite-Datei.
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ProtectKernelTunables=true
ProtectControlGroups=true
RestrictSUIDSGID=true
ReadWritePaths=/opt/gamedevmanager

[Install]
WantedBy=multi-user.target
UNIT
```

> **`ASPNETCORE_URLS` bewusst auf `127.0.0.1`.** Damit ist die Anwendung nur lokal erreichbar und
> ausschließlich über den Reverse Proxy zugänglich. Wer **ohne** Proxy direkt im lokalen Netz
> zugreifen will, setzt hier `http://0.0.0.0:5000` — dann aber unbedingt die
> [Firewall](#10-firewall) beachten, denn ohne TLS gehen Passwörter im Klartext über das Netz.

Dienst aktivieren und starten:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now gamedevmanager
sudo systemctl status gamedevmanager
```

Protokoll live mitlesen:

```bash
sudo journalctl -u gamedevmanager -f
```

---

## 10. Firewall

**Mit Reverse Proxy** (der Normalfall) wird nur HTTP/HTTPS geöffnet — Port 5000 bleibt zu:

```bash
# ufw (Debian/Ubuntu)
sudo ufw allow 80/tcp && sudo ufw allow 443/tcp && sudo ufw enable

# firewalld (Fedora/RHEL)
sudo firewall-cmd --permanent --add-service=http --add-service=https && sudo firewall-cmd --reload
```

**Ohne Reverse Proxy**, nur im vertrauenswürdigen Heimnetz:

```bash
sudo ufw allow from 192.168.1.0/24 to any port 5000 proto tcp
```

---

## 11. Reverse Proxy und HTTPS

Der Proxy erledigt drei Dinge: TLS, einen sprechenden Namen statt einer Portnummer, und er hält
den Anwendungsprozess vom offenen Netz fern.

### Der entscheidende Punkt

GameDevManager ist eine **Blazor-Server**-Anwendung. Die Oberfläche hängt an einer dauerhaften
WebSocket-Verbindung. Ein Proxy, der WebSockets nicht durchreicht oder sie nach 60 Sekunden
Untätigkeit kappt, führt zu einer weißen Seite oder zu der Meldung „Es ist ein Fehler aufgetreten.
Diese Anwendung reagiert möglicherweise nicht mehr". Die drei Zeilen `Upgrade`, `Connection` und
ein großzügiges `proxy_read_timeout` sind deshalb keine Feinheit, sondern Voraussetzung.

### Variante A: Caddy (kürzester Weg, TLS automatisch)

```bash
sudo apt install -y caddy      # bzw. pacman -S caddy / dnf install caddy
```

`/etc/caddy/Caddyfile`:

```caddyfile
gamedev.example.com {
    reverse_proxy 127.0.0.1:5000
    request_body {
        max_size 64MB
    }
}
```

```bash
sudo systemctl reload caddy
```

Caddy reicht WebSockets von sich aus durch und besorgt das Let's-Encrypt-Zertifikat selbst.
Voraussetzung ist nur, dass der DNS-Name auf den Server zeigt und Port 80/443 erreichbar sind.

### Variante B: nginx

```bash
sudo apt install -y nginx
```

`/etc/nginx/sites-available/gamedevmanager`:

```nginx
# Die WebSocket-Aushandlung: "upgrade" nur dann, wenn der Client danach fragt.
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}

server {
    listen 80;
    server_name gamedev.example.com;

    # Uploads: muss mindestens so groß sein wie Assets:MaxFileSizeBytes (Vorgabe 20 MB).
    client_max_body_size 64M;

    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;

        # Ohne diese beiden Zeilen bleibt die Blazor-Oberfläche weiß.
        proxy_set_header   Upgrade    $http_upgrade;
        proxy_set_header   Connection $connection_upgrade;

        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;

        # Die Blazor-Verbindung ist lange still, wenn niemand tippt.
        proxy_read_timeout  3600s;
        proxy_send_timeout  3600s;
        proxy_buffering     off;
    }
}
```

Aktivieren und TLS ergänzen:

```bash
sudo ln -s /etc/nginx/sites-available/gamedevmanager /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx

sudo apt install -y certbot python3-certbot-nginx
sudo certbot --nginx -d gamedev.example.com
```

`certbot` trägt den TLS-Block selbst nach und richtet die Erneuerung ein.

> **Hinweis zum Anmelde-Cookie:** Die Anwendung setzt es mit `SecurePolicy = SameAsRequest`. Weil
> der Proxy die Verschlüsselung übernimmt und intern über HTTP weiterreicht, trägt das Cookie
> kein `Secure`-Kennzeichen. Übertragen wird es trotzdem ausschließlich über die verschlüsselte
> Strecke zum Browser. Der Aufbau ist bewusst so gewählt: Das Tool läuft oft ohne TLS im lokalen
> Netz, und ein erzwungenes `Secure` machte dort jede Anmeldung unmöglich.

---

## 12. Ersteinrichtung im Browser

`https://gamedev.example.com` aufrufen (ohne Proxy: `http://<server-ip>:5000`).

1. **Erstes Konto anlegen.** Beim allerersten Aufruf führt der Weg auf `/konto/einrichten`. Ein
   ausgeliefertes Standardpasswort gibt es bewusst nicht — das erste Konto legst du hier an, und es
   bekommt immer Verwalterrecht. Ohne das käme danach niemand an die Benutzerverwaltung.
2. **Anmelden.** Die Anmeldung sitzt direkt auf dem Startscreen, dort wo für den angemeldeten
   Benutzer der Start-Knopf steht.
3. **Projekt anlegen** unter `/projekte`. Ein leeres Startprojekt legt die Anwendung beim ersten
   Start bereits an; über den Projektumschalter in der Kopfleiste wird gewechselt.
4. **Darstellung einstellen** unter „Einstellungen → Darstellung": Sprache (Deutsch/Englisch) und
   Hell/Dunkel. Beide Angaben gelten **installationsweit**, nicht je Browser — das Tool wird von
   einer Person betrieben, ein Wert im Browserspeicher wäre am nächsten Gerät wieder weg.
5. **Weitere Benutzer** unter `/konto/benutzer`: je Konto lässt sich einstellen, ob es nur lesen
   oder auch schreiben darf, welche Module es sieht und ob Export und Import offenstehen.
   Rechteänderungen wandern ins Anmelde-Cookie und gelten deshalb **ab der nächsten Anmeldung**.
6. **Passwortrichtlinie** ebenfalls in der Benutzerverwaltung (Mindestlänge, Ziffer,
   Sonderzeichen). Sie greift nur beim Setzen **neuer** Passwörter; bestehende bleiben gültig.

Ab hier ist die Installation fertig und du kannst mit Items, NPCs und Karten anfangen.

---

## 13. Betrieb: Aktualisieren, Sichern, Protokolle

### Aktualisieren

**Vorher sichern** (siehe unten) — und der Dienst muss stehen, sonst sind die Dateien gesperrt:

```bash
cd ~/src/GameDevManager
git pull

sudo systemctl stop gamedevmanager

dotnet publish src/GameDevManager.Web/GameDevManager.Web.csproj \
  -c Release -o /opt/gamedevmanager

sudo chown -R gamedev:gamedev /opt/gamedevmanager
sudo systemctl start gamedevmanager
sudo journalctl -u gamedevmanager -n 50
```

`appsettings.Local.json`, `assets/`, `exports/` und die SQLite-Datei überlebt `publish` — es
überschreibt nur die Programmdateien und `appsettings.json`. Ausstehende Datenbankmigrationen
wendet die Anwendung beim Start selbst an (`AutoMigrate`).

### Sichern

Drei Dinge gehören in die Sicherung. Nur eines davon reicht nicht:

```bash
#!/usr/bin/env bash
# /usr/local/bin/gamedevmanager-backup.sh
set -euo pipefail
ZIEL="/var/backups/gamedevmanager/$(date +%Y-%m-%d)"
mkdir -p "$ZIEL"

# 1. Datenbank
#    SQLite — .backup statt cp, damit auch im laufenden Betrieb konsistent:
sqlite3 /opt/gamedevmanager/gamedevmanager.db ".backup '$ZIEL/gamedevmanager.db'"
#    PostgreSQL stattdessen:
# PGPASSWORD=… pg_dump -h 127.0.0.1 -U gamedev gamedevmanager | gzip > "$ZIEL/db.sql.gz"

# 2. Hochgeladene Dateien — sie liegen NICHT in der Datenbank
tar czf "$ZIEL/assets.tar.gz" -C /opt/gamedevmanager assets

# 3. Konfiguration
cp /opt/gamedevmanager/appsettings.Local.json "$ZIEL/"

find /var/backups/gamedevmanager -maxdepth 1 -type d -mtime +30 -exec rm -rf {} +
```

```bash
sudo chmod +x /usr/local/bin/gamedevmanager-backup.sh
sudo crontab -e   # Zeile: 0 3 * * * /usr/local/bin/gamedevmanager-backup.sh
```

Zusätzlich legt die Anwendung von sich aus **Exportstände** unter `exports/` an — automatisch vor
jedem ersetzenden Import und vor dem Löschen eines Projekts, dazu von Hand über die Seite
„Import & Export". Das ist das schnellere Sicherheitsnetz für „ich habe mir gerade das Projekt
zerschossen": Ein Stand ist ein vollständiges Export-ZIP und lässt sich direkt wieder einspielen.
Es ersetzt die Sicherung oben nicht, denn beides liegt auf derselben Platte.

### Protokolle

```bash
sudo journalctl -u gamedevmanager -f              # live
sudo journalctl -u gamedevmanager --since today   # heute
sudo journalctl -u gamedevmanager -p err          # nur Fehler
```

Ausführlicher protokollieren lässt sich über `Logging:LogLevel:Default` in
`appsettings.Local.json` (`"Debug"` statt `"Information"`) — danach den Dienst neu starten.

Davon zu unterscheiden ist das **Änderungsprotokoll** in der Anwendung selbst (Modul
„Änderungen"): wer wann welche Entität angelegt, geändert oder gelöscht hat. Es kürzt sich täglich
selbst auf `ChangeLog:MaxAgeDays` (Vorgabe ein Jahr).

### Dienst-Handgriffe

```bash
sudo systemctl restart gamedevmanager
sudo systemctl stop gamedevmanager
sudo systemctl disable --now gamedevmanager   # dauerhaft abschalten
```

---

## 14. Alle Konfigurationsschlüssel

Alles hier gehört nach `/opt/gamedevmanager/appsettings.Local.json`.

### Datenbank

| Schlüssel | Vorgabe | Bedeutung |
|---|---|---|
| `Database:Provider` | `Sqlite` | `Sqlite`, `PostgreSql`, `MySql` oder `SqlServer` |
| `Database:AutoMigrate` | `true` | Schema beim Start auf den aktuellen Stand bringen |
| `ConnectionStrings:<Provider>` | siehe `appsettings.json` | Der Schlüssel muss genauso heißen wie der Provider |

### Dateien

| Schlüssel | Vorgabe | Bedeutung |
|---|---|---|
| `Assets:StoragePath` | `assets` | Wurzel der hochgeladenen Dateien; relativ zum Anwendungsverzeichnis |
| `Assets:MaxFileSizeBytes` | `20971520` (20 MB) | Obergrenze je Upload — der Reverse Proxy muss mindestens so viel durchlassen |
| `Exports:StoragePath` | `exports` | Wurzel der aufbewahrten Exportstände |
| `Exports:MaxPerProject` | `20` | Wie viele Stände je Projekt bleiben; `0` = unbegrenzt |
| `Exports:MaxAgeDays` | `0` (aus) | Höchstalter eines Standes. Der jüngste bleibt in jedem Fall stehen |

### Änderungsprotokoll

| Schlüssel | Vorgabe | Bedeutung |
|---|---|---|
| `ChangeLog:MaxAgeDays` | `365` | Höchstalter eines Eintrags; `0` = unbegrenzt |
| `ChangeLog:MaxPerProject` | `0` (aus) | Obergrenze je Projekt |
| `ChangeLog:SweepHours` | `24` | Abstand zwischen zwei Wartungsläufen |

### Oberfläche und Konten

Diese Werte schreibt normalerweise die Oberfläche selbst — sie stehen hier, weil sie sich auch
vorbelegen lassen.

| Schlüssel | Vorgabe | Bedeutung |
|---|---|---|
| `Ui:Language` | `de` | `de` oder `en` |
| `Appearance:DarkMode` | — | `true`/`false` |
| `Topbar:ModuleOrder` | — | Kommagetrennte Modulschlüssel für die Reihenfolge der Modulleiste |
| `Project:CurrentId` | — | GUID des zuletzt geöffneten Projekts |
| `PasswordPolicy:MinimumLength` | `8` | |
| `PasswordPolicy:RequireDigit` | `false` | |
| `PasswordPolicy:RequireSpecialCharacter` | `false` | |
| `PasswordPolicy:PasswordsDisabled` | `false` | `true` meldet allein über den Anmeldenamen an — nur für abgeschottete Netze |

### Umgebungsvariablen (in der systemd-Unit)

| Variable | Beispiel | Bedeutung |
|---|---|---|
| `ASPNETCORE_URLS` | `http://127.0.0.1:5000` | Adresse und Port |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` schaltet die HTTPS-Weiterleitung ab und zeigt ausführliche Fehlerseiten |

---

## 15. Fehlersuche

### Der Dienst startet nicht

```bash
sudo systemctl status gamedevmanager
sudo journalctl -u gamedevmanager -n 100 --no-pager
```

| Meldung | Ursache und Abhilfe |
|---|---|
| `The framework 'Microsoft.AspNetCore.App', version '10.0.0' was not found` | Runtime fehlt oder ist zu alt. `dotnet --list-runtimes` prüfen, [Schritt 3](#3-net-10-installieren) nachholen — oder eigenständig veröffentlichen ([Variante B](#variante-b-eigenständig-ohne-net-auf-dem-server)). |
| `Permission denied` | `sudo chown -R gamedev:gamedev /opt/gamedevmanager` und `sudo chmod +x /opt/gamedevmanager/GameDevManager.Web`. |
| `Address already in use` | Der Port ist belegt: `sudo ss -tlnp \| grep 5000`. Anderen Port in `ASPNETCORE_URLS` wählen. |
| `Kein Connection-String für Provider 'X' gefunden` | Der Schlüssel unter `ConnectionStrings` heißt nicht genauso wie `Database:Provider`. Groß-/Kleinschreibung beachten: `PostgreSql`, nicht `Postgres`. |
| `Read-only file system` trotz korrekter Rechte | `ProtectSystem=strict` in der Unit ohne passendes `ReadWritePaths`. Pfad ergänzen, `sudo systemctl daemon-reload`. |

### Die Seite bleibt weiß oder „reagiert nicht mehr"

Fast immer der Reverse Proxy: Die WebSocket-Verbindung kommt nicht durch. Prüfen, dass `Upgrade`-
und `Connection`-Header gesetzt sind und `proxy_read_timeout` großzügig steht
([Schritt 11](#variante-b-nginx)). Zum Eingrenzen den Proxy umgehen und direkt auf Port 5000
zugreifen — geht es dort, liegt es sicher am Proxy.

Die Entwicklerkonsole des Browsers (F12 → Netzwerk) zeigt eine fehlschlagende Anfrage auf
`/_blazor?...&transport=WebSockets`, wenn es daran liegt.

### Uploads schlagen fehl

Zwei Grenzen müssen zusammenpassen: `Assets:MaxFileSizeBytes` in der Anwendung (Vorgabe 20 MB)
und die Obergrenze des Proxys (`client_max_body_size` bei nginx, `max_size` bei Caddy). Ist die
des Proxys kleiner, bricht der Upload mit „413 Request Entity Too Large" ab, bevor die Anwendung
ihn überhaupt sieht.

### Einstellungen werden nicht gespeichert

Die Anwendung kann `appsettings.Local.json` nicht schreiben:

```bash
ls -l /opt/gamedevmanager/appsettings.Local.json     # Besitzer muss gamedev sein
sudo -u gamedev touch /opt/gamedevmanager/appsettings.Local.json
```

Wenn `ProtectSystem=strict` aktiv ist, muss `/opt/gamedevmanager` in `ReadWritePaths` stehen.

### „Nur lesen"-Hinweis in der Kopfleiste, obwohl das Konto schreiben darf

Die Berechtigungen stehen im Anmelde-Cookie und gelten deshalb erst **ab der nächsten Anmeldung**.
Abmelden und neu anmelden.

### Datenbank umziehen (etwa SQLite → PostgreSQL)

1. In der Anwendung unter „Import & Export" das Projekt als ZIP **mit Assets** exportieren.
2. Dienst stoppen, in `appsettings.Local.json` Provider und Connection String umstellen.
3. Dienst starten — die Anwendung legt das Schema in der neuen Datenbank an.
4. Erstes Konto neu anlegen (Benutzer stehen in der Datenbank und ziehen nicht mit), dann das ZIP
   importieren.

Alle GUID-Referenzen bleiben dabei erhalten; das Änderungsprotokoll und die Werkzeug-Daten
(ToDo-Board, Whiteboards, Dashboard-Anordnung) ziehen bewusst **nicht** mit — sie gehören nicht
zum Export.

### Vollständig zurückbauen

```bash
sudo systemctl disable --now gamedevmanager
sudo rm /etc/systemd/system/gamedevmanager.service
sudo systemctl daemon-reload
sudo rm -rf /opt/gamedevmanager          # enthält Assets und Exportstände — vorher sichern!
sudo userdel gamedev
# bei PostgreSQL zusätzlich:
# sudo -u postgres psql -c 'DROP DATABASE gamedevmanager;' -c 'DROP USER gamedev;'
```

---

## Weiterführend

- [README.md](../README.md) — was das Tool fachlich kann
- [knowledge/Konzept.md](../knowledge/Konzept.md) — die fachliche Quelle der Wahrheit
- [CLAUDE.md](../CLAUDE.md) — Architektur und Entwurfsentscheidungen
