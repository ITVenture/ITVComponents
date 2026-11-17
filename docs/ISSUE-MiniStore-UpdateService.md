# ISSUE: Update-Dienst für Client-Anwendungen (Windows-Dienst mit IPC zur Anwendung)

**Status:** in Arbeit, Plan der Toolkit-Seite am Ende · **Datum:** 2026-11-17 · **Version:** 5.0.0-PRE275 ·
**Quelle:** MiniStore, Setup und Verteilung des POS-Agenten

## Worum es geht

Der MiniStore-POS-Agent läuft auf dem Laden-PC: eine Tray-Anwendung (WinForms, WebView2-Vollbild für die
Selbstbedienungs-Station), gekoppelt über `ITVComponents.AgentServices` und den gRPC-Hub. Er wird künftig
**pro Maschine** installiert, als MSI aus dem WiX Toolset:
- nach `Program Files`, mit Administratorrechten;
- Gerätedaten unter `ProgramData`, die Rechte setzt der Installer;
- Autostart über `HKLM\…\Run`, also in jeder Sitzung.

Damit fehlt der Weg für **automatische Updates**: Ein normaler Benutzer darf `Program Files` nicht ändern. Agent
und Web hängen aber eng zusammen (Hub-Vertrag, Bon-Format, Geräte-Verträge), und ein Laden spielt kein Update
von Hand ein. Ohne automatische Updates laufen draussen nach wenigen Releases mehrere Fassungen nebeneinander.

Die Lösung ist ein **Update-Dienst**: ein Windows-Dienst unter `LocalSystem` neben der Anwendung, nach dem
Muster von Chrome, Edge und anderen. Wir möchten seine **Grundkomponente im Toolkit**, nicht im MiniStore. Ein
künftiges Projekt mit einer Client-Anwendung am Arbeitsplatz hat dasselbe Problem, und der heikle Teil ist
überall gleich: Code mit Systemrechten, der aus dem Netz nachlädt.

Dieses Dokument beschreibt die **Anforderungen**. Ausarbeiten und entscheiden soll die Toolkit-Seite.

## Die zwei Aufgaben des Dienstes

### A. Updates holen und installieren

1. **Prüfen**, ob eine neuere Fassung bereitliegt: beim Start und danach in einem konfigurierbaren Abstand, dazu
   auf Anstoss (siehe IPC).
2. **Herunterladen und prüfen**, bevor irgendetwas ausgeführt wird (siehe „Sicherheit“).
3. **Die Anwendung beenden** lassen, geordnet über IPC (siehe B), mit Frist.
4. **Installieren**, für ein MSI still mit `msiexec /i … /qn` und Protokoll. Die Rollback-Logik von MSI (Major
   Upgrade) trägt den Fehlerfall. Der Dienst soll selbst nichts zurückkopieren.
5. **Die Anwendung wieder starten**, in der Sitzung, in der sie vorher lief (siehe „Offene Fragen“).
6. **Sich selbst aktualisieren**: Der Dienst ist Teil desselben MSI oder eines eigenen. In beiden Fällen darf er
   sich nicht selbst den Boden wegziehen, etwa durch einen Zwischenschritt über einen Hilfsprozess.

### B. Lokale IPC-Verbindung für die Anwendung

Der Dienst bietet einen **lokalen** Kanal an, auf den sich die Anwendung verbindet, zum Beispiel eine Named Pipe.
Darüber laufen:

| Zweck | Richtung | Bemerkung |
|---|---|---|
| **Anmelden / Präsenz** | App → Dienst | Die App meldet sich mit Sitzungs-Id, Prozess-Id und Fassung an. Die Verbindung hält, solange die App läuft. |
| **Läuft schon ein Agent?** | App → Dienst | Antwort mit Sitzung und Fassung der laufenden Instanz. Das ersetzt einen globalen Mutex: Bei schnellem Benutzerwechsel laufen sonst zwei Agenten mit demselben Geräteschlüssel, und das Backend lässt nur eine aktive Instanz je ClientApp zu. Die App kann dann sauber sagen „läuft schon unter Benutzer X“ und sich beenden. |
| **Aufforderung zum Beenden** | Dienst → App | Als Ereignis, mit Grund (Update, Benutzerwechsel, Administrator). Die App quittiert und beendet sich geordnet. |
| **Freigeben / übernehmen** | Dienst → App | Bei einem Sitzungswechsel trennt sich der Agent der inaktiven Sitzung vom Hub und gibt die Geräte frei; der Agent der aktiven übernimmt (siehe „Stand der Klärung“, 6). |
| **„Jetzt nicht“** | App → Dienst | Die App darf ein Beenden **aufschieben**, mit einer Frist. An einer Kasse läuft vielleicht gerade eine Kartenzahlung oder ein Druck. Der Dienst versucht es später wieder. Nach einer Höchstdauer gilt eine konfigurierbare Regel (erzwingen oder bis zum nächsten Fenster warten). |
| **Update-Stand abfragen** | App → Dienst | Verfügbar, wird geladen, wartet auf Beenden, installiert, Fehler mit Grund. Die Tray-Anwendung zeigt das an. |
| **Jetzt prüfen** | App → Dienst | Der Anstoss aus dem Tray-Menü oder aus dem Backend, das über die App eine neue Fassung meldet. |

