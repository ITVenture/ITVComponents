# ISSUE: `FileLogWriter` verschluckt Fehler beim Dateiwechsel — und ein kaputtes Ziel reisst die anderen mit

**Status:** umgesetzt (2026-11-18) — Leitfaden §122; der Fall „Öffnen scheitert“ ist nicht automatisch getestet · **Datum:** 2026-11-18 · **Version:** 5.0.0-PRE275 (Quelle gelesen auf dem Stand PRE276) ·
**Quelle:** MiniStore, Dateiprotokoll des POS-Agenten unter `ProgramData`

## Worum es geht

Der POS-Agent schreibt sein Protokoll seit heute über `FileLogWriter`
(`ITVComponents.Logging.DefaultLoggers`) in eine Datei unter `ProgramData`, mit Dateiwechsel nach Grösse
(5 MB, 10 alte Dateien). Beim Durchsehen des Codes sind zwei Stellen aufgefallen. Sie fallen im Betrieb nicht
auf, weil sie per Konstruktion keine Spur hinterlassen:

1. Scheitert der Dateiwechsel, merkt das niemand. Je nach Ursache wächst die Datei danach unbegrenzt, oder das
   Ziel wirft bei jeder Meldung.
2. Wirft ein Ziel, bekommen die Ziele **dahinter** die Meldung nicht mehr. Auch das bleibt ohne Spur.

Beobachtet ist es im Agenten noch nicht. Hergeleitet ist es aus dem Code, siehe unten.

## Der Ablauf

### 1. `FileLogWriter.Log` → `SwitchLog`

```csharp
// FileLogWriter.cs, Log(...)
if (currentSize > maxLogSize)
{
    try
    {
        SwitchLog();
    }
    catch (Exception ex)
    {
    }
}
if (innerStream == null)
{
    OpenStream();
}
WriteMessage(eventMessage);
```

`SwitchLog` schliesst den Stream, verschiebt die alten Dateien, benennt `logName` in `_0` um und öffnet im
`finally` neu. Typischer Fehler: ein Virenscanner, ein Editor oder ein Sicherungsprogramm hält die Datei bzw.
eine der alten Dateien offen, und `File.Move` / `File.Delete` wirft.

**Fall A: Umbenennen scheitert, das Wiederöffnen gelingt.** `OpenStream()` öffnet im `finally` wieder
**dieselbe** Datei und setzt `currentSize` auf deren Länge. Die liegt weiter über `maxLogSize`. Folgen:

- Jede weitere Meldung versucht den Wechsel erneut: Datei schliessen, scheitern, wieder öffnen. Das geschieht bei
  jeder Zeile, solange die Sperre besteht.
- Die Datei wächst unbegrenzt. Die Grenze, die der Betreiber gesetzt hat, gilt nicht mehr.
- Der Grund steht nirgends.

**Fall B: Auch das Wiederöffnen scheitert** (Rechte entzogen, Datenträger voll, Ordner gelöscht). Die Ausnahme
aus dem `finally` landet im leeren `catch`. `innerStream` ist geschlossen, aber **nicht `null`**. Die Prüfung
`innerStream == null` greift deshalb nicht, und `WriteMessage` schreibt in einen geschlossenen Stream:
`ObjectDisposedException`, bei jeder Meldung, aus `LogTarget.LogEvent` heraus.

### 2. `LogEnvironment.OnAllLoggers`

```csharp
try
{
    ...
    foreach (var n in targets.Where(...))
    {
        n.LogEvent(eventText, severity, context);
    }
    ... // Zonen-Ziele, temporärer Hook
}
catch
{
}
```

Der `try` liegt um die **ganze** Schleife. Wirft ein Ziel, wie im Fall B, bekommen alle späteren Ziele, die
Zonen-Ziele und der `TemporaryLoggingHook` diese Meldung nicht. Welche das sind, hängt von der Reihenfolge der
Registrierung ab. Der leere `catch` sorgt dafür, dass auch das nirgends steht.

Das betrifft jeden Host des Toolkits, nicht nur den Dateilogger: Jedes Ziel, das wirft, nimmt den anderen die
Meldung weg.

## Betroffene Stellen

| Datei | Stelle | Problem |
|---|---|---|
| `ITVComponents.Logging.DefaultLoggers/FileLogging/FileLogWriter.cs` | `Log`, `catch` um `SwitchLog()` | leer; Fehlschlag unsichtbar |
| dto. | `SwitchLog` / `finally` → `OpenStream` | nach gescheitertem Öffnen bleibt ein geschlossener Stream in `innerStream` |
| dto. | `Log`, Bedingung `currentSize > maxLogSize` | kein Abstand zwischen zwei Versuchen; neuer Versuch bei jeder Zeile |
| `ITVComponents.Logging/LogEnvironment.cs` | `OnAllLoggers`, `try`/`catch` um die Schleife | ein Ziel, das wirft, nimmt allen folgenden die Meldung; leer |

## Vorschlag

Die Kernfrage ist: wohin mit der Meldung, wenn ein Protokollziel selbst versagt? Die Antwort hängt davon ab,
wie weit es versagt:

| Lage | Wohin |
|---|---|
| Umbenennen scheitert, die alte Datei geht wieder auf (Fall A) | Eine Zeile **in diese Datei**, mit `ex.OutlineException()`. Das Ziel steht nach dem `finally` ja wieder offen bereit. Den nächsten Versuch erst nach einer Frist, z.B. am Takt des vorhandenen `streamKeeper` oder nach weiteren *n* Bytes, nicht bei jeder Meldung. |
| Auch das Öffnen scheitert (Fall B) | `innerStream = null`. Das Ziel meldet sich über `IsEnabled()` ab, bis ein erneutes Öffnen am Takt gelingt. Die übrigen Ziele protokollieren weiter. |
| Die Meldung über das Ausfallen des Ziels selbst | `Trace.TraceError`, **einmal je Ausfall**, nicht je Zeile. Nicht über `LogEnvironment.LogEvent`: das liefe auf demselben Thread zurück in dasselbe Ziel (die `Monitor`-Sperre in `LogTarget` ist wiedereintretend). |
| `OnAllLoggers` | `try`/`catch` **je Ziel** statt um die ganze Schleife. Ein Ziel, das wirft, wird per `Debug`/`Trace` gemeldet, und die anderen bekommen die Meldung trotzdem. |

Erwogen und verworfen: das Windows-Ereignisprotokoll als letzte Stufe. Es braucht eine registrierte Quelle, die
nur ein Administrator anlegen kann. Für ein allgemeines Ziel ist das zu viel, und eine Tray-Anwendung wie der
POS-Agent läuft ohne Administratorrechte.

## Umgehung in MiniStore

Keine nötig. Der Agent hält die Fehlerwahrscheinlichkeit klein: eine Datei je Windows-Konto, damit sich zwei
Sitzungen nicht sperren, und Rückfall auf eine Datei mit Prozessnummer bzw. auf `%LOCALAPPDATA%`, wenn die
Datei beim Start nicht aufgeht (`MiniStore.PosAgent/AgentLog.cs`). Gegen eine Sperre während des Betriebs
hilft das nicht. Dafür braucht es die Änderung im Toolkit.
