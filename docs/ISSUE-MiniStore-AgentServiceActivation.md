# Issue: Dienst-Aktivierung für Agent-Anwendungen — „der Agent sagt, was er kann, das Web wählt"

**Status:** UMGESETZT — erstmals in `5.0.0-PRE252` (`50d5e05e`), Leitfaden §72; die Kopplung kam mit PRE253
dazu (§73). Plan: `docs/Plan-Agent-ServiceActivation.md`, Entscheidungen im Abschnitt „Entscheidungen
2026-10-12" am Ende.
**Datum:** 2026-10-12
**Quelle:** MiniStore-Session (Konsument). Der POS-Agent (Tray-Anwendung am Laden-PC) stellt Bondrucker
und Schublade als Bereichs-Plugins bereit; das Web soll pro Kasse auswählen, was davon benutzt wird.
**Toolkit-Stand:** `5.0.0-PRE251`.
**Hängt zusammen mit:** `ISSUE-MiniStore-DevicePairing.md` (Kopplung, ClientKey als Dienstname),
`BUG-PRE250-FactoryWrapper-Ignores-Request-Scope.md` (Bereichs-Plugins über IPC),
`Migration-Future_10-MLM.md` §70 (Achse C, Agenten-Weg).

## Warum das ins Toolkit gehört

Achse C hat für **Zahlterminals** schon fast alles, was es dafür braucht. Für jedes andere Gerät an
einem Agenten fehlt es dagegen, und das Fehlende ist genau das, was jeder Konsument mit Agent sonst
neu erfindet:

| Baustein | Zustand |
|---|---|
| ClientApp, Kopplung, ClientKey als Dienstname am ServiceHub | fertig |
| Bereichs-Plugins über IPC erreichbar (`FactoryWrapper`, PRE251) | fertig |
| `PluginFactory.DescribeScopedPlugin`, `IDynamicLoader.GetScopedPluginNames` — beschreiben, ohne zu bauen | fertig |
| `ITerminalDevice.DescribeSettingsAsync` → `TerminalSettingDescriptor` → `DeclaredFieldsForm` | fertig, **nur für Zahlterminals** |
| `TenantPaymentTerminal` (Route = ClientKey, ProviderTerminalId = Objektname, Enabled, ConfigurationJson) | fertig, **nur für Zahlterminals** |
| `IRemoteObjectCatalog.GetObjectsAsync(clientKey, contract)` | **nur der Vertrag — keine Implementierung** |
| `ITerminalChoiceProvider` für `TerminalChoiceSources.RemoteObjects` | **muss der Host bauen**, sonst Textfeld |
| Agent-seitige Antwort auf „welche Objekte erfüllen Vertrag X" über IPC | **fehlt** |
| Zuordnung „diese Kasse benutzt dieses Objekt in dieser Rolle" für Nicht-Zahlgeräte | **fehlt** |
| Ein Feldmodell für Einstellungen | **zwei**: `TerminalSettingDescriptor` und `[ActivityParameter]` (Workflow) |

`Migration-Future_10-MLM.md:7070` hält die Richtung schon fest — „Plugins schaltet ihr über das Web
ein und aus, die Einstellungen sitzen im Web" —, aber ausserhalb der Terminals gibt es keinen
Mechanismus dahinter.

## Der Fall

Am Laden-PC läuft der POS-Agent. Seine Geräte stehen als Bereichs-Plugins in der lokalen
Konfiguration (`SettingsDrivenLoader`, `LoadType: Scope`):

```json
"Plugins": [
  { "Name": "EpsonTM", "ConstructionString": "[MiniStore.Devices.Epson.dll]<…EscPosPrinter>…", "LoadType": "Scope" },
  { "Name": "Schublade", "ConstructionString": "[MiniStore.Devices.Simulated.dll]<…SimulatedCashDrawer>", "LoadType": "Scope" }
]
```

**Die Anforderung, und warum sie anders geschnitten ist als Achse C bisher:**

1. **Die Konstruktion bleibt im Agenten.** Wer eine Konstruktionszeile pflegen darf, startet faktisch
   Code auf dem Laden-PC. Der Ladeninhaber soll das nie können — er soll sagen können „ich habe einen
   Epson-Drucker", nicht `[x.dll]<Typ>…`. MiniStore hatte das zuerst anders (Konstruktionszeilen als
   Freitext in der Web-DB, Tabelle `TerminalPlugin`) und verwirft es genau deshalb.