Was über den Kanal **nicht** gehen darf: dem Dienst eine Adresse, einen Pfad, eine Datei oder ein Kommando
vorgeben. Der Dienst läuft mit Systemrechten. Was er installiert, bestimmt allein seine eigene Konfiguration und
die Prüfung des Pakets, nie der Aufrufer.

## Sicherheit

Das ist der Teil, der ein gemeinsames Toolkit-Stück rechtfertigt:

- **Die Signatur prüfen, nicht nur die Prüfsumme.** Authenticode des Pakets gegen einen in der Konfiguration
  des Dienstes festgelegten Herausgeber, über Fingerabdruck oder Zertifikatskette plus Subject. Eine Prüfsumme
  aus demselben Feed schützt nur gegen Übertragungsfehler, nicht gegen einen kompromittierten Feed.
- **Feed nur über HTTPS.**
- **Den Download geschützt ablegen.** Ein Verzeichnis, in das nur `SYSTEM` und Administratoren schreiben dürfen.
  Danach im selben Verzeichnis prüfen und ausführen. Sonst bleibt zwischen Prüfen und Ausführen ein Fenster, in
  dem ein Benutzer die Datei austauscht.
- **Die Pipe mit ACL.** Lokale, angemeldete Benutzer dürfen verbinden, kein Netzwerkzugriff
  (`PipeOptions.CurrentUserOnly` passt nicht, weil der Dienst unter `SYSTEM` läuft). Eine Nachricht ist nur ein
  Wunsch, nie ein Befehl mit Parametern (siehe oben).
- **Kein Downgrade**, ausser ausdrücklich in der Konfiguration erlaubt, zum Beispiel für einen Rückzug.

## Woher die Fassung kommt

Gewünscht ist ein **einfacher HTTP-Feed**: ein Manifest mit Fassung, Paket-URL, Grösse und Hash, dazu optional
Kanal, Mindestfassung und Freigabedatum. Das MiniStore-Web soll ihn selbst ausliefern können, als statische
Dateien oder als Endpunkt. Dann holt jeder Laden-PC die Fassung, die zu seinem Server passt.

Wünschenswert, aber nicht Bedingung: Das Backend kennt die Fassung jeder gekoppelten Instanz (über den Hub, eine
Instanz meldet ihre Fassung ohnehin an) und kann eine **Mindestfassung** vorgeben. Ist die App älter, stösst sie
über IPC „jetzt prüfen“ an.

## Was der Dienst generisch halten muss

- **Kein MiniStore-Wissen.** Produktname, Feed, erwarteter Herausgeber, Name der Pipe, Installationskommando
  und Prüfabstand kommen aus der Konfiguration bzw. aus der Registrierung durch das MSI.
- **Mehrere Produkte auf einer Maschine:** entweder ein Dienst je Produkt (einfach) oder einer für mehrere
  (sparsam). Das soll die Toolkit-Seite entscheiden. Für MiniStore genügt einer je Produkt.
- **Eine Client-Bibliothek für die App:** verbinden, anmelden, Ereignisse empfangen („Beenden angefordert“,
  „Update-Stand geändert“), aufschieben, abfragen. Sie soll sich in eine WinForms-Tray-Anwendung einfügen, ohne
  dass die App das Protokoll kennt. Läuft kein Dienst (Entwicklung, `dotnet run`), arbeitet die App ohne ihn
  weiter und meldet das einmal im Log.
- **Protokoll:** jede Entscheidung des Dienstes (geprüft, nichts Neues, geladen, Signatur abgelehnt, App
  verweigert Beenden, installiert, Exit-Code von `msiexec`) im Log, mit dem im Toolkit üblichen
  Mechanismus. Ein stiller Fehlschlag wäre hier besonders teuer, denn der Laden-PC ist weit weg.
- **Das Dienst-Gerüst wird nicht neu gebaut.** Als Service-Schicht dient das bestehende Projekt
  `ITVComponents.GenericService`. Der Update-Dienst ist darin eine weitere Ausprägung: Plugins und
  Konfiguration wie bei den bestehenden Diensten. Anschauungsbeispiele liegen unter `C:\dv\bkb-services`, z. B.
  `AppTaskScheduler`, `FlowJobWorkerService` und `ServiceController`. Sie laufen noch auf .NET 6/8 und auf
  älteren ITVComponents-Fassungen, im Grundsatz stimmt der Aufbau aber noch. Was dort für den Update-Dienst
  fehlt (Sitzungswechsel über `OnSessionChange`, Wiederherstellungsaktionen, losgelöster Start von `msiexec`),
  wird im GenericService ergänzt und nicht daneben gebaut.

## Stand der Klärung (2026-11-17)

### 1. Start der App in der Benutzersitzung: geht, ohne Neustart des Systems

Der Dienst läuft in Sitzung 0, darf aber als `LocalSystem` in eine andere Sitzung starten:
`WTSEnumerateSessions` (bzw. die Sitzung, in der die App angemeldet war) → `WTSQueryUserToken` (braucht
`SeTcbPrivilege`, das hat `LocalSystem`) → `DuplicateTokenEx` → `CreateEnvironmentBlock` →
`CreateProcessAsUser` mit Desktop `winsta0\default`. Das ist das übliche Verfahren der Update-Dienste von
Browsern. Es geht auch in eine **gesperrte** Sitzung, die App läuft dann hinter dem Sperrbildschirm. Ist niemand
angemeldet, gibt es nichts zu starten, und der Autostart (`HKLM\…\Run`) übernimmt beim nächsten Anmelden.

