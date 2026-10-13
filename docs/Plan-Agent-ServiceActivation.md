# Plan: Dienst-Aktivierung für Agent-Anwendungen

**Stand:** GEBAUT 2026-10-12 (nicht committet, am Host nicht gelaufen) — Leitfaden §72.

**Abweichungen vom Entwurf, beim Bau entschieden:**
- **B4 per Code-Lektüre bestätigt:** Bereichs-Plugins baut die Hauptfabrik (`RequestScopePlugin` →
  `this.LoadPlugin`), ihr `UnknownConstructorParameter` greift also auch im Scope; `DescribePlugin` wertet
  keine Konstruktor-Parameter aus; Scope-Instanzen leben je Scope. Getestet in `AgentServices.Test`.
- **Neu: `IAgentClientFactory`** (Host): `IInjectableProxy<T>` ist je Typ fest verdrahtet, ein Agent wird
  aber erst zur Laufzeit über seinen `ClientKey` gewählt. Darüber liegt `IAgentConnector` (Abstractions),
  den auch Billing nutzt — damit liefert das Toolkit jetzt einen `ITerminalAgentLocator` mit.
- **Fremdschlüssel ohne Navigation** an `ClientAppServiceBinding` (statt `ClientAppServiceBinding<TClientApp>`):
  echte FKs, aber kein generischer Entitätstyp; die Dienste brauchen nur `IAgentServiceBindingContext`.
- **Der Terminal-Assistent** übergibt im zweiten Schritt das ganze Gerät (`TerminalDefinition`) statt nur
  des JSON — vorher fehlte dort die Route, also die Kasse. `DeclaredFieldsForm` hat dafür `FieldChanged`
  bekommen, damit die Objekt-Auswahl der gewählten Kasse folgt.
- **Die Maske** ist ein Reiter in der ClientApp-Detailzeile (`AddClientAppDetailTab`) — dort werden die
  Geräte zugewiesen und endgültig eingerichtet. Lokalisiert (de/en/fr/it, `AgentServiceMessages`) wie die
  übrigen ClientApp-Masken; das Template trägt nur den Katalognamen.
- **Nachbesserung 2026-10-13:** die Einstellungen liegen in den nativen Einstellungen statt in einer Datei je
  Gerät (siehe B, „Ablage"); `JsonUserSettings` dabei repariert und um `SetSubSection` ergänzt.
- **Nebenfund:** ein unbekanntes Parameter-Kennzeichen in einer Konstruktionszeile (`<Typ>($x)`) ergab
  still `null` — jetzt mit Warnung.
- Nicht gebaut: der lesende Hinweis „Zahlterminals an dieser Kasse" im Rollen-Reiter (das Agent-Paket
  kennt Billing nicht).
