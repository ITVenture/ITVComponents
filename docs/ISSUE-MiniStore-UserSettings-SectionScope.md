# ISSUE: `NativeSettings.AddUserSettings` wahlweise auf Abschnitte beschränken

**Status:** umgesetzt (2026-11-18) — Leitfaden §123 · **Datum:** 2026-11-18 · **Version:** 5.0.0-PRE275 (Quelle gelesen auf dem Stand PRE276) ·
**Quelle:** MiniStore, Installation des POS-Agenten pro Maschine (`ProgramData`)

## Worum es geht

`NativeSettings.AddUserSettings(file)` hängt die Benutzerdatei an die **ganze** Konfiguration. Das ist gewollt
und soll **so bleiben können**: Es gibt Hosts, die über diese Datei auch die Hauptkonfiguration anpassen.

Für den POS-Agenten ist es aber eine Lücke, sobald die Datei für mehrere Windows-Konten beschreibbar ist. Gewünscht
ist deshalb eine **wählbare** Beschränkung auf bestimmte Abschnitte. Die Vorgabe bleibt das heutige Verhalten.

## Der Fall

Der Agent wird pro Maschine nach `Program Files` installiert und läuft in jeder Sitzung ohne Administratorrechte.
Die Geräte-Einstellungen aus dem Web (Agent-Katalog, `DeviceSettingsStore.Write`) muss er selbst schreiben können.
Die Datei liegt deshalb unter `%ProgramData%\IT-Venture\MiniStoreApp\appsettings.json`, und **jeder Benutzer des
Rechners** darf dort schreiben. Das ist nötig: An einer Kasse arbeiten wechselnde Konten, und der Drucker darf
nach einem Benutzerwechsel nicht „nicht eingerichtet“ sein.

Gebraucht wird aus dieser Datei nur `ITVenture:AgentServices:Devices:<Name>`. Weil `AddUserSettings` sie aber an
die ganze Konfiguration hängt, kann ein beliebiges Konto am Laden-PC jeden Abschnitt übersteuern. Der nächste
Agent, der unter einem **anderen** Konto startet, liest diese Werte.

### Was sich darüber erreichen lässt

| Abschnitt | Gelesen von | Folge |
|---|---|---|
| `ITVenture:AssemblyResolving` (`FixedAssemblies`: `Name` → `Path`) | `ResolverConfig` (`ITVComponents/AssemblyResolving/ResolverConfig.cs:31`) | Einem Assembly-Namen wird eine beliebige DLL zugeordnet. Damit läuft fremder Code im Agenten eines anderen Kontos. |
| `ITVenture:Plugins:Constants` | `PluginConstSection` (`ITVComponents.Formatting/PluginSystemExtensions/PluginConstSection.cs:22`) | Plugin-Konstanten, die in Konstruktionszeilen eingesetzt werden. |
| Konstruktionszeilen des Hosts (bei uns `ITVenture:KassaAgent:Plugins`) | Host | Ein eigenes Plugin wird gebaut, auch das ist fremder Code. |
| Backend-Adresse des Hosts | Host | Der Agent verbindet sich mit einem fremden Server. |

Die letzten beiden Zeilen hat MiniStore selbst geschlossen (siehe Umgehung). Die ersten beiden liest das Toolkit
selbst aus `NativeSettings`, und dagegen kann der Host nichts tun.

## Betroffene Stellen

| Datei | Stelle |
|---|---|
| `ITVComponents/Settings/Native/NativeSettings.cs` | `AddUserSettings(string fileName)`: `Builder.AddJsonFile(UserSettingsFile, optional: true, reloadOnChange: true)` ohne Einschränkung |
| `ITVComponents.AgentServices/DeviceSettingsStore.cs` | liest über `NativeSettings.GetSection(path, …)`, braucht also nur `ITVenture:AgentServices:Devices` |
| `ITVComponents/AssemblyResolving/ResolverConfig.cs:31` | liest `ITVenture:AssemblyResolving` aus der zusammengeführten Konfiguration |
| `ITVComponents.Formatting/PluginSystemExtensions/PluginConstSection.cs:22` | liest `ITVenture:Plugins:Constants` aus der zusammengeführten Konfiguration |

## Vorschlag

Eine Überladung, die angibt, welche Abschnitte die Datei beisteuern darf:

```csharp
// wie heute: die Datei übersteuert alles
NativeSettings.AddUserSettings(file);

// neu: nur diese Abschnitte werden aus der Datei übernommen
NativeSettings.AddUserSettings(file, "ITVenture:AgentServices:Devices");
```

- **Ohne Abschnitte gilt das heutige Verhalten.** Wer die Hauptkonfiguration über die Benutzerdatei anpassen will,
  ändert nichts.
- **Mit Abschnitten** liefert der Provider nur Schlüssel darunter (Präfix-Vergleich auf `:`-Grenze, ohne Rücksicht
  auf Gross- und Kleinschreibung, wie `IConfiguration` selbst). Umsetzbar z.B. als schmale Hülle um den
  `JsonConfigurationProvider`, die `TryGet` und `GetChildKeys` filtert. `reloadOnChange` bleibt erhalten.
- **Verworfene Schlüssel melden:** beim Laden bzw. Neuladen eine Warnung mit der Datei und den betroffenen
  Abschnitten (nicht den Werten), einmal je Laden. Sonst sucht jemand, warum sein Eintrag nicht greift.
- **Schreiben passt dazu:** `DeviceSettingsStore.Write` schreibt ohnehin nur unter `ITVenture:AgentServices:Devices`.
  Optional kann `Write` ablehnen, wenn der Zielabschnitt nicht zu den erlaubten gehört. Dann fällt ein Fehler beim
  Einrichten auf und nicht erst beim Lesen.
- **Doku:** Im XML-Kommentar von `AddUserSettings` ein Satz, dass eine Datei, die mehr Konten schreiben dürfen als
  die Anwendung selbst, beschränkt werden sollte. Gleiches im Leitfaden beim Agent-Katalog (§72).

Denkbar wäre auch das Umgekehrte, eine Sperrliste (`AssemblyResolving`, `Plugins` …). Davon rate ich ab: Jeder
neue sicherheitsrelevante Abschnitt im Toolkit oder im Host müsste dort nachgetragen werden, und ein vergessener
fällt nicht auf.

## Umgehung in MiniStore

`MiniStore.PosAgent/Program.cs` baut eine zweite Konfiguration (`Program.TrustedConfiguration`) nur aus der Datei
neben der Anwendung und der Adresse aus der Registrierung. Daraus liest `Startup.cs` die Agent-Einstellungen
(Backend-Adresse, Konstruktionszeilen). Die zusammengeführte `NativeSettings.Configuration` benutzt nur noch der
Agent-Katalog.

**Das schützt nur, was MiniStore selbst liest.** `ITVenture:AssemblyResolving` und `ITVenture:Plugins:Constants`
liest das Toolkit weiterhin aus der zusammengeführten Konfiguration, und die bleiben offen, bis es die Überladung
gibt. Danach ruft der Agent `AddUserSettings(file, "ITVenture:AgentServices:Devices")`, und `TrustedConfiguration`
kann wieder weg.