Die Alternative ohne P/Invoke ist eine geplante Aufgabe mit Prinzipal „Benutzer, nur wenn angemeldet“, die der
Dienst bei Bedarf auslöst. Die Aufgabe ist sichtbar und lässt sich von einem Administrator abschalten. Für die
Toolkit-Seite zu entscheiden; die Anforderung ist nur: **Nach einem Update läuft der Agent ohne Zutun wieder
in der Sitzung, in der er vorher lief.**

Damit braucht auch der Dringlichkeitsfall (siehe 2) keinen Neustart des Systems: Beenden lassen, nach Frist
`TerminateProcess`, installieren, neu starten.

### 2. Wann installiert wird

| Fall | Verhalten |
|---|---|
| **Kein Agent verbunden** (niemand angemeldet, App noch nicht gestartet) | sofort installieren |
| **Der letzte Agent hat sich getrennt** (Abmelden, App beendet) | sofort installieren |
| **Agent verbunden, normales Update** | Der Dienst meldet „Update bereit“. Die App beendet sich, sobald sie **untätig** ist (keine offene Zahlung, kein Druck, keine Station mit Kunde), und meldet das als geordnetes Beenden. Der Dienst installiert und startet sie neu (1). |
| **Dringend** (Kennzeichen im Feed) | Aufforderung zum Beenden mit kurzer Frist. Die App darf nur noch abschliessen, was läuft. Nach der Frist beendet der Dienst den Prozess. Dann installiert er und startet die App neu. |

Ohne die dritte Zeile käme ein normales Update an einer Kasse praktisch nie an: Mit dem Autostart ist dort
immer ein Agent verbunden, solange jemand angemeldet ist.

### 3. Ein MSI für App und Dienst

`ServiceInstall`/`ServiceControl` im selben Paket. Daraus folgt für die Selbstaktualisierung: `msiexec` hält
beim Installieren den Dienst an, der es gestartet hat. Das geht gut, weil die Installation selbst im Windows
Installer-Dienst läuft und nicht in unserem Prozess. Der Dienst darf aber **nicht auf das Ergebnis warten**:
- vor dem Start von `msiexec` den Zustand „Installation läuft, Fassung X, Protokoll Y“ auf die Platte schreiben;
- `msiexec` losgelöst starten;
- nach dem Neustart (`ServiceControl Start="install"`) das Protokoll auswerten;
- erst dann die App neu starten.

Bei einem Fehlschlag rollt MSI zurück, und der alte Dienst kommt wieder hoch und findet denselben Zustand vor.

### 4. Format des Feeds: Vorschlag

**Ein eigenes, kleines JSON-Manifest unter fester Adresse**, statisch auslieferbar:

```json
{
  "product": "MiniStore.PosAgent",
  "channel": "stable",
  "latest": {
    "version": "1.4.2",
    "url": "packages/MiniStore.PosAgent-1.4.2.msi",
    "sha256": "…",
    "size": 48213504,
    "published": "2026-11-17T08:00:00Z",
    "urgent": false,
    "minimumVersion": "1.3.0"
  }
}
```

- `url` relativ zum Manifest. So lässt sich der ganze Ordner auf einen anderen Server legen.
- `urgent` ist der Dringlichkeits-Schalter aus 2.
- `minimumVersion`: Wer älter ist, bekommt das Update in jedem Fall als dringend.
- `sha256` nur gegen Übertragungsfehler. Die Sicherheit trägt die Signatur.
- Je Kanal eine Datei (`stable.json`, `beta.json`). Der Kanal steht in der Konfiguration des Dienstes.

Erwogen und verworfen:

| Alternative | Warum nicht |
|---|---|
| NuGet-Feed (MSI im Paket, v3 Flat Container) | Die Infrastruktur gäbe es schon. Ein Laden-PC bräuchte dann aber Zugang zu einem Entwickler-Feed, und Kennzeichen wie `urgent` hätten keinen natürlichen Platz. |
| Squirrel/Velopack `RELEASES` | Auf deren Paketformat zugeschnitten, nicht auf MSI. |
| WinGet-Manifest | Setzt WinGet auf dem Zielrechner und ein Repository voraus. |

### 5. Authentifizierung am Feed: keine

Die Signatur genügt. Das Paket ist öffentlich unbedenklich, und der Feed enthält keine Daten. Was ein
manipulierter Feed noch erreichen kann: ein **gültig signiertes** Paket erzwingen, wegen „kein Downgrade“
höchstens die neueste Fassung, und mit `urgent` einen Neustart der App. Das ist hinnehmbar.

### 6. Übergabe zwischen Sitzungen: ja, gesteuert vom Dienst

Der Dienst sieht jeden Sitzungswechsel (`SERVICE_CONTROL_SESSIONCHANGE`, in .NET `ServiceBase.OnSessionChange`
mit `CanHandleSessionChangeEvent`):
- Anmelden und Abmelden;
- Sperren und Entsperren;
- Konsole verbunden und getrennt;
- Remote verbunden und getrennt.

Die App kann dasselbe auch selbst erkennen, über `SystemEvents.SessionSwitch` bzw.
`WTSRegisterSessionNotification`; ein Tray hat die nötige Nachrichtenschleife. Der Dienst sollte aber
**entscheiden**, denn nur er sieht beide Sitzungen.