**Zweig:** Future_10, Toolkit-Stand `5.0.0-PRE251`
**Anstoss:** `docs/ISSUE-MiniStore-AgentServiceActivation.md` (Entscheidungen dort im Abschnitt
„Entscheidungen 2026-10-12")
**Zwei Teile:** **B** — Katalog am Agenten, Feldmodell, lokale Einstellungen, Zahlterminal-Umbau.
**C** — Zuordnung je Kasse (`ClientAppServiceBinding`), Kontext-Schnitt, Masken.
**Nicht in diesem Plan:** `[ActivityParameter]` auf das neue Feldmodell abbilden; Auswahl-Quellen für
Windows-Drucker/COM-Ports als Toolkit-Lieferung (siehe B5); Hinweise in Fehlermeldungen, die
customVariables und `$`-Parameter in Beziehung setzen (verworfen — zwei getrennte Namensräume).

---

## 1. Ausgangslage (im Code geprüft)

| Baustein | Stand | Fundstelle |
|---|---|---|
| Bereichs-Plugins aufzählen, Typ ohne Bau auflösen | fertig | `IDynamicLoader.GetScopedPluginNames`, `PluginFactory.TryGetPluginType`, `PluginFactory.DescribeScopedPlugin` (`PluginFactory.cs:1552`) |
| Katalog über Bereichs-Plugins, reflection-only, per IPC | **fertig, aber nur für Aktivitäten** | `PluginActivityCatalog` + `WorkflowActivityCatalogClient` |
| Auswahlwerte über benannten Provider im eigenen Scope | fertig (Aktivitäten) | `PluginActivityCatalog.GetValidValues` |
| `IRemoteObjectCatalog` | nur Vertrag | `WebCoreToolkit.InterProcessExtensions/IRemoteObjectCatalog.cs` |
| `ITerminalChoiceProvider` für `RemoteObjects` | Host muss bauen | `BillingViews/Extensions/DependencyInjectionExtensions.cs:76` |
| `ITerminalAgentLocator` | nur Vertrag, keine Umsetzung | `EntityFramework.Billing/Abstractions/TerminalAbstractions.cs:298` |
| Einstellungen eines Agent-Terminals | **baut das Gerät**, um es zu fragen | `AgentTerminalPaymentService.DescribeDeviceSettingsAsync` |
| Pro-Anforderer-Auflösung eines Konstruktor-Parameters | fertig | `UnknownConstructorParameterEventArgs.ResolvedName` + `callingType` (`PluginFactory.cs:1938ff`) |
| An-/Abmeldung am Hub | **keine Ereignisse** | `EndPointBroker.RegisterService` / `UnRegisterService` / Timeout-Drop |
| `ClientApps` im Vertrag | nur `ISecurityContext` (`ISecurityContext.cs:175`) | — |
| Konkrete Kontexte | **alle** implementieren `ISecurityContext` | `SecurityContext`, `AspNetSecurityContext`, Tree-Kontexte |

**Der wichtigste Befund:** Der vom Issue gewünschte Agent-Katalog ist zu grossen Teilen schon gebaut —
als `PluginActivityCatalog`. Er wird **nicht** verallgemeinert (ein Agent soll kein Workflow-Paket
referenzieren), aber sein Kern wandert nach `ITVComponents.Plugins` und wird von beiden benutzt.

---

## 2. Entscheidungen (verbindlich, aus der Abstimmung)

1. **Konstruktion bleibt im Agenten.** Das Web sieht nie eine Konstruktionszeile.
2. **Katalog fragt, ohne zu bauen** — weder beim Aufzählen noch beim Beschreiben. Ein Gerät darf
   **nicht** sein eigener Auswahl-Provider sein (der Rückfall in `GetValidValues`, der das Plugin
   selbst baut, entfällt für Geräte).
3. **Einstellungen getypt und lokal im Agenten.** Kein Spiegel im Web. Neuinstallation = neu
   einrichten.
4. **Agent-Zahlterminals: nur lokal.** `ConfigurationJson` wird auf dem Agenten-Weg nicht mehr gelesen
   (**breaking**). Cloud-Wege unverändert.
5. **Katalogname:** nullbares Feld `CatalogObjectName` am `ClientAppTemplate`; leer = feste Konstante.
6. **`LastSeenUtc`:** aus An-/Abmeldung am Hub, **eine** Spalte an der `ClientApp`.
7. **Zuordnung:** eindeutig je `(ClientAppId, Slot)`; zwei gleiche Geräte = zwei Slots.
8. **Kontext:** eigenes WebPart; `IAgentServiceContext` erbt `IBaseTenantContext` (Mandant) **und** ein
   neues schmales `IClientAppContext` (ClientApp mit echtem Fremdschlüssel). `IBaseTenantContext`
   selbst bleibt unverändert.
9. **`TenantPaymentTerminal` und Bindung nebeneinander**, kein FK, kein gemeinsames `Enabled`.

---

## 3. Paketschnitt

| Paket (Arbeitstitel) | Abhängigkeiten | Inhalt |
|---|---|---|
| `ITVComponents.AgentServices.Abstractions` | **keine** (agent- und webtauglich) | Feldmodell, `[DeviceSetting]`, `IHasDeviceSettings<T>`, `IAgentObjectCatalog`, DTOs, `AgentCatalog.DefaultObjectName` |
| `ITVComponents.AgentServices` | `ITVComponents.Plugins`, Settings | `PluginAgentObjectCatalog`, lokale Ablage, Einstellungs-Auflösung, `IDeviceChoiceProvider` |
| `ITVComponents.WebCoreToolkit.EntityFramework.AgentServices` | TenantSecurity (Shared), InterProcessExtensions | `IAgentServiceContext`, `ClientAppServiceBinding`, Slots, `AgentRemoteObjectCatalog`, Bindungs-Auflöser, Hub-Beobachter |
| `ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AgentServiceViews` | obiges + MudBlazor | Slot-Maske in der ClientApp-Ansicht, Einstellungs-Dialog |

**Alle neuen Projekte in die `.sln`** — wer fehlt, wird still nie gebaut und nie publiziert.

`Billing.Terminals` referenziert danach `AgentServices.Abstractions` (beide abhängigkeitsfrei).

---

## Teil B — Katalog, Feldmodell, lokale Einstellungen

### B1. Gemeinsamer Kern in `ITVComponents.Plugins`

Aus `PluginActivityCatalog.Activities()` wird eine öffentliche Hilfe:

```csharp
// PluginFactory (oder Extension in ITVComponents.Plugins)
IEnumerable<(PluginConfigurationItem Item, Type Type)> GetScopedPluginTypes(Type contract);
```

- Läuft über alle `IDynamicLoader` der Factory, `TryGetPluginType`, `contract.IsAssignableFrom(type)`.
- `Disabled`-Einträge erscheinen **nicht** (lokaler Notschalter hat Vorrang). *Prüfen:* liefern die
  Loader abgeschaltete Einträge heute überhaupt aus? Falls ja, hier filtern.
- Fehler eines Loaders: loggen (`LogEnvironment`, Warning, mit `OutlineException`), weiter mit dem nächsten.
- `PluginActivityCatalog` stellt darauf um (reine Umstellung, Tests `PluginActivityCatalogTest` müssen
  unverändert grün bleiben).

### B2. Feldmodell (`AgentServices.Abstractions`)

```csharp
public enum SettingKind { Text, MultilineText, Number, Boolean, Date, Choice, DateTime }  // Werte = TerminalSettingKind
public class SettingDescriptor { Name, Label, Kind, Required, HelpText, DefaultValue,
                                 IReadOnlyList<SettingChoice>? Choices, string? ChoiceSource, string? DependsOn }
public class SettingChoice     { Value, Label }
```

- `TerminalSettingDescriptor : SettingDescriptor` + `StoredIn` (bleibt terminal-spezifisch).
- `TerminalSettingKind` / `TerminalSettingChoice` entfallen zugunsten der neutralen Typen
  (**breaking**, Compile-Fehler beim Konsumenten, mechanisch behebbar). Die numerischen Werte bleiben
  identisch — falls irgendwo gespeichert, ändert sich nichts.
- `DeclaredFieldsForm` / `DeclaredField` nehmen `SettingDescriptor` (heute Terminal-Deskriptor) — damit
  dient dieselbe Maske Terminals und Agent-Geräten.

### B3. Einstellungen am Plugin deklarieren

```csharp
public interface IHasDeviceSettings<TSettings> where TSettings : class, new() { }   // reiner Marker

[AttributeUsage(AttributeTargets.Property)]
public sealed class DeviceSettingAttribute : Attribute
{
    public DeviceSettingAttribute(string label);
    public bool Required { get; set; }
    public string? HelpText { get; set; }
    public string? ChoiceSource { get; set; }   // Name eines IDeviceChoiceProvider am Agenten
    public string? DependsOn { get; set; }
    public SettingKind? Kind { get; set; }      // leer = aus dem Property-Typ abgeleitet
}
```

- Der Katalog findet `TSettings` per Reflexion am Plugin-Typ (Marker-Interface) — **kein Bau**.
- Nur Properties mit `[DeviceSetting]` sind Felder. Was dort kein Feld hat, existiert nicht.
- Kind aus dem Typ: `string`→Text, Ganzzahl/Dezimal→Number, `bool`→Boolean, `DateTime`→DateTime,
  Enum→Choice mit statischen `Choices`.
- Ein Plugin ohne Marker hat keine Einstellungen (leere Liste, kein Fehler).

### B4. Wie das Plugin an seine Einstellungen kommt

Im Konstruktor, nicht nachträglich — kein Zeitfenster, in dem das Gerät ohne Einstellungen existiert.

```json
{ "Name": "EpsonTM", "ConstructionString": "[MiniStore.Devices.Epson.dll]<…EscPosPrinter>$DeviceSettings", "LoadType": "Scope" }
```

- `AgentServices` hängt sich an `PluginFactory.UnknownConstructorParameter`. Für den Namen
  `DeviceSettings` liest es den **Aufrufer** (`callingType`, dessen `UniqueName` = Objektname), sucht
  dessen `IHasDeviceSettings<T>`, lädt die lokale Ablage und deserialisiert nach `T`. Kein Eintrag
  → `new T()` mit Vorbelegungen **und** eine Warnung im Log („device X is not configured").
- Keine customVariables, kein `RegisterObject` je Gerät.
- **Verify-first, bevor gebaut wird:**
  1. Erreicht das Ereignis der Hauptfabrik auch Ladevorgänge in einem Scope (`NewScope`, FactoryWrapper
     je Nachricht)? Falls nicht: Weiterreichung an Scopes ergänzen.
  2. Verhalten im `reflectOnly`-Lauf (`DescribeScopedPlugin`): dort muss der Handler den **Typ**
     liefern, nichts laden.
  3. Wird das Ergebnis gepuffert? Eine geänderte Einstellung muss beim nächsten Bau des Bereichs-Plugins
     greifen.

### B5. Agent-Katalog

```csharp
public interface IAgentObjectCatalog
{
    Task<RemoteObjectInfo[]>     GetObjectsAsync(string contractTypeName);
    Task<SettingDescriptor[]>    DescribeSettingsAsync(string objectName);
    Task<SettingChoice[]>        GetChoicesAsync(string objectName, string settingName, string? draftJson);
    Task<string?>                GetSettingsAsync(string objectName);
    Task<ApplySettingsResult>    ApplySettingsAsync(string objectName, string json);
}
```

- **Arrays als Rückgabe**, nicht Listen — dieselbe IPC-Falle wie im Aktivitätskatalog.
- `contractTypeName`: `Type.FullName`. Der Agent löst gegen die Typen der Bereichs-Plugins auf
  (Vergleich über deren Interfaces), **nicht** über `Type.GetType` — der Vertrag muss am Agenten nicht
  vorab geladen sein, damit die Frage beantwortbar ist. Unbekannter Vertrag → leere Liste + Debug-Log.
- `RemoteObjectInfo`-DTO liegt in den Abstractions; `IRemoteObjectCatalog` im Web bildet es ab.
  `DisplayName` kommt aus einem neuen optionalen `PluginConfigurationItem.DisplayName` (Rückfall: Name).
- `GetChoicesAsync` → `IDeviceChoiceProvider` (benannt, am Agenten registriert). Der Provider ist **nie**
  das Gerät selbst. Das Toolkit liefert den Mechanismus; konkrete Provider (Windows-Drucker, COM-Ports)
  liefert der Konsument — sie sind plattformgebunden, das Paket bleibt Linux-lauffähig.
- `ApplySettingsAsync`: strikt nach `TSettings` deserialisieren (`UnmappedMemberHandling.Disallow`),
  `Required` prüfen, dann lokal ablegen. Ergebnis trägt Feldfehler, statt zu werfen.
- **Ablage (überarbeitet 2026-10-13):** in den nativen Einstellungen unter
  `ITVenture:AgentServices:Devices:<Name>` — lesen über `NativeSettings.GetSection`, schreiben mit
  `JsonUserSettings` in die Benutzerdatei (`NativeSettings.AddUserSettings`). Ersetzt die ursprüngliche
  Datei je Gerät unter `devices/`.
- Der Katalog ist `IPlugin` und wird am Agenten unter `AgentCatalog.DefaultObjectName` (oder dem Namen
  aus dem Template, siehe C2) registriert.
- **Schreibzugriff auf den Laden-PC:** der Katalog darf nur über den authentifizierten Kanal erreichbar
  sein. *Prüfen:* reicht die Hub-Authentifizierung, oder braucht der Katalog `RequiresSecurity` am
  FactoryWrapper?

### B6. Zahlterminals auf den Katalog umstellen

- `AgentTerminalPaymentService.DescribeDeviceSettingsAsync` fragt `IAgentObjectCatalog.DescribeSettingsAsync`
  (Route = ClientKey, Objekt = `ProviderTerminalId`) — **baut das Gerät nicht mehr**.
- Speichern im Terminal-Assistenten: die Geräte-Felder gehen per `ApplySettingsAsync` an den Agenten,
  nicht in `ConfigurationJson`. Route und `ProviderTerminalId` bleiben Spalten.
- `TerminalTarget.ConfigurationJson` ist auf dem Agenten-Weg leer; das Gerät liest seine Einstellungen
  aus dem Konstruktor (B4).
- `ITerminalDevice.DescribeSettingsAsync` entfällt (**breaking** — er war nur für den Agenten-Weg da).
  Das Gerät deklariert stattdessen `IHasDeviceSettings<T>`.
- Agent offline beim Bearbeiten: Maske zeigt „nicht erreichbar", **keine** veralteten Werte, Speichern
  gesperrt.
- Standard-`ITerminalChoiceProvider` für `TerminalChoiceSources.RemoteObjects` (Vertrag `ITerminalDevice`)
  kommt aus dem AgentServices-Webpaket und wird mit `AddAgentServices()` registriert — der Host baut nichts.

### B7. Tests (Teil B)

- `GetScopedPluginTypes`: Vertrag filtert, Disabled fehlt, kaputter Loader blockiert die anderen nicht.
- Katalog: **ein Gerät, dessen Konstruktor wirft, lässt sich aufzählen und beschreiben** (der Beweis,
  dass nichts gebaut wird).
- `ApplySettings`: unbekanntes Feld → abgelehnt; Pflichtfeld fehlt → Feldfehler; Ablage wird gelesen.
- B4: Bereichs-Plugin mit `$DeviceSettings` bekommt seine Werte; zwei Geräte desselben Typs bekommen
  je ihre eigenen.
- IPC-Rundreise wie `WorkflowActivityCatalogIpcTest`.
- Attrappen von `ITerminalDevice` im selben Zug mitziehen.

---

## Teil C — Zuordnung je Kasse

### C1. Kontext-Schnitt

```csharp
public interface IClientAppCore        { int ClientAppId; int TenantId; int ClientAppTemplateId;
                                         string ClientName; string ClientKey; bool Enabled; DateTime? LastSeenUtc; }
public interface IClientAppTemplateCore{ int ClientAppTemplateId; string Name; string? CatalogObjectName; }

public interface IClientAppContext<TClientApp, TClientAppTemplate>
    where TClientApp : class, IClientAppCore
    where TClientAppTemplate : class, IClientAppTemplateCore
{
    DbSet<TClientApp> ClientApps { get; set; }
    DbSet<TClientAppTemplate> ClientAppTemplates { get; set; }
}

ISecurityContext<…>       : IBaseTenantContext<…>, IClientAppContext<TClientApp, TClientAppTemplate>
IAgentServiceContext<…>   : IBaseTenantContext<…>, IClientAppContext<TClientApp, TClientAppTemplate>
{
    DbSet<ClientAppServiceBinding<TClientApp>> ClientAppServiceBindings { get; set; }   // Form beim Bau festlegen
}
```

- `ClientApp`/`ClientAppTemplate` implementieren die Core-Interfaces und **behalten alle Navigationen**
  (rund 80 Verwendungen von `.PermissionSets`, `.AppPermissions`, `.ClientAppTemplate` bleiben unberührt).
- Die beiden DbSets **wandern** von `ISecurityContext` nach `IClientAppContext` (nicht doppelt
  deklarieren, sonst CS0108). Aufrufer über `ISecurityContext` merken nichts.
- `IBaseTenantContext` bleibt unverändert — keine neuen Typparameter für Konsumenten ohne Agent.
- Navigation **einseitig**: Bindung → ClientApp. Die ClientApp bekommt **keine** Sammlung von Bindungen
  (das Sicherheitspaket kennt das Agent-Paket nicht).
- Test-Kontexte (InMemory) nehmen eine schlanke Test-ClientApp, die nur `IClientAppCore` erfüllt —
  sonst zieht EF über die Navigationen die ganze Rechte-Kette ins Testmodell.

### C2. Schema-Änderungen im Sicherheitskontext

| Tabelle | Spalte | Typ | Zweck |
|---|---|---|---|
| `ClientAppTemplates` | `CatalogObjectName` | `nvarchar(128) NULL` | leer = `AgentCatalog.DefaultObjectName` |
| `ClientApps` | `LastSeenUtc` | `datetime2 NULL` | An-/Abmeldung am Hub |

- Keine neue Entität im Sicherheitskontext → keine Stelligkeits-Drift der Vertrauens-Einträge.
  *Trotzdem prüfen:* ändert eine neue **Spalte** den Modell-Hash, gegen den die Vertrauens-Einträge
  laufen? Falls ja, gehört das in den Migrationsleitfaden.
- Beide Spalten in allen drei Fassungen (Basic, CoreIdentity, CoreIdentityTree) und in Config-Export /
  Mandanten-Vorlage (`CatalogObjectName` gehört zum Template und reist mit; `LastSeenUtc` nicht).
- Template-Maske (`AppTemplateDialog.razor`): ein Textfeld „Katalog-Objekt (leer = Standard)".

### C3. `ClientAppServiceBinding`

| Spalte | Typ | Regel |
|---|---|---|
| `ClientAppServiceBindingId` | int PK | |
| `TenantId` | int, FK `Tenants` | **`OnDelete: NoAction`** — sonst zwei Kaskadenpfade (Tenant→ClientApp→Bindung, Tenant→Bindung), SQL Server lehnt ab |
| `ClientAppId` | int, FK `ClientApps` | `OnDelete: Cascade` — Kasse weg, Bindungen weg |
| `Slot` | `nvarchar(64)` | vom Host deklariert |
| `ObjectName` | `nvarchar(128)` | Name am Agenten, unter dem der Proxy entsteht |
| `Enabled` | bit | abschalten statt löschen |

- Eindeutig: `UQ_ClientAppServiceBinding (ClientAppId, Slot)`.
- `TenantId` muss mit `ClientApp.TenantId` übereinstimmen — beim Schreiben prüfen (die DB kann es nicht).
- Mandantenfilter über den generischen `SetCurrentTenantInterceptor` (`EF.Property`).
- Mapping per `modelBuilder.ConfigureAgentServices<TClientApp>()`; schema-formende Konfiguration nur dort
  (Design-Time), nicht doppelt.
- Kein Config-Export, keine Mandanten-Vorlage: `ObjectName` beschreibt einen bestimmten Laden-PC.

### C4. Slots und Auflösung

```csharp
services.AddAgentServices<TContext, TClientApp, TClientAppTemplate>()
        .AddAgentServiceSlot<IReceiptPrinter>("receiptPrinter", "Bondrucker")
        .AddAgentServiceSlot<ICashDrawer>("cashDrawer", "Schublade");

public interface IAgentServiceResolver
{
    Task<AgentServiceHandle<T>?> GetAsync<T>(int clientAppId, string slot, CancellationToken ct = default) where T : class;
}
```

- Auflösung: Bindung (Enabled) → ClientApp (Enabled, gleicher Mandant) → `ClientKey` + `ObjectName` →
  `IInjectableProxy<T>`. Jeder Abbruchgrund (keine Bindung, abgeschaltet, Kasse aus, Vertrag passt nicht
  zum Slot) bekommt **seinen eigenen** Log-Eintrag und ein unterscheidbares Ergebnis — kein stilles `null`.
- Slot-Vertrag ≠ registrierter Vertrag → Fehler beim Start, nicht beim ersten Aufruf.

### C5. Web-seitige Standard-Umsetzungen

- `AgentRemoteObjectCatalog : IRemoteObjectCatalog` — fragt `IAgentObjectCatalog` unter dem ClientKey;
  Objektname aus `ClientAppTemplate.CatalogObjectName ?? AgentCatalog.DefaultObjectName`.
- Offline / Zeitüberschreitung: eigener Ergebnisfall „nicht erreichbar" (geloggt als Warning), nicht
  „leere Liste" — leer heisst laut Vertrag „bietet nichts dieser Art an".

### C6. `LastSeenUtc` aus dem Hub

- `EndPointBroker` bekommt Ereignisse `ServiceRegistered` / `ServiceUnregistered` (inkl. Timeout-Drop und
  `UnsafeServerDrop`), Argument: Dienstname + OwnerScope.
- Ein Beobachter im AgentServices-Webpaket schreibt `ClientApp.LastSeenUtc`, gefunden über `ClientKey`
  (systemweit eindeutig; Zugriff ohne Mandantenfilter, **nur** diese eine Spalte).
- Nicht passende Dienstnamen (kein ClientApp-Key): Debug-Log, nichts tun.
- Bei weitergereichter Registrierung (`remoteBroker`) feuert das Ereignis dort, wo der Broker sitzt. Läuft
  der Hub ohne Zugang zur Sicherheits-DB, bleibt `LastSeenUtc` leer — im Leitfaden dokumentieren.
- Online-Zustand für die Maske kommt live vom Broker; `LastSeenUtc` ist „seit wann / zuletzt".

### C7. Maske

- Einhängepunkt in `ClientAppDialog.razor` für Zusatzabschnitte (AdminViews kennt das Agent-Paket nicht).
  *Prüfen:* gibt es schon einen Erweiterungsmechanismus für die ClientApp-Detailansicht? Sonst einen
  schmalen bauen (registrierte Abschnitte, gerendert per `DynamicComponent`).
- Je deklariertem Slot: Auswahl aus dem Katalog (gefiltert auf den Slot-Vertrag), Ein/Aus, darunter
  `DeclaredFieldsForm` aus `DescribeSettingsAsync`, Werte aus `GetSettingsAsync`, Speichern →
  `ApplySettingsAsync` + Bindung schreiben.
- Lesender Hinweis: Zahlterminals, die auf diese Kasse zeigen (Route = ClientKey). Pflege bleibt in
  `TerminalsAdmin`.
- Rechte: die Handler prüfen selbst (wie §71). Eigenes Recht für die Slot-Pflege, Name beim Bau festlegen.

### C8. Tests (Teil C)

- Kontext: echter Kontext + InMemory mit schlanker Test-ClientApp; Kaskade ClientApp → Bindung.
- Eindeutigkeit `(ClientAppId, Slot)`; Mandanten-Mismatch wird abgelehnt.
- Resolver: jeder Abbruchgrund einzeln, mit unterscheidbarem Ergebnis.
- Handler-Rechte (Muster `TerminalsHandlerPermissionTests`).
- Broker-Ereignisse: Registrierung, Abmeldung, Timeout.

---

## 4. Reihenfolge

| Schritt | Inhalt | Breaking |
|---|---|---|
| 1 | B1 Kern in Plugins, `PluginActivityCatalog` umstellen | nein |
| 2 | **Verify-first B4** (Ereignis im Scope, reflectOnly, Puffer) | — |
| 3 | B2 Feldmodell + Abstractions-Paket | ja (Terminal-Kinds) |
| 4 | B3–B5 Agent-Paket: Marker, Auflösung, Katalog, Ablage | nein |
| 5 | B6 Zahlterminals umstellen | ja (`ConfigurationJson`, `ITerminalDevice`) |
| 6 | C1–C2 Kontext-Schnitt + Spalten | Migration Sicherheitskontext |
| 7 | C3–C5 WebPart, Bindung, Resolver, Remote-Katalog | neue Tabelle (opt-in) |
| 8 | C6 Broker-Ereignisse + Beobachter | nein |
| 9 | C7 Maske | nein |
| 10 | Migrationsleitfaden §72, Gegenprobe MiniStore | — |

## 5. Migrationsleitfaden (§72, Entwurf der Punkte)

- **Pflicht für alle mit Sicherheitskontext:** 2 nullbare Spalten (`ClientAppTemplates.CatalogObjectName`,
  `ClientApps.LastSeenUtc`).
- **Opt-in:** `IAgentServiceContext` implementieren → 1 Tabelle.
- **Agent-Zahlterminals:** bestehende `ConfigurationJson` der Agent-Terminals einmal per Maske an den
  Agenten übertragen; danach wird das Feld nicht mehr gelesen. §70.4 („der Agent hält nichts") wird
  dafür überarbeitet.
- **Compile-Fehler:** `TerminalSettingKind` → `SettingKind`, `TerminalSettingChoice` → `SettingChoice`;
  `ITerminalDevice.DescribeSettingsAsync` entfernen, stattdessen `IHasDeviceSettings<T>`.
- **Agent-Konfiguration:** Konstruktor-Parameter `$DeviceSettings`; Katalog-Plugin unter dem Standardnamen
  registrieren (oder dem im Template gesetzten).

## 6. Risiken

- **B4 trägt den Plan.** Erreicht das Ereignis den Scope nicht, oder puffert die Fabrik das Ergebnis,
  braucht es einen anderen Weg — darum Schritt 2 vor allem anderen am Agenten.
- **Hub ohne DB** → kein `LastSeenUtc`. Akzeptiert, dokumentiert.
- **Schreiben auf den Laden-PC über IPC** — Absicherung des Katalogs vor Schritt 4 klären.