2. **Der Agent meldet, was er anbietet** — je Vertrag (`IReceiptPrinter`, `ICashDrawer`,
   `ITerminalDevice`), mit Anzeigename.
3. **Das Web entscheidet, was benutzt wird**: je Kasse und Rolle („Bondrucker", „Schublade") ein Angebot
   auswählen, ein-/ausschalten, und die **wenigen Einstellungen** setzen, die das Plugin selbst zulässt
   (Anschluss, Papierbreite). Die Maske dafür entsteht aus der Selbstbeschreibung des Plugins; wo ein
   Wert nur auf dem Laden-PC bekannt ist (Drucker, Ports), kommt er **als Liste vom Agenten** — niemand
   tippt einen Druckernamen oder eine Schnittstelle ab.
4. **Nach der Einrichtung ist der Agent eine Blackbox.** Die Einstellungen werden **einmal** an den
   Agenten übergeben und liegen danach **lokal** bei ihm. Zur Laufzeit spricht das Web das gewählte
   Objekt über den Proxy mit kleinen, atomaren Befehlen an — „drucke diesen Beleg", „öffne die
   Schublade", „fordere diesen Betrag an". Keine Konfiguration im Aufruf: das Plugin weiss selbst, wie
   es sein Gerät erreicht. Das hält die Fachschnittstellen klein und stabil.
5. **Zahlterminals bleiben, wie sie sind.** Ein `TenantPaymentTerminal` wird entweder direkt aus dem Web
   bedient (Stripe, Payrexx, wallee) oder über den Agenten — das muss so bleiben. Siehe unten,
   Abschnitt „Verhältnis zu `TenantPaymentTerminal`".

## Vorschlag

### 1. Angebote im Agenten: Bereichs-Plugins mit Vertrag und Anzeigename

`PluginConfigurationItem` (oder ein abgeleiteter Eintrag für Agenten) bekommt optional einen
`DisplayName`. Den Vertrag braucht es **nicht** in der Konfiguration: `DescribeScopedPlugin` löst den
Typ ohne Bau auf, und daraus lässt sich `contract.IsAssignableFrom(type)` beantworten. So kann die
Konfiguration nicht lügen.