Die Regel:
- **Sperren allein ist kein Grund.** Eine gesperrte Kasse ohne andere Sitzung bleibt verbunden, und der Bon der
  letzten Zahlung soll trotzdem kommen.
- **Wird eine andere Sitzung interaktiv**, über Anmelden, Entsperren oder Konsole verbunden: Der Agent der
  bisherigen Sitzung bekommt „freigeben“. Er trennt sich vom Hub und gibt die Geräte frei, denn ein offener
  COM-Port oder eine USB-Verbindung blockierte sonst den Agenten der neuen Sitzung. Danach wartet er.
- **Der Dienst startet den Agenten in der neuen Sitzung** bzw. weckt ihn dort.
- **Wird die alte Sitzung wieder interaktiv**, wechselt es zurück.

„Warten“ statt „beenden“: Der Agent bleibt mit dem Dienst verbunden, aber nicht mit dem Hub. Das spart den
Neustart beim Zurückwechseln, denn ein Entsperren ist kein Anmelden, und `HKLM\…\Run` würde ihn nicht wieder
starten.

Dazu gehört in die IPC-Tabelle oben das Ereignis **„freigeben / übernehmen“** (Dienst → App).

### 7. Weitere Entscheide

- **Eine aktive Instanz, beliebig viele wartende.** Sind zwei Sitzungen gleichzeitig interaktiv (Konsole und
  RDP), hat die **Konsole Vorrang**.
- **Aufschieben ist auf eine Woche begrenzt.** Die Frist zählt ab dem Erscheinen der Fassung. Danach gilt das
  Update als dringend. Die Woche ist die Vorgabe und eine **Einstellung des Dienstes**.
- **Der Stand wird über den Agenten gemeldet.** Der Agent kennt seine ClientApp und ist mit dem Hub verbunden.
  Er meldet seine Fassung und den Stand des Dienstes, den er über IPC kennt: aktuell, Update bereit,
  aufgeschoben seit, fehlgeschlagen mit Grund. Die ClientApp-Seite zeigt das je Instanz, z. B. „Kasse 1 aktuell,
  Kasse 2 ausstehend seit 3 Tagen“. Der Dienst selbst spricht nicht mit dem Backend.

### 8. Die Auswertung nach der Installation ist eine Strategie

Wie ein Ergebnis zu lesen ist, hängt vom Paketformat ab. Gewünscht ist dafür eine austauschbare Strategie
(`IInstallResultEvaluator` o. ä.), zuerst für MSI. Bei MSI heissen die Exit-Codes:

| Exit-Code | Bedeutung |
|---|---|
| `0`, `1641`, `3010` | Erfolg. `3010` heisst „Neustart nötig“ und gehört als eigener Stand gemeldet. |
| `1602` | Abbruch |
| `1603` | Fehler |
| `1618` | Eine andere Installation läuft gerade, also später nochmals versuchen. |

Dazu kommt das Protokoll für den Grund. Ein späteres Projekt mit EXE-Setup oder anderem Format bindet eine
eigene Strategie an. Ebenso austauschbar sollte der Aufruf der Installation selbst sein (Kommandozeile).

**Entscheidend: Der Dienst läuft nach der Installation wieder, in jedem Ausgang.**

| Ausgang | Was den Dienst wieder startet |
|---|---|
| Erfolg | `ServiceControl Start="install"` im MSI |
| Rollback | Die Rücknahme des Anhaltens startet den alten Dienst wieder. Das gehört getestet, nicht angenommen. |
| Als Sicherheitsnetz | Wiederherstellungsaktionen des Dienstes („bei Fehler neu starten“, WiX `ServiceConfig`/`util:ServiceConfig`) und Starttyp automatisch, damit spätestens ein Neustart ihn zurückbringt |

Ein Test „Installation bricht mitten drin ab“ gehört in die Abnahme.

## Vorschlag: Feed und Pakete im Toolkit statt als Dateien

Statt die Manifeste von Hand zu pflegen, verwaltet das Toolkit die Freigaben. Das Manifest aus 4 bleibt das
**Leitungsformat** zum Dienst; erzeugt wird es aus der Datenbank.

**Datenmodell**, als optionale Erweiterung des Basis-Kontexts nach dem Muster von `IBillingContext`: Ein Host,
der das Interface nicht implementiert, bekommt keine Tabellen. Ein eigener Kontext wäre auch möglich, aber das
Produkt hängt an den ClientApp-Vorlagen, und die liegen im Basis-Kontext.

| Tabelle | Inhalt |
|---|---|
| Produkt | gehört zu einer ClientApp-Vorlage (Typ-Schlüssel); ein Produkt ist das, was ein Dienst aktualisiert |
| Freigabe | Fassung, Kanal, Zeitpunkt, `urgent`, `minimumVersion`, zurückgezogen ja/nein, Hash, Grösse, Verweis auf die Ablage |

**Ablage über eine Strategie** (`IUpdatePackageStore` o. ä.), mit zwei Ausprägungen:

