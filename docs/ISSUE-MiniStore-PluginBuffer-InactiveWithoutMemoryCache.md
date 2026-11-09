# Issue: Der Plugin-Puffer der WebPlugins ist ohne `IMemoryCache` still abgeschaltet

**Status:** umgesetzt — `GetObjectProvider` puffert ohne `IMemoryCache` in einem Singleton je DI-Container (`InitializePluginSystem`), sonst Warnung; neues Signal-Thema `PluginConfiguration` macht Einträge nach Änderungen ungültig; Fabriken mit ausdrücklichem Mandanten puffern getrennt und lesen den genannten Mandanten vor dem Filter. Leitfaden §100
**Datum:** 2026-11-09
**Quelle:** MiniStore-Session (Konsument), beim Messen des Dashboards (`DashboardHost`, Leitfaden §98/§99).
**Toolkit-Stand:** `5.0.0-PRE268`.

## Der Fall

Das Dashboard des MiniStore (8 Kacheln, alle auf einem Fachkontext `SalesContext` aus der Plugin-Fabrik)
braucht im warmen Zustand stabil **1.3 s**, rund **130 ms je Kachel**. Die Datenmenge ist dabei
winzig (59 Aufträge), und die eigentlichen Abfragen der Kacheln kosten in Postgres wenige Millisekunden.

Postgres-Statementlog (`log_min_duration_statement = 0`) über **einen** warmen Ladevorgang (1.67 s mit
eingeschaltetem Protokoll):

| Statement | Anzahl |
|---|---|
| `DISCARD ALL` (Pool-Reset beim Zurückgeben der Verbindung, also je Abfrage) | 605 |
| `SELECT … FROM "TenantSettings"` | 214 |
| `SELECT … FROM "GlobalSettings"` | 214 |
| `SELECT u."ParentTenantId" …` (Baumaufstieg für die Vererbung) | 115 |
| `SELECT … FROM "GenericPluginParams"` | 55 |
| `SELECT … FROM "WebPlugins"` (zwei Formen) | 22 |
| Fach-SQL der Kacheln (`Orders`, `Payments` …) | eine Handvoll |

Die gelesenen Schlüssel sind die Init-Sequenzen jedes Glieds der Kontext-Kette, **in einem einzigen
Ladevorgang je 8- bis 16-mal**:

```
PreInitSequenceFor45e6968d5a7044c38ee17787efe0ee66SalesContext          16×
PostInitSequenceFor45e6968d5a7044c38ee17787efe0ee66SalesContext         16×
PreInitSequenceFor$PgSqlOptionsLoader4[CallingPlugin.UniqueName]          9×
PreInitSequenceForPgSqlOptionsLoader4SalesContext                         8×
PreInitSequenceFor$SqlDefaultSafeInterceptor4[CallingPlugin.PrevPlugin(1).UniqueName]   9×
PreInitSequenceForSqlDefaultSafeInterceptor4SalesContext                  8×
… (dasselbe für CurrentUserProvider, SqlAutoTenantSaveInterceptor, BuilderOptions, jeweils Pre und Post)
```

Dass beide Formen gelesen werden, generisch **und** aufgelöst, ist richtig so (`InitSequenceSetting`,
`WebPluginHelper.cs:242`). Falsch ist nur, dass dieselben Schlüssel bei jeder Kachel wieder in der
Datenbank landen. Keine dieser Einstellungen existiert.

## Die Ursache

Der Puffer dafür ist da und richtig gebaut:

- `SettingByName` puffert **je Name** über `tenantObjects.GetBufferedValue` (`WebPluginHelper.cs:235`).
  Generischer und aufgelöster Name sind damit getrennte Einträge, so wie es das Auflösen verlangt.
- `TimedObjectProvider` puffert auch `null` (`BufferedObject(defaultValue(k), …)`), also auch das „gibt
  es nicht“, 30 Minuten lang.
- Ebenso gepuffert wären `TenantPI#…` (die `WebPlugins`-Zeile, `:264`) und `PIArgs4#…` (die generischen
  Parameter, `:374`).

**Nur kommt `TimedObjectProvider` gar nicht zum Zug.** `ServiceProviderExtensions.GetObjectProvider`
(`Extensions/ServiceProviderExtensions.cs:718`) legt ihn ausschliesslich in einem `IMemoryCache` ab:

```csharp
var mc = serviceProvider.GetService<IMemoryCache>();
if (!(mc?.TryGetValue<IObjectProvider>(objectName, out retVal) ?? false))
{
    if (mc == null)
    {
        retVal = defaultProvider?.Invoke();     // → new DummyObjectProvider()
    }
    …
```