`Disabled` bleibt, was es ist: der **lokale Notschalter** am Laden-PC („dieses Plugin gibt es hier
nicht"). Ob ein *angebotenes* Plugin benutzt wird, entscheidet das Web — das sind zwei verschiedene
Fragen, und sie dürfen zwei Schalter haben, solange klar ist, dass der lokale Vorrang hat (ein
abgeschaltetes Plugin erscheint gar nicht erst im Katalog).

### 2. Agent-seitiger Katalog über IPC

Ein Standard-Objekt, das der Agent unter festem Namen am ServiceHub bereitstellt, etwa
`IAgentObjectCatalog`:

```csharp
Task<IReadOnlyList<RemoteObjectInfo>> GetObjectsAsync(string contractTypeName);
Task<IReadOnlyList<SettingDescriptor>> DescribeSettingsAsync(string objectName);
Task<IReadOnlyList<SettingChoice>> GetChoicesAsync(string objectName, string settingName, string? draftJson);
Task<string?> GetSettingsAsync(string objectName);                      // was lokal gespeichert ist
Task<SettingsResult> ApplySettingsAsync(string objectName, string json); // validieren, lokal ablegen
```

- `GetObjectsAsync` über `IDynamicLoader.GetScopedPluginNames` + `DescribeScopedPlugin` — **ohne zu
  bauen**. Ein Plugin, das beim Bauen schon die Hardware öffnet, darf nicht deshalb anspringen, weil
  jemand im Web eine Liste aufklappt.
- `DescribeSettingsAsync` ebenfalls ohne Bau: **per Reflexion über einen Einstellungstyp**, den das
  Plugin deklariert (siehe 4). Das ist das Muster des Workflow-Aktivitätskatalogs (`[ActivityParameter]`
  per Reflexion), nicht das von `ITerminalDevice` (Instanz fragen).
- `GetChoicesAsync` ist **der eigentliche Gewinn gegenüber einer statischen Konfiguration**: die
  Auswahlwerte, die nur der Laden-PC kennt — installierte Windows-Drucker, vorhandene COM-Ports,
  USB-Geräte. Der Ladeninhaber wählt dann „EPSON TM-T20III Receipt" aus einer Liste, statt einen
  Portnamen abzutippen. (Analog zu `ChoiceSource`/`DependsOn` im bestehenden Deskriptor.)

Auf der Web-Seite: **eine Standard-Implementierung von `IRemoteObjectCatalog`**, die genau diesen
Katalog unter dem ClientKey fragt — und darauf aufbauend ein Standard-`ITerminalChoiceProvider` für
`TerminalChoiceSources.RemoteObjects`. Damit profitiert Achse C sofort: die Auswahl „Geräte-Objekt auf
dieser Kasse" wird vom Textfeld zur echten Liste, ohne dass der Host etwas baut.

### 3. Ein Feldmodell statt zwei

`TerminalSettingDescriptor` (Kind, Choices, ChoiceSource, DependsOn, StoredIn) aus dem Terminal-Paket
in ein neutrales, abhängigkeitsfreies Paket heben (agententauglich wie heute
`Billing.Terminals.Abstractions`). Der Terminal-Deskriptor wird davon abgeleitet oder bleibt als
Alias; `StoredIn` ist terminal-spezifisch und bleibt dort. `[ActivityParameter]` kann später auf
dasselbe Modell abgebildet werden — beide landen heute ohnehin in `DeclaredField`.

### 4. Einstellungen: deklariert am Plugin, getypt, nicht als freies JSON

Das Plugin deklariert einen Einstellungstyp, etwa über ein Attribut am Plugin-Typ oder ein
Marker-Interface `IConfigurableDevice<TSettings>`:

```csharp
public sealed class EscPosPrinterSettings
{
    [DeviceSetting("Drucker", ChoiceSource = "windowsPrinters")]  public string PrinterName { get; set; }
    [DeviceSetting("Papierbreite (Zeichen)")]                      public int Columns { get; set; } = 42;
}
```

- Der Katalog beschreibt daraus die Felder (Reflexion, kein Bau).
- Das Web zeigt ein generiertes Formular — nie JSON, nie eine Konstruktionszeile. JSON ist nur das
  Format auf der Leitung.
- Am Agenten kommt es getypt an: deserialisiert in `TSettings`, und **nur** in diesen Typ. Was dort kein
  Feld hat, existiert nicht — das ist die Grenze, die verhindert, dass über die Einstellungen doch
  wieder Konstruktion hereinkommt.

**Ablage: lokal im Agenten, nicht im Web — und nicht mit jedem Aufruf.** Das ist eine bewusste
Abweichung von „der Agent hält nichts" (§70.4), und sie ist gewollt:

- `ApplySettingsAsync` validiert gegen `TSettings` und legt die Werte **auf dem Laden-PC** ab, je
  Objektname (naheliegend über `NativeSettings` im Benutzer- bzw. Geräteverzeichnis, das seit PRE251
  einen verlässlichen `SettingsBasePath` hat).
- Zur Laufzeit baut die Fabrik das Bereichs-Plugin, und das Plugin bekommt **seine** Einstellungen aus
  dieser Ablage — etwa über einen Konstruktor-Parameter, den ein agent-seitiger Helfer je Objektname
  auflöst (`$deviceSettings` o. ä.), oder über `IConfigurableDevice<TSettings>`. Die Fachaufrufe tragen
  **nur das Was**: `PrintAsync(beleg)`, `OpenDrawerAsync()`, `StartPaymentAsync(betrag)`.
- Gewinn: kleine, stabile Fachschnittstellen; der Agent ist nach der Einrichtung eine Blackbox; ein
  Aufruf, der unterwegs verloren geht, kann keinen halben Konfigurationsstand hinterlassen.
- Preis: die Einstellungen stehen nicht in der Web-DB. Die Maske liest sie über `GetSettingsAsync` vom
  Agenten — ist der offline, zeigt sie „nicht erreichbar" statt veralteter Werte. Das ist ehrlicher
  als ein Spiegel, der still vom Gerät abweicht.

*Bitte prüfen:* wie ein Bereichs-Plugin am saubersten an seine lokal abgelegten Einstellungen kommt.
customVariables an `LoadPlugin` werden für `$`-Parameter bewusst **nicht** herangezogen — das hat uns
gerade einen Startabbruch `plugins (->plugins) not found!` beschert. Eine Fehlermeldung, die in dem
Fall sagt „ist als customVariable vorhanden, wird für $-Parameter aber nicht gelesen —
`RegisterObject` verwenden", hätte die Suche abgekürzt.

### 5. Generische Zuordnung je ClientApp

Eine Tabelle im Systemkontext, analog `TenantPaymentTerminal`, aber ohne Zahlungsbezug — Arbeitstitel
`ClientAppServiceBinding`:

| Spalte | Bedeutung |
|---|---|
| `ClientAppId` | die Kasse (der Agent) |
| `Slot` | die Rolle, vom Host deklariert: `receiptPrinter`, `cashDrawer` |
| `ObjectName` | das gewählte Angebot im Agenten — **unter diesem Namen entsteht der Proxy** |
| `Enabled` | benutzen ja/nein; abgeschaltet statt gelöscht |
| `LastSeenUtc` | letzter erfolgreicher Kontakt |

**Keine Einstellungen in dieser Tabelle** (siehe 4) — sie hält nur fest, *welches* Objekt für *welche*
Rolle angesprochen wird. Das Web löst daraus `IInjectableProxy<TContract>` auf (Dienst = ClientKey,
Objekt = `ObjectName`) und ruft direkt die Fachmethode.

Der Host deklariert seine Slots beim Registrieren (`AddAgentServiceSlot<IReceiptPrinter>("receiptPrinter", "Bondrucker")`),
die Verwaltungsmaske hängt sich an die ClientApp-Detailansicht: je Slot eine Auswahl aus dem
Katalog (gefiltert auf den Vertrag des Slots), darunter das generierte Einstellungsformular.

Ob ein Slot mehrere Bindungen tragen darf (zwei Drucker an einer Kasse) — Vorschlag: eindeutig je
`(ClientAppId, Slot)`; wer zwei braucht, deklariert zwei Slots.

## Verhältnis zu `TenantPaymentTerminal` — nebeneinander, nicht ineinander

`TenantPaymentTerminal` soll **nicht** in `ClientAppServiceBinding` aufgehen. Ein Zahlterminal ist
ein Zahlungsgegenstand mit eigener Identität (Provider + ProviderTerminalId, eindeutig über
Mandantengrenzen), Verkäufe verweisen darauf, und die meisten Wege laufen gar nicht über einen
Agenten. Das soll so bleiben.

Was die beiden teilen, ist **nur der untere Teil**:

```
TenantPaymentTerminal (Provider = agent)         ClientAppServiceBinding (Slot = receiptPrinter)
   Route = ClientKey ──────────┐                   ClientAppId ──────────┐
   ProviderTerminalId = Objekt │                   ObjectName = Objekt   │
                               ▼                                         ▼
                      Agent-Katalog (2) + Feldmodell (3) + Einstellungs-Zustellung (4)
```

- Beide zeigen auf ein **Angebot im selben Katalog** und beschreiben ihre Einstellungen mit **demselben
  Feldmodell**.
- **Wo die Einstellungen eines Agent-Terminals liegen, muss die Toolkit-Seite entscheiden.** Heute reisen
  sie als `TerminalTarget.ConfigurationJson` mit jedem Aufruf. Mit Punkt 4 könnten sie auch lokal im
  Agenten liegen, und `ConfigurationJson` bliebe für den Agenten-Weg leer. Für die Cloud-Wege (Stripe,
  Payrexx, wallee) ändert sich nichts — dort gibt es keinen Agenten, der etwas halten könnte. Wünschenswert
  wäre, dass ein Agent-Terminal **beides** kann: lokal eingerichtet (Normalfall) oder mitgeschickt
  (Kompatibilität zum heutigen Stand).
- **Kein Fremdschlüssel** zwischen den Tabellen, und **kein gemeinsamer `Enabled`**: das `Enabled` des
  Terminals regelt, ob darüber kassiert wird; das der Bindung, ob der Slot bedient wird. Zwei Fragen,
  zwei Schalter.
- `AgentTerminalPaymentService` bleibt, nutzt aber den Standard-Katalog (Punkt 2) für
  `RemoteObjects` und dessen `DescribeSettingsAsync`, statt das Gerät selbst zu instanziieren und zu
  fragen. Dadurch beantwortet sich „was braucht dieses Terminal" für Agent-Terminals und andere
  Agent-Geräte gleich.
- Lesend darf die ClientApp-Ansicht zeigen, dass ein Zahlterminal auf diese Kasse zeigt — als Hinweis,
  nicht als Pflegestelle. Die Pflege bleibt in `TerminalsAdmin`.

## Was MiniStore bis dahin tut

- Der Agent behält seine lokalen Angebote (`SettingsDrivenLoader`). Heute wählt das Web per
  `ConfigureAsync` nur den Namen (`PrinterName`/`DrawerName`), und eine Fassade (`DeviceSet`) leitet
  weiter. Das bleibt als Zwischenstufe; mit diesem Baustein spricht das Web die Geräte-Objekte direkt
  über ihren Proxy an, und die Fassade fällt weg.
- `PosTerminal` wird ein Verweis auf die ClientApp mit wenigen fachlichen Zusatzangaben (Name, Ort für
  den Bon). Die Tabelle `TerminalPlugin` (Konstruktionszeilen im Web) wird entfernt.
- Keine eigene Zuordnungstabelle in MiniStore, um nicht zu bauen, was hier vorgeschlagen ist.

## Offene Fragen an die Toolkit-Seite

1. Gehört `ClientAppServiceBinding` in den Sicherheitskontext (neben ClientApp) oder in ein eigenes
   WebPart mit eigenem Kontext-Vertrag (`IAgentServiceContext`)? Für Konsumenten ohne Agent sollte es
   keine Tabelle erzwingen.
2. Fester Objektname des Agent-Katalogs am ServiceHub, oder vom Agenten bei der Anmeldung mitgeteilt?
3. `LastSeenUtc`: aus der An-/Abmeldung am ServiceHub (dann für ClientApp, Bindung und Terminal aus
   einer Quelle) oder je erfolgreichem Aufruf?

## Entscheidungen 2026-10-12 (Toolkit-Seite)

Umsetzung: `docs/Plan-Agent-ServiceActivation.md`.

**Angenommen wie vorgeschlagen:** Konstruktion bleibt im Agenten; der Agent meldet seine Angebote je
Vertrag; das Web wählt je Kasse und Rolle; Einstellungen getypt und lokal im Agenten; Zahlterminals und
Zuordnung nebeneinander ohne FK und ohne gemeinsames `Enabled`.

**Verworfen:** eine Fehlermeldung, die bei einem unbekannten `$`-Parameter auf eine gleichnamige
customVariable hinweist. Plugins und customVariables sind zwei getrennte Namensräume; Namensgleichheit
heisst nicht, dass das eine mit dem anderen zu tun hat.

**Zu §4 „Bitte prüfen" (wie kommt das Plugin an seine Einstellungen):** über den Konstruktor, ohne
customVariables. Konstruktionszeile `…$DeviceSettings`; der Agent-Helfer löst den Parameter über
`PluginFactory.UnknownConstructorParameter` je **anforderndem** Plugin auf (dessen Name = Objektname)
und deserialisiert in dessen deklarierten Einstellungstyp. Wird vor dem Bau verifiziert (Scope,
reflect-only, Pufferung).

**Zu §2 (Katalog):** Der Kern existiert schon als `PluginActivityCatalog` (Bereichs-Plugins,
reflection-only, IPC). Er wird nach `ITVComponents.Plugins` gezogen und von beiden Katalogen benutzt.
Ein Gerät ist **nie** sein eigener Auswahl-Provider — sonst würde das Aufklappen einer Liste die
Hardware öffnen.

| Frage | Entscheidung |
|---|---|
| Neuinstallation des Laden-PCs | wird neu eingerichtet; **kein** Spiegel der Einstellungen im Web |
| Einstellungen eines Agent-Zahlterminals | **nur lokal** im Agenten; `ConfigurationJson` wird auf dem Agenten-Weg nicht mehr gelesen (breaking, Migrationsleitfaden) |
| Mehrere gleiche Geräte an einer Kasse | eindeutig je `(ClientAppId, Slot)`; zwei Drucker = zwei Slots |
| Offene Frage 1 — wohin die Zuordnung | eigenes WebPart; `IAgentServiceContext` erbt `IBaseTenantContext` (Mandant) **und** ein neues schmales `IClientAppContext` → echter FK auf die ClientApp. `ISecurityContext` erbt `IClientAppContext` ebenfalls; `IBaseTenantContext` bleibt unverändert. Schnitt über schmale Interfaces (`IClientAppCore`), nicht über das Kappen von Navigationen |
| Offene Frage 2 — Name des Katalogs | nullbares Feld `CatalogObjectName` am `ClientAppTemplate`; leer = feste Konstante. Vom Admin setzbar, nichts auszuhandeln |
| Offene Frage 3 — `LastSeenUtc` | aus An-/Abmeldung am ServiceHub, **eine** Spalte an der `ClientApp` (Broker bekommt dafür Ereignisse) |