1. **Blob in der Datenbank.**
   - **Nur gestreamt, nie als `byte[]`.** Ein self-contained Agent mit MSI hat 70–100 MB. Zehn Kassen, die
     gleichzeitig laden, wären sonst ein Gigabyte im Speicher des Webservers. Mit EF heisst das: der Inhalt
     nicht als Eigenschaft der Entität, sondern in Blöcken (z. B. 1 MB je Zeile) oder über
     `GetStream`/`CommandBehavior.SequentialAccess` direkt von Npgsql.
   - **Aufräumen:** Jede Freigabe vergrössert jedes Backup. Es braucht eine Regel wie „die letzten N Fassungen
     je Kanal behalten“.
2. **Datei auf einem beschreibbaren Volume**, unter einem konfigurierten Pfad. Das ist einfacher und günstiger.
   Es setzt voraus, dass das Volume den Neustart eines Containers überlebt und bei mehreren Instanzen geteilt
   ist.

Später sind weitere Ausprägungen denkbar (S3, Azure Blob), ohne dass sich am Rest etwas ändert.

**Ausliefern über einen File-Handler**, anonym:
- Das genügt, weil die Signatur die Sicherheit trägt (5).
- **HTTP-Range** sollte er können, damit ein abgebrochener Download über eine schwache Ladenleitung
  weiterläuft. Bei der Datei ist das trivial, beim Blob in Blöcken machbar.
- Dazu kommen das Manifest je Produkt und Kanal und ein `ETag`/`Last-Modified`, damit die regelmässige Prüfung
  ohne Neuigkeit fast nichts kostet.

**Hochladen über eine Maske**, mit eigenem Recht:
- Ob die Fassung im Paket steckt, lässt sich auf einem Linux-Server nicht ohne Weiteres prüfen. Die
  `ProductVersion` steckt in der MSI-Datenbank, einer OLE-Verbunddatei, und die Authenticode-Prüfung eines MSI
  braucht unter Linux eigene Arbeit.
- **Vorschlag:** Die Fassung wird beim Hochladen eingegeben. Der Server prüft nur Hash und Grösse. **Massgeblich
  bleibt die Prüfung im Dienst.** Ein Paket mit falscher Fassung oder ohne Signatur kommt dort nicht durch,
  höchstens bleibt ein Laden auf dem alten Stand. Das zeigt die Statusanzeige aus 7.
- Kann das Toolkit die `ProductVersion` doch lesen, umso besser. Das ist aber keine Bedingung.

**Was die Maske sonst bietet:**
- Freigaben je Produkt und Kanal;
- `urgent` setzen und eine Freigabe zurückziehen;
- Beim **Zurückziehen** zeigt das Manifest wieder die vorherige Fassung. Das ist kein Downgrade: Wer schon
  aktualisiert hat, bleibt auf der neuen Fassung.
- daneben die Übersicht aus 7: welche Instanz auf welcher Fassung steht.

## Was MiniStore selbst übernimmt (zur Abgrenzung)

- das WiX-Projekt für den Agenten, pro Maschine, self-contained;
- den Umzug der Gerätedaten (`agent.key`, `kiosk.dat`, Geräte-Einstellungen) nach `ProgramData`;
- den Autostart über `HKLM\…\Run`;
- die Code-Signatur;
- die Prüfung der WebView2-Runtime;
- das Einrichten von Produkt und Ablage im MiniStore-Web und das Hochladen der Freigaben (der Feed selbst
  kommt aus dem Toolkit, siehe „Vorschlag“);
- das Melden von Fassung und Update-Stand aus dem Agenten (der Meldeweg kommt aus dem Toolkit);
- die Einbindung der Client-Bibliothek in den Tray: Anzeige, Aufschieben während Zahlung oder Druck, „läuft
  schon“.

## Plan der Toolkit-Seite (Stand 2026-11-17)

Dieser Abschnitt hält fest, was die Toolkit-Seite entschieden und schon gebaut hat. Wo er den Abschnitten oben
widerspricht, gilt er.

### P1. Lokaler Kanal: gebaut (`f56cf145c`, Leitfaden §110)

Die Anwendung spricht den Dienst über die ITVComponents-IPC an, nicht über ein eigenes Protokoll. Der Transport
ist das neue Paket `ITVComponents.InterProcessCommunication.NamedPipe`.

- **Die Pipe:**
  - Die ACL lässt angemeldete Konten zu, gesperrt sind Netzwerkzugriffe und neue Instanzen.
  - Der Dienst öffnet die Pipe exklusiv.
  - Die Anwendung kann prüfen, dass die Pipe LocalSystem gehört (`ExpectedServerOwner = S-1-5-18`).
- **Identität:** Der Dienst liest sie beim Betriebssystem, nie aus der Nachricht: Benutzer, Gruppen, Sitzung,
  Prozess-Id und Programmpfad. Damit sind Sitzungs-Id und PID aus der Tabelle in B nicht mehr Teil der Anmeldung,
  der Dienst kennt sie selbst.
- **Angebot des Dienstes:** Er bietet eine schmale Fassade an. Öffentlich ist nur, was die Anwendung tun darf.
  Argumente sind nur Grundtypen und angemeldete Typen.
- **Ereignisse** (Beenden, freigeben/übernehmen, Stand geändert) kommen über den Rückkanal der IPC.

### P2. Feed: eine Paketliste

Ein Manifest je Kanal (`stable.json`, `beta.json`) listet alle Pakete eines Produkts. Das Grundpaket ist ein
Eintrag darin.