Ohne `IMemoryCache` in der DI kommt `DummyObjectProvider`, der bei jedem Aufruf `defaultValue(key)` ruft.
Das heisst: **kein einziger Puffer der Plugin-Fabrik wirkt**, und es gibt dazu keine Meldung.

Wer `IMemoryCache` registriert, ist Zufall der Host-Konfiguration. `AddControllersWithViews`/`AddRazorPages`
(MVC, Cache-TagHelper) bringen ihn mit, `AddRazorComponents` nicht. Das Toolkit selbst ruft
`AddMemoryCache` nirgends (ausser in `ITVComponents.GenericService`, indirekt über MVC). Der MiniStore ist
ein reiner Blazor-Host und registriert ihn deshalb nicht. MLM ist ebenfalls ein Blazor-Host und ruft
`AddMemoryCache` vermutlich von Hand; dort fällt es deshalb nicht auf. Dass der Puffer an einem Aufruf
hängt, den der Host kennen muss, steht nirgends.

## Die Wirkung

Jede Auflösung eines Fachkontexts über die Plugin-Fabrik kostet im MiniStore rund 140 Rundreisen zur
Datenbank (Settings zweimal je Glied und Phase, Baumaufstieg, Plugin-Zeile, generische Parameter, und je
Abfrage ein Pool-Reset). Beim Dashboard fällt das auf, weil `DashboardHost` den Kontext je Kachel least,
was für sich richtig ist. Es trifft aber **jede** Pflegemaske und jeden Dienst, der einen Fachkontext least.

## Vorschlag

1. **Den Puffer nicht von der Host-Konfiguration abhängig machen.** Entweder registriert das Toolkit dort,
   wo es die WebPlugins einrichtet, selbst `services.AddMemoryCache()` (idempotent, `TryAdd` intern),
   oder `GetObjectProvider` fällt ohne `IMemoryCache` auf einen prozessweiten `TimedObjectProvider` je
   `objectName` zurück statt auf `DummyObjectProvider`.
2. **Den Rückfall nicht still lassen.** Falls `DummyObjectProvider` als Möglichkeit bleiben soll, einmal
   je Prozess eine Warnung, die sagt, dass die Plugin-Puffer aus sind und warum.
3. **Bei einem eigenen Puffer für die Init-Sequenzen**, falls es den je geben soll: Er muss generischen und
   aufgelösten Namen getrennt führen und den Vorrang „spezifisch schlägt generisch“ behalten, wie heute
   `InitSequenceSetting`. Ein Puffer je *Plugin* statt je *Name* gäbe dem zweiten Anforderer die Sequenz
   des ersten.

Eine Frage dazu: Wird der Puffer geleert, wenn jemand eine Init-Sequenz, eine `WebPlugins`-Zeile oder einen
generischen Parameter über die Masken ändert? Sonst wirkt eine Änderung mit dem Puffer bis zu 30 Minuten
später als heute. Für den MiniStore wäre das hinnehmbar. Es sollte aber im Leitfaden stehen, weil es ein
anderes Verhalten ist als das, was Hosts ohne `IMemoryCache` heute erleben.

## Umgehung (MiniStore)

`builder.Services.AddMemoryCache();` in `Program.cs`, im MiniStore seit 2026-11-09 eingebaut.

### Gemessen mit Umgehung (2026-11-09)

Selbes Dashboard, selbe Daten, Statementlog beide Male eingeschaltet:

| | ohne `IMemoryCache` | mit `IMemoryCache` |
|---|---|---|
| Dashboard warm, gesamt | 1.67–1.75 s | **0.67–0.80 s** |
| je Kachel | 155–355 ms | 56–144 ms |
| Rundreisen je warmem Ladevorgang | ~1300 | **~173** |
| davon `DISCARD ALL` | 605 | 70 |
| `TenantSettings` / `GlobalSettings` | 214 / 214 | 0 / 0 |
| `GenericPluginParams` / `WebPlugins` | 55 / 22 | 0 / 0 |
| `ParentTenantId` | 115 | 33 |

Die Settings-, Plugin- und Parameter-Abfragen sind nach dem ersten Laden vollständig weg. Damit ist die
Ursache bestätigt.

Was je Kachel bleibt, ist klein, aber sichtbar: `DashboardHost` liest bei jedem Laden je Kachel die
`DiagnosticsQueries`-Zeile, ihre Parameter, das Recht dazu, den Mandanten und den Benutzer neu (je 8–9×).
Das ist kein Teil dieses Issues und für sich unkritisch; erwähnt nur, falls beim Puffer ohnehin jemand
dort vorbeikommt.
