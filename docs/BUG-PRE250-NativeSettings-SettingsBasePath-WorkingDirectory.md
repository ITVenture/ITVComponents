# BUG (PRE250): `NativeSettings.SettingsBasePath` zeigt aufs Arbeitsverzeichnis, die Einstellungen kommen aber aus dem Anwendungsverzeichnis

> Gemeldet aus **MiniStore** (POS-Agent, WinForms-Tray, .NET 10). Quellstand geprüft:
> `cleanworkspace/ITVComponents-Public` @ `d4ae7238` („nativeSettings expose SettingsBase-Path now“).

## Übersicht

| | |
|---|---|
| **Betroffen** | `ITVComponents/Settings/Native/NativeSettings.cs`, `Init`, Z. 67–70 |
| **Kern** | Die Datei wird mit `AddJsonFile(settingsFileName, …)` und **relativem** Namen registriert. Ohne `SetBasePath` löst `Microsoft.Extensions.Configuration` das gegen `AppContext.BaseDirectory` auf. `SettingsBasePath` dagegen kommt aus `Path.GetFullPath(settingsFileName)` — also gegen das **aktuelle Arbeitsverzeichnis**. |
| **Folge** | Sobald Arbeits- und Anwendungsverzeichnis auseinanderfallen, nennt `SettingsBasePath` ein Verzeichnis, in dem die Einstellungen **nicht** liegen. Wer daneben weitere Dateien ablegt oder sucht (Schlüssel, `-params.json`, Protokolle), greift ins Leere. |
| **Wann** | Autostart/Run-Key (Arbeitsverzeichnis meist `C:\Windows\System32`), Verknüpfung mit anderem „Ausführen in“, Dienst, `dotnet run` (Arbeitsverzeichnis = Projektordner, nicht `bin/…`). |

## Der Fall

Der POS-Agent legt seinen verschlüsselten Geräteschlüssel `agent.key` neben die Einstellungen. Mit
`Path.Combine(NativeSettings.SettingsBasePath, "agent.key")` landete er bei `dotnet run` im
Projektordner, beim Autostart unter `System32` — die Einstellungen selbst kamen in beiden Fällen
richtig aus `bin/…`. Ein gekoppeltes Gerät galt danach als ungekoppelt, ohne Fehlermeldung.

```csharp
builder = new ConfigurationBuilder().AddJsonFile(settingsFileName, optional: true, reloadOnChange: true);
var tmpFull = Path.GetFullPath(settingsFileName);          // ← Arbeitsverzeichnis
SettingsBasePath = Path.GetDirectoryName(tmpFull);
```

## Vorschlag

Beides an **dieselbe** Basis binden, z. B.:

```csharp
SettingsBasePath = AppContext.BaseDirectory;
builder = new ConfigurationBuilder()
    .SetBasePath(SettingsBasePath)
    .AddJsonFile(settingsFileName, optional: true, reloadOnChange: true);
```

`SetBasePath` macht dabei explizit, was heute nur der Vorgabewert des Frameworks ist — und gilt
dann auch für Dateien, die der Host später relativ über `NativeSettings.Builder.AddJsonFile(...)`
ergänzt (`<name>-params.json`).

## Umgehung (im Host)

MiniStore benutzt vorerst `AppContext.BaseDirectory` direkt (`Program.SettingsDirectory`) und nicht
`SettingsBasePath`. Nach der Korrektur kann das wieder auf `SettingsBasePath` zeigen.