```json
{
  "channel": "stable",
  "generated": "2026-11-17T08:00:00Z",
  "packages": [
    {
      "id": "MiniStore.PosAgent", "kind": "base", "version": "1.4.2",
      "url": "packages/MiniStore.PosAgent-1.4.2.msi", "sha256": "…", "size": 48213504,
      "published": "…", "urgent": false, "minimumVersion": "1.3.0"
    },
    {
      "id": "MiniStore.Printer.EpsonTM", "kind": "module", "version": "2.0.1",
      "url": "packages/MiniStore.Printer.EpsonTM-2.0.1.zip", "sha256": "…", "size": 812345,
      "published": "…", "requires": { "MiniStore.PosAgent": "[1.4.0,2.0.0)" }
    }
  ]
}
```

- **`kind`** bestimmt die Installations-Strategie: `base` installiert per `msiexec` (Auswertung wie in 8),
  `module` per Entpacken und Umschalten (P4).
- **`requires`** gibt einen Fassungsbereich in NuGet-Schreibweise an. Passt er nicht zur installierten Fassung,
  wird das Paket nicht installiert, und der Stand meldet den Grund.
- **`sha256` ist Pflicht.** Ohne passenden Hash wird nichts installiert.
- `url` ist relativ zum Manifest. `urgent` und `minimumVersion` gelten wie in 2 und 4.

### P3. Signatur des Feeds: optional, Hash zwingend

- Das Web signiert das Manifest byte-genau so, wie es ausgeliefert wird. Die Signatur liegt daneben
  (`stable.json.sig`). Es gibt keine Kanonisierung, und alles bleibt statisch auslieferbar.
- **Der Schlüssel** ist ein Zertifikat, das in der Web-Anwendung hinterlegt wird.
  - Es kommt als PFX aus der Konfiguration, das Passwort aus den Secrets, und es liegt hinter `IFeedSigner`.
    Später kann es so auch aus einem Key Vault kommen.
  - Es liegt nicht in der Datenbank, sonst steckte es in jedem Backup.
- **Der Dienst** kennt die Fingerabdrücke des öffentlichen Zertifikats. Er nimmt zwei an, damit sich das
  Zertifikat ohne Unterbruch wechseln lässt.
- **Einstellung „Signatur verlangen“:** Ist sie an, lehnt der Dienst ein unsigniertes oder falsch signiertes
  Manifest ab. Ist sie aus, hängt die Echtheit der Module allein an HTTPS und am Webserver. Module haben keine
  Authenticode-Signatur, das Manifest ist ihre einzige Bindung. Für den Laden einschalten, für die Entwicklung
  ausschalten.
- Das MSI des Grundpakets prüft der Dienst immer zusätzlich per Authenticode, wie unter „Sicherheit“ beschrieben.

### P4. Zusatzpakete (Module)

Ein Modul ist eine Erweiterung des Agenten: Plugin-Assemblies, ihre Abhängigkeiten und ihr Eintrag im Agent-Katalog
(§72). Es installiert nichts am System. Die Windows-Installation verantwortet der Kunde. Der Dienst hütet nur das
Grundpaket und die Module.

- **Ablage:** `Program Files\<Produkt>\modules\<id>\`, schreibbar nur für SYSTEM. So kann kein Benutzer Code
  unterschieben, der danach im Agenten läuft.
  - Der Pfad enthält keine Fassung. Damit bleiben die Konstruktionszeilen über Updates hinweg gleich.
  - Die Fassung steht im `module.json` des Ordners, zusammen mit Id und Haupt-Assembly. Der Dienst schreibt und
    prüft es.
- **Inhalt:** Das Modul wird als Plugin-Projekt gebaut (`EnableDynamicLoading`). Seine `.deps.json` beschreibt alle
  Abhängigkeiten, auch native. Eine eigene Liste braucht es nicht.
- **Installation:**
  - Herunterladen und den Hash prüfen.
  - Nach `modules\<id>.new\` entpacken. Kein Eintrag darf aus dem Zielordner herausführen (Zip-Slip).
  - Umbenennen, sobald der Agent beendet ist, denn er hält die Dateien geladener Assemblies offen. Der Agent startet
    nie mit einem halb geschriebenen Modul.
- **Wirksam** wird ein Modul mit dem nächsten Start des Agenten: untätig beenden lassen und neu starten, wie in 2.
  Es braucht keinen Neustart des Systems.
- **Buchführung:** Was installiert ist, führt der Dienst selbst in `ProgramData`. Deinstallieren entfernt den
  Ordner des Moduls und sonst nichts (P5).
- **Konfiguration:** Das Paket bringt seinen Eintrag für den Agent-Katalog mit. Die Assemblies nennt er immer mit
  Pfad relativ zum Installationsordner (`[modules\<id>\X.dll]<Typ>`). Ein blosser Dateiname könnte mit einem
  anderen Modul kollidieren und wird mit Warnung im Log abgelehnt.
- **Zuweisung:**
  - Im Web wird je ClientApp festgelegt, welche Module sie bekommt, z. B. über die Vorlage plus Zusätze je Kasse.
  - Der Agent reicht die Liste der Paket-Ids an den Dienst weiter. Über die Pipe gehen nur Ids, nie Pfade oder
    URLs, wie oben unter B gefordert.
  - Der Dienst nimmt die Liste nur vom Agenten an, erkennbar an dessen Programmpfad im Installationsordner.
  - Er gleicht ab: Fehlt ein zugewiesenes Modul, wird es installiert. Ist ein installiertes Modul nicht mehr
    zugewiesen, wird es entfernt.
- **Stand je Paket:** Der Agent meldet ihn wie in 7, z. B. „Kasse 2: Grundpaket aktuell, EpsonTM fehlgeschlagen
  (Hash stimmt nicht)“.

### P5. Abhängigkeiten von Modulen: ein Ladekontext je Modul

Module bringen eigene Abhängigkeiten mit, und mehrere Module können dieselbe Assembly brauchen, auch in
verschiedenen Fassungen. Jedes Modul wird deshalb in einem eigenen `AssemblyLoadContext` geladen. Darin lädt es
seine Abhängigkeiten in der Fassung, die es mitbringt. Jeder Modulordner ist vollständig. Es gibt keine geteilten
Dateien, keine Referenzzählung und keine Konflikte zwischen Fassungen. Deinstallieren heisst Ordner löschen.

- **PluginFactory:** Eine austauschbare Wahl des Ladekontexts, `IAssemblyContextSelector`
  (`ContextFor(assemblyPath)`).
  - Ohne Selektor arbeitet die Factory wie bisher im Standard-Kontext. Für bestehende Hosts ändert sich nichts.
  - Der Agent bekommt einen `ModuleContextSelector`: Liegt eine Assembly unter `modules\<id>\`, kommt sie in den
    Kontext dieses Moduls, je Modul genau einer. Alles andere kommt in den Standard-Kontext. Der Ort entscheidet,
    nicht ein Eintrag in der Konfiguration, und den Ort kann nur der Dienst beschreiben.
  - Die Abhängigkeiten einer Modul-Assembly löst die Runtime über deren Kontext auf. Die Factory muss davon nichts
    wissen.
  - Lade-Scopes und Transient-Plugins sind nicht betroffen: Sie regeln die Lebensdauer von Instanzen, nicht das
    Laden von Assemblies.
- **`ModuleLoadContext`** löst über den `AssemblyDependencyResolver` der Haupt-Assembly auf und lädt native DLLs
  aus dem Modulordner. Er ist nicht entladbar, Module werden ohnehin mit dem Neustart des Agenten wirksam.
- **Verträge kommen immer aus dem Standard-Kontext:** ITVComponents, Plugins, die Agent-Abstraktionen und die
  Geräte-Schnittstellen. Der Modul-Kontext gibt für sie nichts zurück. Würde ein Modul eine eigene Kopie laden,
  wären seine Typen für die Runtime andere als die des Agenten (`InvalidCastException`). Ein Modul, das
  Vertrags-Assemblies mitbringt, lehnt der Dienst ab.
- **Was über die IPC geht, ist in Vertrags-Assemblies definiert, nie in einem Modul.** Die IPC löst Typnamen im
  Standard-Kontext auf, ein Typ aus einem Modul wäre dort nicht zu finden. Das Web kennt die Module ohnehin nicht.
  Die Bereitstellung der Dienste ans Web-Backend ändert sich damit nicht.
- **Restrisiko native DLLs:** Windows lädt eine native DLL je Prozess nach ihrem Namen. Der Modul-Kontext lädt sie
  über den vollen Pfad, ihre eigenen Abhängigkeiten löst Windows aber nach dem Namen auf. Zwei Module mit
  gleichnamigen nativen DLLs in verschiedenen Fassungen können sich deshalb weiterhin stören.
- **Zu bestätigen zu Beginn von Phase 4:** Lädt die Factory die Modul-Assembly über den Selektor, sollten die
  Konstruktionszeilen der Module ohne weitere Änderung funktionieren. Das wird zuerst mit einem Test geprüft.

### Anmerkungen MiniStore zum Plan (2026-11-17)

**M1. Ein Update des Grundpakets darf kein zugewiesenes Modul verlieren.**

Der Fall: Das Grundpaket geht von 1.x auf 2.0, und ein Modul trägt `requires: [1.4.0,2.0.0)`. Nach P2 wird das
Modul dann nicht installiert, der Agent startet aber trotzdem neu. An der Kasse fehlt danach der Bondrucker.

Gewünscht:
- Ein Grundpaket wird nur installiert, wenn für **jedes zugewiesene Modul** eine Fassung im Feed liegt, deren
  `requires` die neue Fassung einschliesst.
- Diese Module kommen im **selben Durchgang** mit. Der Agent startet erst, wenn Grundpaket und Module stehen.
- Fehlt eine passende Modulfassung, bleibt das Grundpaket stehen. Der Stand nennt den Grund, z. B. „wartet auf
  EpsonTM für 2.x“.
- `urgent` und die Wochenfrist überstimmen das **nicht**. Eine Kasse, die nicht mehr druckt, ist schlechter als
  eine veraltete.

**M2. Woran erkennt der Dienst eine Vertrags-Assembly (P5)?**

P5 lässt den Dienst ein Modul ablehnen, das Vertrags-Assemblies mitbringt. Offen ist, woran er sie erkennt.

Vorschlag: **Was im Installationsordner des Grundpakets liegt, gilt als Vertrag.** Das braucht keine Liste, die
man pflegen muss, und es stimmt mit jeder Fassung des Grundpakets von selbst.

Dazu gehört eine Bauanleitung im Leitfaden: Im Modul-Projekt tragen die Verweise auf Vertragsprojekte und
-pakete `<Private>false</Private>` bzw. `ExcludeAssets="runtime"`. So gelangen sie gar nicht erst ins Paket, und
die Ablehnung trifft nur den Fehlerfall.

**M3. Rückweg für ein Modul.**

Beim Grundpaket rollt MSI zurück, beim Modul fehlt bisher ein Rückweg.

Vorschlag:
- Beim Umbenennen den bisherigen Ordner als `modules\<id>.old\` behalten.
- Gelöscht wird er erst, wenn der Agent nach dem Start meldet, dass das Modul geladen ist.
- Meldet der Agent einen Ladefehler oder stürzt er beim Start ab, schaltet der Dienst zurück und meldet die neue
  Fassung als fehlgeschlagen. Er versucht sie erst mit einer neueren Fassung im Feed wieder.

Wünschenswert, keine Bedingung.

### Antworten der Toolkit-Seite zu M1–M3 (2026-11-17)

**Zu M1: einverstanden, umgesetzt in Phase 4, mit zwei Ergänzungen.**

- Der Dienst plant einen **Durchgang** statt eines Pakets: das Grundpaket, dazu für jedes zugewiesene Modul die
  höchste Fassung, deren `requires` die neue Grundfassung einschliesst. Fehlt eine solche Fassung, bleibt das
  Grundpaket stehen, mit dem Stand „wartet auf <Modul> für <Fassung>“. `urgent` und die Wochenfrist überstimmen das
  nicht.
- **Reihenfolge:** Alle Module des Durchgangs werden heruntergeladen und geprüft, **bevor** `msiexec` startet.
  1. Das MSI installiert das Grundpaket.
  2. Der Dienst startet neu, entpackt die Module und startet erst dann den Agenten.

  Danach kann nur noch etwas am Dateisystem scheitern. Ein Grundpaket zurückdrehen kann der Dienst nicht, MSI rollt
  nur eine gescheiterte Installation zurück. Den Rest deckt M3 ab.
- **Folge:** Eine dringende Korrektur am Grundpaket kann an einem fehlenden Modul hängen bleiben. Das Web fängt das
  beim Freigeben ab (Phase 5): Gibt jemand eine Grundfassung frei, für die ein zugewiesenes Modul keine passende
  Fassung hat, warnt die Maske und nennt die betroffenen Kassen.

**Zu M2: die Bauanleitung ja, die Regel „was im Installationsordner liegt“ nein.**

- Im Installationsordner liegen auch fremde Bibliotheken des Agenten (`Newtonsoft.Json`, `System.IO.Ports` …). Gälte
  alles dort als Vertrag, dürfte kein Modul eine eigene Fassung davon mitbringen. Genau dafür ist der Ladekontext je
  Modul aber da (P5).
- **Stattdessen:** Vertrags-Assemblies tragen ein Assembly-Attribut (`[assembly: AgentContract]`), ITVComponents
  und die Agent-Abstraktionen. Das pflegt sich so selbst wie der Vorschlag, ohne Fehlgriffe. Dienst und Ladekontext
  lesen es aus den Metadaten, ohne die Assembly zu laden.
- Die Bauanleitung (`<Private>false</Private>` bzw. `ExcludeAssets="runtime"` für Verweise auf Verträge) kommt in
  den Leitfaden.

**Zu M3: einverstanden, umgesetzt in Phase 4.**

- Beim Umschalten bleibt `modules\<id>.old\` stehen.
- Der Agent meldet nach dem Start über die Fassade, welche Module er geladen hat und welche nicht.
- **Als Absturz** gilt: Der Agent trennt sich innerhalb einer Frist nach dem Start, ohne diese Meldung geschickt
  zu haben.
- Bei einem Ladefehler oder Absturz schaltet der Dienst zurück und merkt sich die neue Fassung als
  fehlgeschlagen, wie beim Grundpaket. Er versucht sie erst mit einer neueren Fassung wieder.
- Zusammen mit M1 deckt das den Rest-Fall ab, dass nach einem Grundpaket-Update ein Modul nicht lädt.

### P6. Phasen

| Phase | Inhalt |
|---|---|
| 1 | Dienst auf GenericService:<br>- Sitzungswechsel über eine eigene `WindowsServiceLifetime`<br>- Fehler beim Start ins Log statt auf die Konsole<br>- Feed als Paketliste, zunächst nur das Grundpaket; Hash zwingend<br>- Authenticode-Prüfung des MSI, dazu `UpgradeCode` und `ProductVersion` gegen Konfiguration und Manifest<br>- Installation losgelöst, mit Zustandsdatei und `IInstallResultEvaluator`<br>- Neustart der Anwendung in ihrer Sitzung<br>- Fassade über die Pipe: Anmelden, Stand, Beenden, Aufschieben, Jetzt prüfen<br>- Client-Bibliothek für die Anwendung |
| 2 | Übergabe zwischen Sitzungen, „läuft schon“, Konsole hat Vorrang |
| 3 | Signatur des Feeds (P3): `IFeedSigner` im Web, Prüfung im Dienst |
| 4 | Module (P4, P5): Paketart `module`, `IAssemblyContextSelector` in der PluginFactory, `ModuleLoadContext` im Agenten, Abgleich der Zuweisung |
| 5 | Web: Pakete und Freigaben verwalten, Manifest-Endpunkt mit `ETag`, Zuweisung der Module je ClientApp, Übersicht des Stands je Instanz |
