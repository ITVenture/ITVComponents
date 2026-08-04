# ITVComponents.Workflow — Einbindungs-Leitfaden

Dieser Leitfaden zeigt, wie die Workflow-Bibliothek in einen WebCoreToolkit-Host eingebunden
wird — anhand von drei Deployment-Szenarien:

- **a) Web-Only** — Editor **und** Engine laufen im selben Web-Prozess.
- **b) Web / Backend-Service** — Editor im Web, Engine (Ausführung) in einem Backend-Dienst.
- **c) Web / Web-Engine / Backend-Service** — Editor im Web, **eine** Engine im Web **und** eine
  im Backend-Dienst (verteilte Ausführung mit Ziel-Routing).

> Alle Typ- und Methodennamen in diesem Dokument entsprechen dem aktuellen Stand des Codes
> (Branch `Workflow_Bringup`). Für die genauen Signaturen siehe die jeweiligen Quelldateien.

---

## 1. Bausteine

| Baustein | Typ | Aufgabe |
|---|---|---|
| **Store** | `IWorkflowStore` — `EfWorkflowStore` (DB) / `InMemoryWorkflowStore` | Persistenz von Definitionen und Instanzen; Abfragen für Wiederaufnahme (Signal, Timer, Handoff). |
| **DbContext** | `WorkflowContext` (EF Core) | Tabellen (Instanzen, Token-Zeilen, Protokoll-Zeilen, Definitionen, Zweig-Sperren); tenant-fähig. |
| **Engine** | `WorkflowEngine` | Treibt Instanzen voran (Knoten ausführen, Gateways, Wartepunkte). |
| **Activity-Host** | `IActivityHost` — `ActivityRegistry` / `PluginActivityHost` / `WebToolkitActivityHost` | Löst je Vortrieb die auszuführenden Aktivitäten auf. |
| **Runner** | `WorkflowRunner` (auf `ITVComponents.ParallelProcessing`) | Laufender Dienst: treibt Workflows **zweig-granular, nebenläufig, prozessübergreifend** voran. |
| **Katalog** | `IWorkflowActivityCatalog` — `PluginActivityCatalog` (in-process) / `WorkflowActivityCatalogClient` (IPC) | Liefert dem Editor die verfügbaren Aktivitäts-Typen + deklarierten Parameter (ohne Instanzierung). |
| **Views** | `AddWorkflowViews(...)` (Modul `…WorkflowViews`) | Blazor-Editor + Monitoring + Design-Ansichten. |

### Zwei Ausführungs-Modi

1. **Inline / sequenziell** — `engine.StartWorkflow`, `engine.SignalWorkflow`, `engine.TriggerDueTimers`.
   Advanced **synchron im aufrufenden Prozess** bis zum nächsten Wartepunkt. Einfach, ein Prozess.
2. **Nebenläufiger Runner** — `WorkflowRunner`. Zweig-granular, mit Zweig-Sperren + optimistischem
   Commit + atomarem Join; **prozessübergreifend** und Voraussetzung für verteilte Ausführung/Handoff.

> **Signal-Zustellung ist konfigurierbar.** Der Monitor-Handler stellt ein Operator-Signal je nach
> `WorkflowViewsOptions.SignalDelivery` zu: **`Inline`** (Standard) advanced im Web-Prozess
> (`SignalWorkflow`) — ideal für Web-Only; **`Runner`** reaktiviert nur store-only
> (`ReactivateSignal`) und überlässt das Vorantreiben einem (Backend-)Runner — für getrennte
> Deployments (b/c). `Cancel` ist in allen Fällen eine reine Store-Operation. Siehe die Szenarien.

### Schlüsselbegriffe für den verteilten Betrieb

- **`AutomatedActivityNode.ExecutionTarget`** (freier String, leer = beliebiger Host): der Host, auf
  dem diese Aktivität laufen muss. Wird im Editor pro Aktivität als „Execution target" gepflegt.
- **`WorkflowEngine` — `hostTargets`** (Ctor-Parameter, `engine.HostTargets`): die Ziele, die
  **diese** Engine/dieser Host bedient. Trifft ein Zweig auf einen Knoten mit fremdem Ziel, **parkt**
  er (`TokenStatus.WaitingForTarget`) und wird von einem Runner mit passendem Ziel aufgenommen.
- **`WorkflowRunnerOptions.Owner`** (stabiler Runner-Name, Standard `Environment.MachineName`):
  Besitzer der Zweig-Sperren **und der Timer-Ansprüche**. Muss über Neustarts **gleich** bleiben (beim
  Start räumt der Runner seine eigenen, nach einem Absturz hängengebliebenen Sperren und Ansprüche über
  diesen Namen ab).
- **`WorkflowRunnerOptions.TimerLeaseMs`** (Standard 60 000) und **`.MaxTimerBatch`** (Standard 200):
  wie lange ein aufgegriffener fälliger Timer für diesen Runner reserviert bleibt und wie viele
  Instanzen ein Poll höchstens nimmt. Beim Web-Worker heissen sie `WorkflowWorkerOptions.TimerLease`
  und `.MaxTimerBatch`. Siehe §6, „Timer-Ansprüche".

---

## 2. Gemeinsame Voraussetzungen (alle Szenarien)

1. **Datenbank / `WorkflowContext`.** Provider (SqlServer/PostgreSql/SQLite) wählen und das Schema
   per EF-Migration (Prod) bzw. `EnsureCreated` (Tests) anlegen. `WorkflowContext` ist
   `[ScopedDependency]` + `IPlugin` und wird nach der **Toolkit-Konvention** geladen (wie der
   `TaskSchedulerContext`): als benannte, scoped DI-Dependency mit `ContextOptionsLoader<WorkflowContext>`,
   `IUserAwareContext` (Tenant-Quelle) und `useTenantFilter`. Der Host stellt daraus einen
   `IDbContextFactory<WorkflowContext>` bereit (davon hängen die View-Handler ab).

2. **Store / Engine registrieren** (es gibt bewusst keinen `AddWorkflowEngine`-Zauber — explizit
   verdrahten):

   ```csharp
   // IWorkflowStore über den DbContext-Factory
   services.AddSingleton<IWorkflowStore>(sp =>
       new EfWorkflowStore(() =>
           sp.GetRequiredService<IDbContextFactory<WorkflowContext>>().CreateDbContext()));

   // Activity-Host: je nach Szenario (siehe unten)
   services.AddSingleton<IActivityHost>(sp => /* … */);

   // Engine
   services.AddSingleton(sp => new WorkflowEngine(
       sp.GetRequiredService<IWorkflowStore>(),
       sp.GetRequiredService<IActivityHost>(),
       evaluator: null,                 // null = CScript-Standard
       hostTargets: /* siehe Szenario */));
   ```

3. **Views-Modul.** Entweder über die WebPart-Konfiguration (`WorkflowViewsOptions.ConfigureViews = true`
   in der Modul-Sektion der `appsettings`) — dann registriert `WebPartInit` alles automatisch — oder
   explizit:

   ```csharp
   services.AddWorkflowViews(partTypeLoadBehavior: null);
   ```

   Feature `ITVWorkflow` und die Permissions `Workflow.Monitor` / `Workflow.Operate` /
   `Workflow.Design` / `Workflow.Tasks` (Konstanten in `WorkflowSecurity`) sind **DB-getrieben** zu
   aktivieren (Navigation + Feature-Freischaltung sind Host-Sache). `Workflow.Tasks` ist die
   **Benutzer**-Berechtigung für die Arbeitsliste (Abschnitt 9) und gehört an andere Rollen als die drei
   Betreiber-Rechte.

4. **BlazorMonaco-Skripte — nichts mehr zu tun ausser `<ITVentureReferences />`.** Der
   CScript-/Ausdrucks-Editor braucht die drei BlazorMonaco-Skripte (`jsInterop.js`, `loader.js`,
   `editor.main.js`) in dieser Reihenfolge. Das **Modul meldet sie selbst an**
   (`WebPartInit.RegisterServices` → `AddToolkitClientScript`, Fix zu `BUG-PRE141` §5); der Host muss
   nur die eine Zeile `<ITVentureReferences />` in seiner **statisch gerenderten** Host-Seite (`App.razor` /
   `_Host.cshtml`) haben — dort emittiert das Toolkit die Vereinigung aller Modul-Skripte.
   Handgesetzte `<script>`-Tags sind nicht mehr nötig und können entfernt werden; Duplikate werden
   ohnehin ignoriert, die Registrierungsreihenfolge bleibt erhalten.

   > Fehlt `<ITVentureReferences />` in der Host-Seite, rendern die CScript-Felder still nicht — das
   > ist dann die erste Stelle, an der man nachsieht. Ein Skript-Tag **innerhalb** einer interaktiven
   > Komponente wird nicht ausgeführt; es muss im initialen Dokument stehen.

5. **View-Handler: frischer Kontext pro Op + Engine-Factory (Blazor-/Tenant-sicher).** Die View-Handler
   halten **keinen** langlebigen Store/Engine/DbContext mehr (unter Blazor ist der DI-Scope der ganze
   Circuit → geteilt über nebenläufige Renders). Stattdessen zieht **jede Operation** über **einen** Seam —
   `IFreshInjectablePlugin<WorkflowContext>` — einen **frischen** `WorkflowContext` (eigener
   Operations-Scope, am Op-Ende disposed). Der Host stellt dafür bereit:

   1. **`UseInjectablePlugins(...)`** (registriert `IFreshInjectablePlugin<>`).
   2. **`WorkflowContext` als scope-owned Dependency** über `FactoryOptions.AddDependency(name, delegate,
      disposeWithScope: true)`. Das `disposeWithScope: true` ist Pflicht — nur so hängt
      `CreateOperationScope()` den Kontext frisch je Op ein und disposed ihn mit dem Scope. **WOHER** der
      Kontext kommt, ist reine Host-Registrierung — DI-vs-Plugin-Dualität in **einem** Delegate:

      ```csharp
      // global (Ein-Kontext, filterfrei): das Delegate zieht aus dem DbContext-Factory
      factoryOptions.AddDependency("WorkflowContext",
          sp => sp.GetRequiredService<IDbContextFactory<WorkflowContext>>().CreateDbContext(),
          disposeWithScope: true);

      // ODER per-Tenant: das Delegate baut den tenant-fähigen Kontext (Toolkit-Konvention)
      ```

   Die Views kennen nur den einen Seam; sie sehen den Unterschied global/tenant nicht.

   **Alternative im Ein-Kontext-Fall: `WorkflowContext` direkt aus der DI statt aus der Plugin-Factory.**
   Betreibt der Host genau **eine** Umgebung, muss der `WorkflowContext` nicht zwingend als scope-owned
   Factory-Dependency (`AddDependency(..., disposeWithScope: true)`) laufen — er kann auch schlicht der im
   DI-Container registrierte Service sein. Dazu wird für `WorkflowContext` ein `ServiceProviderPluginInjector`
   registriert; der Konsument hängt weiterhin nur an `IFreshInjectablePlugin<WorkflowContext>` (ein Ctor,
   keine DI-Mehrdeutigkeit), bekommt die Instanz aber aus der DI:

   ```csharp
   services.Configure<InjectablePluginOptions>(o =>
       // disposeWithContext: true ist beim Fresh-Weg PFLICHT (siehe Kasten) — es ist der bewusste Opt-in.
       o.UseServiceInstance<WorkflowContext>(disposeWithContext: true));
   ```

   > **Wichtig — was `disposeWithContext: true` zusichert.** Der Fresh-Weg (`IFreshInjectablePlugin`) öffnet je
   > `Lease()` einen eigenen Operations-Scope und disposed ihn am Op-Ende; er erwartet, dass die geleaste
   > Instanz **von diesem Scope besessen** und mit ihm disposed wird. Der `ServiceProviderPluginInjector` zieht
   > die Instanz aber aus dem **äußeren** `ServiceProvider` — der Scope besitzt sie nicht. Ohne Deklaration
   > **wirft** der Fresh-Weg deshalb bewusst (klare Trennung „frisch" vs. „geteilt"); der reguläre
   > `IInjectablePlugin<WorkflowContext>`-Weg (geteilte Instanz) bleibt davon unberührt. Setzt du das Flag,
   > **übernimmst du die Zusage**, dass die DI-Registrierung je Lease eine **frische, disposbare** Instanz
   > liefert — sonst bekommst du entweder einen über den ganzen Scope/Circuit **geteilten** Kontext
   > (Change-Tracker-Bleed unter Nebenläufigkeit) oder ein **Disposal-Leck**. Registriere `WorkflowContext`
   > dafür so, dass jede Auflösung frisch ist (z.B. das Delegate zieht aus `IDbContextFactory<WorkflowContext>`
   > bzw. Transient-Registrierung), **nicht** als geteilten Scoped-Service. Die falsche Zusage ist eine
   > bewusste Fehlkonfiguration und quittiert sich meist schnell mit einem Laufzeitfehler.
   >
   > Der `WorkflowContext` wird **benannt** geleast (`Lease(spec.StorePluginName)`); auf dem DI-Weg ist der
   > Name bedeutungslos (die DI kennt genau eine `WorkflowContext`-Registrierung) — der `ServiceProviderPluginInjector`
   > liefert in beiden Fällen (benannt/namenlos) dieselbe DI-Instanz.

   Für **Signal/Abbruch** registriert der Host zusätzlich eine `WorkflowEngineFactory` — sie baut eine
   Engine über den *pro Op frisch gebauten* Store und kapselt die Engine-Konfiguration des Hosts:

   ```csharp
   services.AddSingleton<WorkflowEngineFactory>(sp => store =>
       new WorkflowEngine(store, sp.GetRequiredService<IActivityHost>(),
           evaluator: null, hostTargets: /* wie im Szenario */));
   ```

   > **Wichtig:** Die `store`-Variable des Delegaten verwenden (nicht einen Singleton-Store einfangen) —
   > nur so läuft jede Operation gegen einen frischen, tenant-korrekten Kontext.

   Die in Abschnitt 2.2 registrierten **Singleton**-`IWorkflowStore`/`WorkflowEngine` bleiben für den
   **Runner** und die **Inline-Ausführung** (`StartWorkflow`/`TriggerDueTimers`) sinnvoll — die *Views*
   nutzen sie nicht mehr.

   **Inline-Ausführung im Web = globales Szenario.** Advanced der Web-Prozess ein Signal inline
   (`SignalDelivery.Inline` → `SignalWorkflow`), führt er die folgenden Aktivitäten lokal aus — das
   funktioniert nur sinnvoll gegen **einen** Kontext. Multi-Tenant klappt nur mit einem
   **tenant-übergreifenden Runner**, der alle offenen Instanzen lädt und jede unter ihrem Tenant
   voran treibt. Deshalb im Multi-Tenant-Fall den `WorkflowContext`-Delegaten tenant-fähig registrieren
   **und** `SignalDelivery = Runner` setzen: das Web reaktiviert/bricht nur store-only auf dem Tenant der
   Instanz ab (korrekt pro Op), die Ausführung übernimmt der Runner.

---

## 3. Szenario a) Web-Only

Editor, Monitoring **und** Engine im selben Web-Prozess. Einfachster Fall — kein IPC, kein
Ziel-Routing.

```
┌────────────────────────── Web-Host (ASP.NET Core + MudBlazor) ──────────────────────────┐
│  WorkflowViews (Editor/Monitoring/Design)                                                │
│  IWorkflowActivityCatalog = PluginActivityCatalog  (in-process)                          │
│  WorkflowEngine + IActivityHost (Aktivitäten hier)                                        │
│  IWorkflowStore = EfWorkflowStore ── WorkflowContext ──► DB                              │
│  (optional) WorkflowRunner als HostedService (Timer/Recovery/Nebenläufigkeit)           │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

**Verdrahtung** (zusätzlich zu Abschnitt 2):

```csharp
// Aktivitäten: entweder als Plugins (DB-getrieben, empfohlen im Toolkit) …
services.AddSingleton<IActivityHost>(sp => new WebToolkitActivityHost(sp));
//   … oder für feste, in-Code-Aktivitäten:
// services.AddSingleton<IActivityHost>(_ => new ActivityRegistry()
//     .Register("sendMail", ctx => { /* … */ }));

// Katalog für die typisierten Editor-Formulare (in-process, aus der Plugin-Factory)
services.AddSingleton<IWorkflowActivityCatalog>(sp =>
    new PluginActivityCatalog(sp.GetRequiredService<PluginFactory>()));

// Engine ohne Ziele — alles läuft hier
services.AddSingleton(sp => new WorkflowEngine(
    sp.GetRequiredService<IWorkflowStore>(),
    sp.GetRequiredService<IActivityHost>()));
```

**Ausführung.** Zwei Wege, je nach Bedarf:

- **Inline** (genügt oft): Workflow starten mit `engine.StartWorkflow(defId, vars)`; ein Signal aus
  dem Monitoring liefert der Handler über `engine.SignalWorkflow(...)` (advanced hier, korrekt, weil
  die Aktivitäten lokal sind). Fällige Timer aus einem einfachen `IHostedService`-Tick über
  `engine.TriggerDueTimers(DateTime.UtcNow)`.
- **Runner** (empfohlen, sobald Timer/Recovery/Parallelität gebraucht werden): einen
  `WorkflowRunner` als `IHostedService` mitlaufen lassen — er nimmt fällige Timer auf, treibt
  parallele Zweige nebenläufig voran und greift nach einem Neustart liegengebliebene Instanzen wieder
  auf:

  ```csharp
  public sealed class WorkflowRunnerService : IHostedService
  {
      private readonly WorkflowRunner runner;
      public WorkflowRunnerService(WorkflowEngine engine, IWorkflowStore store)
          => runner = new WorkflowRunner(engine, store,
                 new WorkflowRunnerOptions { WorkerCount = 4, PollTimeMs = 1000 });
      public Task StartAsync(CancellationToken _) { runner.Start(); return Task.CompletedTask; }
      public Task StopAsync(CancellationToken _) { runner.Stop(); return Task.CompletedTask; }
  }
  // services.AddHostedService<WorkflowRunnerService>();
  ```

Kein `ExecutionTarget`, keine `HostTargets` — der Standard (leer/„beliebig") deckt Web-Only voll ab.

---

## 4. Szenario b) Web (Editor/Monitoring) / Backend-Service (Engine)

Der Editor lebt im Web, die **Ausführung** in einem separaten Backend-Dienst. Beide teilen sich
**dieselbe Datenbank**. Die Aktivitäts-Plugins liegen **nur im Backend**.

```
┌───────────── Web-Host ─────────────┐         ┌──────────── Backend-Service ────────────┐
│ WorkflowViews (Editor/Monitoring)  │         │ PluginFactory (+ Activity-Plugins)       │
│ IWorkflowActivityCatalog =         │  IPC    │ IActivityHost (Plugin/Toolkit)           │
│   WorkflowActivityCatalogClient ───┼────────►│ PluginActivityCatalog  (exponiert)       │
│ IWorkflowStore/Engine* (nur        │         │ WorkflowEngine + WorkflowRunner          │
│   Store-Operationen, KEINE         │         │   (Owner = "backend")                    │
│   Ausführung)                      │         │ IWorkflowStore = EfWorkflowStore         │
└───────────────┬────────────────────┘         └───────────────┬──────────────────────────┘
                └────────────── gemeinsame DB ───────────────────┘
```

### Backend-Service

```csharp
// Plugin-Factory mit den Aktivitäts-Plugins (ScopeMode.PerAsyncContext für Worker-Threads)
var factory = new PluginFactory(ScopeMode.PerAsyncContext);
// … Loader/Assemblies registrieren, sodass die ActivityRefs auflösbar sind …

IWorkflowStore store = new EfWorkflowStore(() => new WorkflowContext(options)); // FILTERFREI, s.u.
IActivityHost host = new PluginActivityHost(factory);   // oder WebToolkitActivityHost(sp) im Toolkit
var engine = new WorkflowEngine(store, host);           // keine hostTargets nötig (einziger Runner)

var runner = new WorkflowRunner(engine, store,
    new WorkflowRunnerOptions { Owner = "backend", WorkerCount = 4, PollTimeMs = 1000 });
runner.Start();   // räumt eigene Alt-Sperren ab, pollt Instanzen + Timer, treibt nebenläufig voran

// Katalog über IPC exponieren, damit der Web-Editor die Aktivitäts-Typen kennt:
var catalog = new PluginActivityCatalog(factory) { UniqueName = "workflowCatalog" };
var exposed = new Dictionary<string, object> { { "workflowCatalog", catalog } };
// InMemory (in-proc/Test) …
var server = new InMemoryServer((IServiceHubProvider)hub, exposed, "WorkflowService");
server.Initialize();   // BaseServer ist IDeferredInit — sonst findet der Client den Dienst nicht!
// … über Prozessgrenzen dieselbe Form mit dem gRPC-Server/-Client aus
//    ITVComponents.InterProcessCommunication (Muster wie ManagementExtensions/Scheduling).
```

- **Tenant-übergreifend:** Der Runner-Store bleibt **filterfrei** (Discovery über alle Tenants),
  aber jede Instanz wird unter **ihrem** Tenant vorangetrieben. Im Toolkit erreicht man das mit
  `WebToolkitActivityHost` (fixiert je Vortrieb den Instanz-Tenant über einen Hintergrund-Principal);
  stack-neutral genügt `PluginActivityHost` + der ambiente `WorkflowExecutionScope`, den `RunBranch`
  aus `instance.TenantId` selbst setzt.
- **`Owner`** stabil halten (z.B. Dienstname/Host) — bei mehreren Backend-Instanzen je einen
  **eindeutigen** Owner; die Zweig-Sperren serialisieren dann prozessübergreifend über die DB.

### Web-Host

```csharp
// Katalog: Proxy auf den Backend-Katalog (der Editor holt IWorkflowActivityCatalog aus DI)
services.AddSingleton<IWorkflowActivityCatalog>(sp =>
    new WorkflowActivityCatalogClient(sp.GetRequiredService<IBaseClient>(), "workflowCatalog"));

// Store + DbContext auf DIESELBE DB (für Design speichern/laden + Monitoring lesen)
services.AddSingleton<IWorkflowStore>(sp =>
    new EfWorkflowStore(() =>
        sp.GetRequiredService<IDbContextFactory<WorkflowContext>>().CreateDbContext()));

// Der Monitor-Handler hängt an einer WorkflowEngine — sie darf hier aber NICHT ausführen.
// Defensiv: ein winziger, host-eigener IActivityHost, dessen Scope beim Resolve wirft (die Web-Engine
// soll nie eine Aktivität laufen lassen). Alternativ genügt eine leere `new ActivityRegistry()`, die
// bei einem versehentlichen Resolve mit KeyNotFoundException scheitert.
services.AddSingleton<IActivityHost>(_ => new ActivityRegistry());
services.AddSingleton(sp => new WorkflowEngine(
    sp.GetRequiredService<IWorkflowStore>(), sp.GetRequiredService<IActivityHost>()));

// Signal store-only zustellen (kein inline-Advance im Web): der SplitWorkflowMonitorHandler wird
// registriert; das Signal reaktiviert nur, der Backend-Runner treibt voran.
services.AddWorkflowViews(partTypeLoadBehavior: null,
    options: new WorkflowViewsOptions { SignalDelivery = WorkflowSignalDelivery.Runner });
// (Per WebPart-Konfiguration äquivalent: in der Modul-Sektion "SignalDelivery": "Runner" setzen.)
```

**Kommando-Oberfläche im Web (starten / signalisieren / abbrechen) — nur Store-Operationen:**

| Aktion | Web ruft | Wirkung |
|---|---|---|
| Starten | `engine.CreateInstance(defId, vars, corr)` | Legt die Instanz mit **aktiven Start-Tokens** an, **ohne** zu advancen → der Backend-Runner nimmt sie beim Poll auf. |
| Signal | `engine.ReactivateSignal(id, signal, payload)` | Schiebt wartende Tokens **store-only** über den Wartepunkt (führt **keine** Aktivität aus) → Backend-Runner treibt sie voran. |
| Timer | `engine.ReactivateTimers(id, now)` | Wie Signal, für fällige Timer. |
| Abbrechen | `engine.CancelWorkflow(id)` | Reine Store-Operation (Tokens verbraucht, Status `Cancelled`). |

> **Signal-Zustellung:** Mit `SignalDelivery = Runner` (oben) registriert das Modul den
> `SplitWorkflowMonitorHandler` — „Signal" im Monitoring reaktiviert dann nur store-only
> (`ReactivateSignal`), advanced also **nicht** im Web; der Backend-Runner treibt voran. Beim
> **Starten** aus eigenem App-Code entsprechend `engine.CreateInstance(...)` nutzen (store-only, der
> Runner nimmt es auf) statt `engine.StartWorkflow(...)`. Auflisten/Detail/Design/`Cancel` bleiben
> unverändert (reine Store-Operationen). Alternativ die Start-/Signal-Aktionen per IPC an den
> Backend-Dienst routen (analog zum Katalog-IPC).

`ExecutionTarget`/`HostTargets` sind in Szenario b nicht nötig — es gibt nur einen ausführenden
Runner, der alles übernimmt.

---

## 5. Szenario c) Web / Web-Engine / Backend-Service

Wie b), aber der **Web-Prozess führt zusätzlich web-eigene Aktivitäten aus** (z.B. Schritte mit
Benutzerinteraktion / im Web-Kontext), während backend-gebundene Schritte an den Backend-Dienst
übergeben werden. Das ist der **verteilte Handoff** aus Phase 3b.

```
┌───────────── Web-Host ─────────────┐        ┌──────────── Backend-Service ────────────┐
│ WorkflowViews (Editor/Monitoring)  │        │ Activity-Plugins (Ziel "backend")        │
│ Activity-Plugins (Ziel "web")      │        │ WorkflowEngine(hostTargets: "backend")   │
│ WorkflowEngine(hostTargets: "web") │  DB    │ WorkflowRunner(Owner "backend")          │
│ WorkflowRunner(Owner "web")        │◄──────►│ PluginActivityCatalog (IPC-exponiert)    │
│ IWorkflowActivityCatalog (IPC →    │  IPC   │                                          │
│   Backend, für ALLE Typen)         │        │                                          │
└───────────────┬────────────────────┘        └───────────────┬──────────────────────────┘
                └────────────── gemeinsame DB ───────────────────┘
```

**Kernidee.** Jede Aktivität trägt ein `ExecutionTarget` (`"web"` oder `"backend"`). Jeder Host-Runner
kennt seine `HostTargets`. Erreicht ein Zweig einen Knoten mit **fremdem** Ziel, **parkt** er
(`WaitingForTarget`); der Runner des passenden Ziels nimmt ihn per Poll (`FindBranchesWaitingForTarget`)
auf und führt ihn aus. So läuft **ein** Workflow abschnittsweise auf verschiedenen Hosts — auch
parallele Zweige echt gleichzeitig.

### Web-Host (Engine **mit** Zielen + Runner)

```csharp
services.AddSingleton<IActivityHost>(sp => new WebToolkitActivityHost(sp)); // web-Aktivitäten hier
services.AddSingleton(sp => new WorkflowEngine(
    sp.GetRequiredService<IWorkflowStore>(),
    sp.GetRequiredService<IActivityHost>(),
    hostTargets: new[] { "web" }));            // ← dieser Host bedient das Ziel "web"

// Web-Runner als HostedService (Owner stabil, EIGEN je Prozess)
services.AddHostedService(sp => new WorkflowRunnerService(
    sp.GetRequiredService<WorkflowEngine>(), sp.GetRequiredService<IWorkflowStore>(),
    new WorkflowRunnerOptions { Owner = "web", WorkerCount = 2, PollTimeMs = 1000 }));

// Katalog: die Web-Engine kennt nur die web-Aktivitäten. Damit der Editor ALLE Typen (web + backend)
// zeigt, den Katalog vom Backend über IPC beziehen (dort ist der volle Satz registriert):
services.AddSingleton<IWorkflowActivityCatalog>(sp =>
    new WorkflowActivityCatalogClient(sp.GetRequiredService<IBaseClient>(), "workflowCatalog"));
```

### Backend-Service (Engine **mit** Zielen + Runner)

```csharp
var engine = new WorkflowEngine(store, new PluginActivityHost(factory),
    hostTargets: new[] { "backend" });         // ← dieser Host bedient das Ziel "backend"
var runner = new WorkflowRunner(engine, store,
    new WorkflowRunnerOptions { Owner = "backend", WorkerCount = 4, PollTimeMs = 1000 });
runner.Start();
// Katalog wie in Szenario b über IPC exponieren.
```

### Autoring & Ablauf

- Im Editor je Aktivität das Feld **„Execution target"** setzen (`"web"` bzw. `"backend"`); leer =
  läuft auf beliebigem Runner.
- Beispiel: `Start → (web) Formular ausfüllen → Timer → (backend) Verbuchung → (web) Bestätigung → End`.
  Der Web-Runner führt die web-Schritte aus und **parkt** die Verbuchung; der Backend-Runner nimmt sie
  auf, führt sie aus und der Zweig läuft (auf dem Web-Runner) mit der Bestätigung weiter.
- **Konsistenz:** In c) beide Hosts über den **Runner** betreiben (nicht über den inline
  `SignalWorkflow`), damit durchgehend das nebenläufige, sperren-basierte Modell gilt — also auch hier
  `SignalDelivery = Runner` setzen. Start/Signal store-only (`CreateInstance` / `ReactivateSignal`)
  auslösen und die Runner voranbringen lassen (siehe Kommando-Tabelle in Szenario b). Ein Zielname
  ohne bedienenden Runner lässt den Zweig
  **unbegrenzt** parken (Betriebs-/Konfig-Sache, kein Fehler; beim Parken auf Report-Ebene geloggt).

---

## 6. Betriebshinweise (alle verteilten Szenarien)

- **`Owner` stabil & eindeutig.** Je Runner-Prozess ein fester Name über Neustarts hinweg; bei
  mehreren Prozessen unterschiedliche Namen. Beim Start räumt ein Runner nur **seine eigenen**
  Alt-Sperren ab (Crash-Recovery ohne Wartefrist). Für einen endgültig toten Runner:
  `store.ReleaseLocksOfOwner(name)` als Admin-Übernahme.
- **Zweig-Sperren** haben **keine TTL** (lange Aktivitäten blockieren nicht). Sie leben, bis sie
  freigegeben oder über den Owner zurückgesetzt werden.
- **Timer-Ansprüche** (`IWorkflowStore.ClaimDueTimers`) haben umgekehrt **immer** eine TTL. Fällige
  Timer stehen im gemeinsamen Store, also sieht sie ohne Anspruch *jeder* Runner: alle laden dieselben
  Instanzen, und alle bis auf einen scheitern danach am Commit. Der Anspruch stempelt die aufgegriffenen
  Timer für `TimerLeaseMs` auf den Owner und macht die Scheiben damit disjunkt.

  Wichtig für das Verständnis: **das ist Lastverteilung, keine Sperre.** Dass ein Timer genau einmal
  feuert, sichert weiterhin allein der Versions-Check beim Commit (`ReactivateTimers` läuft in
  `ReactivateAndCommit`) — er *muss* es, denn ein Anspruch kann ablaufen, während sein Halter noch
  arbeitet. Genau deshalb darf ein abgelaufener Anspruch gefahrlos übernommen werden, und genau deshalb
  ist ein abgestürzter Runner unkritisch. Beide Zusicherungen sind in
  `WorkflowConcurrentTimerTest` festgehalten.

  Ein Commit auf die Instanz beendet den Anspruch sofort — sonst wäre ein **neu gestellter**
  Fristen-Timer bis zum Ablauf des alten Anspruchs unsichtbar und die Eskalation käme zu spät.

  Der In-Memory-Store kennt keine Ansprüche (ein Prozess, dieselben Referenzen) und liefert schlicht
  die fälligen Instanzen.
- **Optimistische Nebenläufigkeit.** Kurze Merge-Sektion pro Instanz über `WorkflowInstance.Version`
  (EF-Concurrency-Token). Die eigentliche Ausführung bleibt parallel.
- **Konflikt-Policy.** Seit den **Zweig-Scopes** (§8) arbeitet jeder parallele Zweig in seiner eigenen
  Kopie — gleichnamige Schreibzugriffe kollidieren also nicht mehr, sondern werden am Join
  zusammengeführt (bei verschiedenen Werten gewinnt der später gespawnte Zweig, mit Warnung im Protokoll;
  entschieden gehört das per Join-Mapping). Der Validator warnt dafür weiterhin schon zur Design-Zeit.
  Der Laufzeit-Fault auf konkurrierende Schreibzugriffe bleibt als Sicherungsnetz für Tokens **ohne**
  Zweig-Scope (Instanzen aus der Zeit davor) bestehen.
- **Migrationen.** Das komplette Schema liegt als **`InitialWorkflow`-Migration** in zwei
  provider-spezifischen Projekten: **`ITVComponents.Workflow.EntityFramework.SqlServer`** und
  **`…PostgreSql`** (je mit `IDesignTimeDbContextFactory<WorkflowContext>`). Der Host wählt beim Aufsetzen
  des `WorkflowContext` den Provider und setzt die **`MigrationsAssembly`** auf das passende Projekt, z.B.
  `UseSqlServer(cs, o => o.MigrationsAssembly("ITVComponents.Workflow.EntityFramework.SqlServer"))`, und
  wendet die Migrationen an (`ctx.Database.Migrate()` bzw. das Deployment-Verfahren des Hosts). Künftige
  Schemaänderungen als weitere Migration **je Provider** generieren (`dotnet ef migrations add … --project
  <Provider-Projekt>`). Bisher gibt es genau eine Folge-Migration: **`BranchScopes`** (die zwei nullable
  Token-Spalten der Zweig-Scopes, §8) — ebenfalls in beiden Provider-Projekten. Tests nutzen weiterhin
  `EnsureCreated` (kein Migrationsbedarf). Fehler-Ausgänge und JSON-Export/Import brauchen **kein** Schema
  (rein im Definition-JSON / normale Variablen).

## 7. Kurzreferenz: Wer macht was?

| | a) Web-Only | b) Web / Backend | c) Web / Web-Engine / Backend |
|---|---|---|---|
| Editor + Monitoring | Web | Web | Web |
| Aktivitäts-Ausführung | Web | Backend | Web **und** Backend (je Ziel) |
| Katalog (`IWorkflowActivityCatalog`) | `PluginActivityCatalog` (in-proc) | IPC-Client → Backend | IPC-Client → Backend |
| `WorkflowRunner` | optional (in-proc) | Backend (`Owner="backend"`) | Web (`Owner="web"`) + Backend (`Owner="backend"`) |
| `WorkflowEngine.hostTargets` | — | — | Web `"web"`, Backend `"backend"` |
| `ExecutionTarget` an Knoten | — | — (optional) | ja (`"web"`/`"backend"`) |
| Start / Signal aus dem Web | inline (`StartWorkflow`/`SignalWorkflow`) | store-only (`CreateInstance`/`ReactivateSignal`) → Backend-Runner | store-only → Runner |
| `WorkflowViewsOptions.SignalDelivery` | `Inline` (Standard) | `Runner` | `Runner` |
| IPC | nein | ja (Katalog) | ja (Katalog) |

---

## 8. Modellier-Features (Kurzüberblick)

Diese Bausteine betreffen das **Autoren** von Workflows (Editor + Definition), nicht das Deployment —
hier nur der Überblick mit den deployment-relevanten Hinweisen:

- **Fehler-Ausgänge (Error-Routing).** Ein `AutomatedActivityNode` kann eine Fehler-Kante
  (`ErrorFlowId`) haben: scheitert die Aktivität (Exception ODER kontrolliert via `ctx.Fail(msg)`),
  nimmt der Token diese Kante statt zu faulten — mit Fehlermeldung (`ErrorVariable`), erhaltenem
  Zwischenstand (die Outputs bei `ctx.Fail`) und Fehlversuchs-Zähler (`AttemptVariable`: +1 je Fehler,
  0 bei Erfolg). Damit lassen sich Retry-Schleifen, Verzweigung nach Fehleranzahl (XOR auf den Zähler)
  und Benutzer-Korrektur (Wait-Knoten im Fehlerpfad) modellieren. Editor: Felder im Aktivitäts-Panel.
  **Keine Schema-Änderung.**

- **Subworkflows (`CallWorkflowNode`).** Ein Workflow ruft einen anderen mit Ein-/Ausgabewerten auf;
  der Aufrufer parkt, bis der Subworkflow endet, und übernimmt dessen Ergebnis. Ein gescheiterter
  Subworkflow faultet standardmäßig den Aufrufer — oder nimmt (wie bei Aktivitäten) einen **Fehler-Ausgang**
  (`ErrorFlowId`/`ErrorVariable`/`AttemptVariable`); mit Zähler führt eine Fehlerkante zurück zum Knoten den
  Subworkflow erneut aus (frische Kind-Instanz je Versuch). Abbruch kaskadiert auf laufende Kinder. Der
  Subworkflow ist eine **eigene Instanz**
  (eigenes Monitoring), erbt den Tenant, ist beliebig verschachtelbar. **Getrieben vom Runner** — reines
  inline `StartWorkflow` ohne Runner treibt Kinder NICHT (Web-Only hostet den Runner in-proc, wie
  empfohlen). Aggregierte History: Kind-Einträge tragen die `RootInstanceId` des Elternbaums → das
  Protokoll des ganzen Baums ist über die eine Wurzel lesbar. Editor: Palette „Subworkflow" +
  Config-Panel (Definition/Version + Bindungen).

- **Signatur und Ergebnis eines Workflows (`StartNode.Inputs` / `EndNode.Outputs`).** Der Start-Knoten
  deklariert die **Parameter** des Workflows: jede Bindung (Konstante = Vorgabewert, Variable =
  durchreichen/umbenennen, CScript = berechnen) wird beim Anlegen der Instanz gegen die *übergebenen*
  Startwerte aufgelöst und ergibt eine Instanz-Variable. Mit `ScopeMode = Replace` wird die Signatur
  **strikt**: die Instanz startet mit genau den deklarierten Parametern plus `RetainVariables`, alles
  andere Übergebene fällt weg. Der End-Knoten deklariert spiegelbildlich das **Ergebnis**: ist
  `Outputs` gesetzt, besteht der Variablenstack beim Übergang auf `Completed` genau daraus (plus
  `RetainVariables`) — der Re-Base passiert beim Statuswechsel, nicht beim Verbrauch des Tokens, damit
  bei parallelen Zweigen nicht der erste ankommende den Stack der anderen abräumt.
  Der Start-Knoten trägt zusätzlich die **Start-Maske** (`FormFields`/`FormDescription`) für den Start
  von Hand über die Oberfläche — siehe [§10](#10-einen-workflow-von-hand-starten).
  Beides gilt für **jeden** Einstieg: ein als Subworkflow aufgerufener Workflow wendet seine eigene
  Signatur auf die Werte an, die die `Inputs` des `CallWorkflowNode` liefern, und was der Aufrufer als
  Ergebnis sieht, ist genau das deklarierte Result — die Aufruferseite bleibt dadurch unverändert
  (keine zweite Ablage, **keine Schema-Änderung**). Leer gelassen verhält sich alles wie bisher
  (übergebene Werte = Stack, kompletter Stack = Ergebnis), bestehende Definitionen sind also
  unberührt. Signatur und Ergebnis gehören der Definition, nicht dem Knoten: sie dürfen nur an *einem*
  Start- bzw. End-Knoten stehen — der Validator meldet Mehrfach-Deklaration als Fehler. Editor:
  Panels „Start parameters" und „Result".

- **Genau ein Start- und ein End-Knoten.** Damit Signatur und Ergebnis überhaupt eindeutig sein können,
  hat eine Definition genau **einen** Start- und **einen** End-Knoten; der Validator meldet mehrere als
  Fehler, und die Palette bietet Start/End nur an, solange keiner existiert. Mehrere Start-Knoten waren
  bisher ein *impliziter* Parallelstart (jeder bekam ein Token) — das leistet ein AND-Split hinter dem
  einen Start, und zwar sichtbar. Die Engine startet Altdefinitionen weiterhin (mit allen Start-Tokens),
  schreibt dabei aber eine Warnung ins Log. **Keine Schema-Änderung**; betrifft nur Definitionen, die
  bisher schon mehrdeutig waren.

- **Mapping auf der Verbindung (`SequenceFlow.Inputs`).** Auch eine *Kante* kann Bindungen tragen: sie
  beantworten „wie sieht der Variablen-Stack aus, wenn ein Token **hier ankommt**". Damit sind es zwei
  Ebenen — die **Kante** normalisiert (typisch, wenn mehrere Pfade denselben Knoten mit unterschiedlich
  benannten Werten erreichen), die **Aktivität** zieht aus dem Stack ihre Parameter. Reihenfolge an einem
  XOR: erst wählt die `Condition` die Kante, dann greift *deren* Mapping — das Mapping der nicht
  genommenen Kante läuft nicht. `ScopeMode = Replace` (+ `RetainVariables`) konsolidiert wie am Knoten —
  innerhalb einer parallelen Region wirkt das nur auf die Kopie des eigenen Zweigs (siehe Zweig-Scopes).
  Gilt auf allen Wegen gleich, auch auf den Zweig-Kanten eines AND-Splits (dort schreibt jede Kante in
  ihre eigene Zweig-Kopie). Leer gelassen = bisheriges
  Verhalten, **keine Schema-Änderung** (Kanten stecken im Definitions-JSON). Editor: Panel „Connection" →
  „Mapping on arrival"; im Diagramm trägt eine Kante mit Mapping ein `{…}` vor ihrer Beschriftung.

- **Zweig-Scopes (paralleler Datenfluss).** Ein **AND-Split** gibt jedem Strang eine eigene **Kopie** des
  Variablen-Stacks (`Token.Variables`); alles, was der Zweig danach liest und schreibt, läuft in dieser
  Kopie. Der Instanz-Stack bleibt während der Region auf dem Stand des Splits. Der zugehörige **Join**
  führt die Kopien wieder zusammen: ohne Deklaration fließt alles nach oben, was die Zweige geschrieben
  haben (bisheriges Verhalten); mit `ParallelGatewayNode.Outputs` (+ `ScopeMode`/`RetainVariables`) kommt
  **genau das Deklarierte** aus der Region heraus — dieselbe Extend/Replace-Semantik wie an Aktivität,
  Subworkflow, Start und Kante. Aktivitäten merken davon nichts: `ctx.Variables` zeigt auf den Scope des
  eigenen Zweigs. Verschachtelte Splits fallen Ebene für Ebene zurück (jedes Token merkt sich in
  `SplitTokenId`, aus welchem Split es stammt).
  *Verhaltensänderung:* Ein Zweig sieht **nicht mehr**, was ein Nachbarzweig schreibt, sondern den Stand
  vom Split — schreibend war das ohnehin verboten (es faultete), lesend war es bisher ein Wettlauf.
  Dafür entfällt die Fehlerklasse „paralleler Schreibkonflikt": schreiben zwei Zweige denselben Namen,
  gewinnt beim Merge deterministisch der später gespawnte Zweig, und der Fall landet als
  `BranchMergeConflict` (Warnung) im Protokoll und im Log — entschieden gehört er per Join-Mapping (je
  Zweig ein eigener Ergebnisname). Ein Zweig, der ins **Ende** statt in seinen Join läuft, verliert seine
  Variablen (Warnung zur Laufzeit *und* im Validator). **Schema-Änderung:** zwei nullable Spalten auf
  `Tokens` (`VariablesJson`, `SplitTokenId`) → Migration `BranchScopes` je Provider-Projekt; laufende
  Instanzen bleiben gültig (beide Spalten null = Verhalten wie bisher). Editor: Panel am parallelen
  Gateway („Result of the parallel region", nur wenn es als Join wirkt); im Diagramm trägt ein Join mit
  deklariertem Ergebnis ein `{…}`, und das Monitoring zeigt den Zweig-Scope je Token.

- **Benutzer-Aufgaben (`UserActivityNode`).** Ein Schritt, den ein **Mensch** erledigt: der Zweig parkt,
  bis die Aufgabe in der Oberfläche abgeschlossen wird. Technisch wartet das Token wie an einem
  Wartepunkt, fachlich ist es etwas anderes (Zuständigkeit, Maske, definierter Abschluss) — deshalb ein
  eigener Knoten und ein eigener Abschlussweg. Details, Rechte und Host-Verdrahtung: **Abschnitt 9**.

- **JSON Export/Import.** `ITVComponents.Workflow.Serialization.WorkflowJson` (öffentlich) ist das
  kanonische, portable Format (stabile `"kind"`-Diskriminatoren, **typnamen-unabhängig**) — dasselbe,
  das der Store persistiert. `ExportDefinition` / `ImportDefinition`; im Editor Export-/Import-Buttons
  (Import ersetzt die Zeichenfläche und validiert, speichert aber nicht automatisch). Ermöglicht Teilen
  als Datei, Versionierung (Git), Transport zwischen Umgebungen und Tooling.

- **Protokoll mit Schweregrad.** Das Ausführungsprotokoll liegt als eigene Tabelle (`HistoryEntryRow`,
  append-only) mit `Severity` (Verbose/Info/Warning/Error) — filter-/abfragbar; das Monitoring-Detail
  zeigt eine farbige Severity-Spalte. Bei Subworkflows über die `RootInstanceId` baumweit aggregierbar.
  Wie *viel* davon überhaupt geschrieben wird, steuert der Protokoll-Filter — siehe
  [§15](#15-wie-gesprächig-das-ablauf-protokoll-ist).

- **Parallele Iteration einer Aktivität (`AutomatedActivityNode.Iteration`).** Ein Aktivitäts-Knoten
  kann seine Aktivität **je Element einer Sammlung** ausführen, wahlweise mehrere gleichzeitig — für den
  einen langen Schritt in einem sonst seriellen Ablauf. Details: [§14](#14-eine-aktivität-über-eine-sammlung-parallelisieren).
  **Keine Schema-Änderung** (steckt im Definitions-JSON).

- **Ereignisbasiertes Gateway, Terminate, Message/Signal.** Ein Rennen zwischen mehreren Wartepunkten,
  ein Abbruch der ganzen Instanz, und die Trennung gerichtete Nachricht gegen Rundruf. Details:
  [§17](#17-ereignisse-rennen-rundruf-und-abbruch). **Schema-Änderung:** drei Spalten auf `Tokens`.

- **Dringlichkeit einer Instanz (`WorkflowInstance.Priority`).** Bestimmt, in welcher Reihenfolge die
  Hintergrund-Verarbeitung Instanzen aufgreift. Details: [§13](#13-dringlichkeit-priorität-von-instanzen).
  **Schema-Änderung:** eine Spalte `Priority` auf `WorkflowInstances` (+ Index) → Migration
  **`InstancePriority`** je Provider-Projekt. Laufende Instanzen bleiben gültig (Vorgabewert `Normal`).

## 9. Benutzer-Aufgaben und Arbeitsliste

Ein `UserActivityNode` hält den Prozess an, bis ein **Mensch** handelt. Er ist bewusst kein erweiterter
`WaitNode`: eine Aufgabe hat eine Zuständigkeit, eine Maske und einen definierten Abschluss — und der
Abschluss darf **nicht** über ein Signal laufen (`SignalWorkflow` weckt *alle* gleichnamig wartenden
Tokens und schreibt ohne Versionsvergleich; bei zwei parallelen Aufgaben derselben Art wäre beides
falsch). Der Weg ist `WorkflowEngine.CompleteUserTask(instanceId, tokenId, result, completedBy)` —
nebenläufigkeits-sicher über `TryCommitInstance`, mit einem eigenen Ausgang für „war schon erledigt".

### Was am Knoten steht

| Feld | Bedeutung |
| --- | --- |
| `TaskKey` | Die Aufgabenart (Pflicht). Filter der Arbeitsliste und Ausweich-Schlüssel für die Maske. |
| `RequiredPermission` | Wer diese **Sorte** Aufgabe sehen und erledigen darf. Leer = das allgemeine Aufgaben-Recht genügt. |
| `Assignment` | CScript → Benutzername. **Einmal** beim Parken ausgewertet und am Token festgeschrieben (eine Arbeitsliste ist eine Datenbankabfrage und kann kein Skript auswerten). Leer = Pool-Aufgabe. |
| `ViewKey` | Optionaler Schlüssel der Oberflächen-Komponente. **Nie ein Typname** — Definitionen sind DB-Daten, ein Typname darin wäre Code-Ausführung per Datenpflege. |
| `Title` / `Description` | Klartext **oder** Kultur-JSON (`{"de":"Freigabe","fr":"Approbation"}`) — dieselbe Konvention wie bei Navigations-Einträgen. |

| `Inputs` / `Outputs` | Was die Maske sieht bzw. zurückgibt (dieselbe Bindungs-Maschinerie wie an der Aktivität, inkl. `ScopeMode`/`RetainVariables`). |
| `FormFields` | Deklaration der **generischen Maske**. Leer = die Aufgabe wird nur bestätigt. |
| `DueInHours` | Frist ab dem Parken — reine Anzeige-/Sortierinformation. |

Zwei Dinge, die man nicht verwechseln darf: **Permission** = siehst du diese Sorte Aufgabe,
**Assignment** = ist dieser konkrete Fall deiner.

Ein Zuweisungs-Ausdruck, der scheitert, **faultet die Instanz** — bewusst: die Aufgabe läge sonst im Pool
und wäre für jeden mit der Permission sichtbar, also eine stille Sichtbarkeits-Ausweitung. Ein
scheiterndes `FormatData` ist dagegen nur eine Log-Zeile (der Titel bleibt unformatiert) — eine
unerledigbare Aufgabe wäre die teurere Folge.

Die Frist landet in `Token.TaskDueUtc`, **nicht** in `DueUtc`: letzteres ist die Timer-Fälligkeit, und der
Timer-Aufgriff würde eine überfällige Aufgabe kurzerhand selbst weiterlaufen lassen. Soll eine Frist
etwas *auslösen*, gehört ein `TimerNode` in einen parallelen Zweig.

### Die Maske

Aufgelöst wird in dieser Reihenfolge: `ViewKey` → `TaskKey` → generische Maske aus `FormFields`. Ein
Schlüssel, den niemand registriert hat, fällt ebenfalls auf die generische Maske zurück — aber mit
Log-Zeile, nicht still. Registriert wird im Host:

```csharp
services.ConfigureWorkflowTaskViews(cfg => cfg.RegisterTaskView<ApproveInvoice>("ApproveInvoice"));
```

Die Komponente liest ihren Zustand über `[CascadingParameter] WorkflowTaskContext TaskContext`
(Payload, Feld-Deklaration, übersetzte Texte). Bewusst per Cascading und nicht über ein
Parameter-Dictionary einer `DynamicComponent`: dessen Parameternamen werden erst zur Laufzeit geprüft,
und dieselbe Komponente läuft so unverändert im Registry-Weg **und** direkt auf einer eigenen Seite.

#### Der Vertrag: `IUserTaskView`

Eine registrierte Maske **muss** `IUserTaskView` erfüllen — verlangt schon beim Übersetzen
(`RegisterTaskView<T>() where T : IComponent, IUserTaskView`), nicht erst beim Öffnen des Dialogs:

```csharp
public interface IUserTaskView
{
    Task<UserTaskViewResult> ResolveActivityAsync();
}
```

Mehr ist nicht zu tun. Die Maske **zeigt an und liefert auf Zuruf ihre Ausgabewerte**:

```csharp
public Task<UserTaskViewResult> ResolveActivityAsync()
{
    if (string.IsNullOrWhiteSpace(comment) && rejected)
    {
        return Task.FromResult(UserTaskViewResult.Incomplete("Bitte begründen."));
    }

    return Task.FromResult(UserTaskViewResult.Complete(new Dictionary<string, object>
    {
        { "approved", !rejected },
        { "comment", comment }
    }));
}
```

Wohin diese Werte wandern, entscheidet das `Outputs`-Mapping des Knotens — die Maske muss die Variablen
des Prozesses nicht kennen. `Incomplete()` **ohne** Meldung heisst „die Maske hat die fehlenden Stellen
selbst markiert"; eine zusätzliche Einblendung wäre dann nur Lärm. Und `Complete(null)` ist ein völlig
gültiges Ergebnis — eine Aufgabe, die nur bestätigt wird, hat keine Ausgabewerte. Genau deshalb ist die
Antwort ein eigener Typ und kein `Dictionary?`, bei dem `null` beides bedeuten müsste.

#### Der Rahmen gehört dem Mantel — für jede Maske gleich

`UserTaskDialog` stellt Titelzeile und Fusszeile mit **„Erledigen"/„Schliessen"**, und zwar unabhängig
davon, ob innen die generische oder eine eigene Maske steht. „Erledigen" ruft `ResolveActivityAsync()`
und schliesst mit dem Ergebnis ab. Die generische Maske erfüllt denselben Vertrag — es gibt im Mantel
also nur **einen** Abschlussweg.

Das ist keine Kosmetik: die Fusszeile ist **angeheftet**, der Inhalt scrollt darunter (siehe
`itv-mudblazor.css`). Eine Maske, die ihre eigenen Knöpfe mitbrächte, legte sie damit *in* den
scrollenden Bereich — auf einem schmalen Gerät unter Umständen ausserhalb des Bildes, und der Benutzer
könnte die Aufgabe nicht abschliessen. Genau dieser Fall war die MLM-Meldung zur Aufgabenliste.

Der teure Teil war ohnehin nie das Anzeigen, sondern der Abschluss: Ergebnis-Mapping, weiche Sperre,
Doppel-Klick, Versionskonflikt und „war schon erledigt". Der liegt einmal in der Bibliothek.

> **BREAKING (ab PRE157).** Masken, die bisher ihre eigenen Aktionen mitbrachten und
> `TaskContext.CompleteAsync(result)` selbst riefen, übersetzen nicht mehr: `RegisterTaskView<T>`
> verlangt jetzt `IUserTaskView`. Die Umstellung ist mechanisch — die eigenen Knöpfe entfallen, ihr
> Rumpf wird zu `ResolveActivityAsync`. `TaskContext.CompleteAsync` bleibt für den Sonderfall bestehen,
> dass eine Maske von sich aus abschliessen will (z.B. nach einer eigenen Rückfrage).

### Arbeitsliste, Rechte und Sperre

Die Seite ist `Workflow/Tasks` („Meine Aufgaben"), gegated durch die neue Permission **`Workflow.Tasks`**
neben `Workflow.Monitor`/`Operate`/`Design`. Sie ist absichtlich getrennt: wer Rechnungen freigibt,
braucht deswegen keinen Blick in fremde Instanzen.

**Serverseitig geprüft, nicht nur im Razor-Wrapper.** `IWorkflowTaskHandler` prüft in *jeder* Methode
(`services.VerifyUserPermissions`), und `GetTaskAsync`/`ClaimAsync`/`CompleteAsync` prüfen zusätzlich,
dass der Token zu einer für **diesen** Benutzer sichtbaren, offenen Aufgabe des eigenen Tenants gehört —
sonst genügte das Erraten einer Token-Id.

Der **Tenant wird explizit gefiltert** (`IPermissionScope.PermissionPrefix`), nicht dem globalen
Query-Filter überlassen: `TokenRow` hat keinen, und ob der Instanz-Filter greift, entscheidet die
Registrierung des Kontexts im Host (der Weg über die DbContext-Factory ist bewusst filterfrei). Eine
Arbeitsliste darf davon nicht abhängen.

**Weiche Sperre.** Beim Öffnen wird die Aufgabe für 15 Minuten als „wird bearbeitet" markiert
(`ClaimedBy`/`ClaimedUntil`). Sie blockiert **nicht** — die harte Entscheidung fällt weiterhin am
Versionsvergleich des Abschlusses — sondern warnt den zweiten Bearbeiter, bevor er die Arbeit doppelt
macht. Bewusst eigene Spalten und **nicht** `WorkflowBranchLockRow`: dessen `ReleaseLocksOfOwner` räumt
die Sperren eines Runners auf und würde einen Oberflächen-Claim mitreißen.

**Deep-Link** für Benachrichtigungs-Mails: `Workflow/Tasks?task={instanceId}:{tokenId}` — **relativ**
(ohne führenden Slash), sonst 404 außerhalb des Tenants.

**Schema-Änderung:** neun nullable Spalten auf `Tokens` (`TenantId`, `TaskKey`, `TaskPermission`,
`AssignedTo`, `TaskTitle`, `TaskCreatedUtc`, `TaskDueUtc`, `ClaimedBy`, `ClaimedUntil`) plus die Indizes
`(TenantId, TaskKey, Status)` und `(AssignedTo)` → Migration **`UserTasks`** je Provider-Projekt.
Denormalisiert, weil `TaskKey`/Zuständigkeit/Titel sonst im Definitions-JSON steckten und es ohne die
Spalten kein serverseitiges Filtern, Sortieren oder Paginieren gäbe. Laufende Instanzen bleiben gültig
(alle Spalten null = kein Aufgaben-Token).

### Lokalisierung

Zwei Ebenen, die getrennt bleiben:

- **Rahmen** (Spaltenköpfe, Knöpfe, Meldungen) = `IStringLocalizer<WorkflowTaskMessages>` gegen
  `Resources/WorkflowTaskMessages.{de|fr|it}.resx` (neutral = Englisch). Nur die Aufgaben-Seite ist
  lokalisiert; Monitoring und Editor sind Betreiber-Werkzeuge und bleiben englisch.
- **Inhalte** (Aufgabentitel, Feldbeschriftungen) = Kultur-JSON in der Definition, aufgelöst mit
  `StringExtensions.Translate` beim **Anzeigen**. Am Token steht der Titel **unaufgelöst** — sonst
  bestimmte die Kultur des ausführenden Runners die Sprache des Lesers. Der Kultur-Fallback ist
  `de-CH` → `de` → Schlüssel `Default` → Rohwert; ein kaputter Datensatz wird angezeigt *und* geloggt,
  und der Validator meldet ihn schon beim Speichern.

---

## 10. Einen Workflow von Hand starten

Bis hierhin entstand eine Instanz nur aus Code (`engine.CreateInstance` / `StartWorkflow`). Es gibt jetzt
zusätzlich einen **Weg über die Oberfläche**: `Workflow/Instances` → **New instance**. Die Maske dafür
steht nicht im Code, sondern **in der Definition** — genau wie bei den Benutzer-Aufgaben.

### Die Start-Maske am Knoten

Der `StartNode` hat zwei neue, rein beschreibende Eigenschaften:

| Eigenschaft | Bedeutung |
|---|---|
| `FormFields` (`List<UserTaskField>`) | Die Felder, die ein Mensch beim Start ausfüllt. **Dieselbe** Feldbeschreibung wie `UserActivityNode.FormFields` (Name, Label, Kind, Required, HelpText, Choices) — für „Formular aus Daten" gibt es damit nur eine Sprache und nur einen Renderer (`UserTaskFieldsForm`). |
| `FormDescription` (`string`) | Optionale Anleitung über den Feldern. Klartext oder Kultur-JSON, wie die Aufgaben-Titel. |

Zwei Eigenschaften des Feldes sind beim Start **ohne Bedeutung** und werden ignoriert: `ReadOnly` und
`PayloadName`. Beide beziehen sich auf den Payload einer laufenden Aufgabe — beim Start gibt es noch
keinen. Der Feld-Dialog blendet sie für den Start-Knoten deshalb aus.

**Die Engine liest die Felder nicht.** Sie sind eine Zusage der Oberfläche, kein Vertrag: ein
programmatischer Start bleibt unverändert möglich und übergibt seine Werte direkt. Wer Werte
*erzwingen* will, deklariert sie zusätzlich in der Signatur (`StartNode.Inputs`).

### Wie Maske und Signatur zusammenspielen

Das ist die Stelle, an der man sich vertun kann — die beiden Ebenen sind bewusst getrennt:

```
Eingabe der Maske ──► übergebene Startvariablen ──► StartNode.Inputs (Signatur) ──► Instanz-Variablen
   FormFields[].Name  =  Name der übergebenen Variable        auflösen/umbenennen/berechnen
```

Der **Feldname der Maske ist der Name der übergebenen Variable** — also genau das, wogegen die Signatur
anschließend aufgelöst wird. Ohne Signatur landen die Eingaben unverändert als Instanz-Variablen.

Achtung bei **strikter Signatur** (`ScopeMode = Replace`): dann überlebt nur, was in `Inputs` (oder
`RetainVariables`) steht — ein Feld, das dort fehlt, ist unmittelbar nach dem Start weg. Der Editor
rechnet das aus und warnt namentlich; der Start-Dialog weist zusätzlich darauf hin.

### Rechte

Neue Permission **`Workflow.Start`** neben `Monitor`/`Operate`/`Design`/`Tasks`. Bewusst nicht
`Operate` mitbenutzt: einen laufenden Prozess anzustoßen oder abzubrechen ist Betrieb an etwas
Bestehendem — einen neuen Geschäftsfall zu eröffnen ist eine fachliche Handlung, die typisch andere
Leute dürfen. Sie **ergänzt** `Workflow.Monitor` (der Knopf sitzt in der Instanz-Übersicht), ersetzt es
also nicht.

Serverseitig geprüft, nicht nur im Razor-Wrapper: alle drei neuen Handler-Methoden
(`ListStartableDefinitionsAsync`, `GetStartFormAsync`, `StartInstanceAsync`) rufen
`services.VerifyUserPermissions` selbst auf.

### Deployment-Verhalten

Der Start folgt derselben Weggabelung wie die Signal-Zustellung (`WorkflowViewsOptions.SignalDelivery`):

| | `Inline` (Web-Only) | `Runner` (Split / Multi-Tenant) |
|---|---|---|
| Aufruf | `engine.StartWorkflow(...)` | `engine.CreateInstance(...)` |
| Wirkung | anlegen **und** synchron bis zum ersten Wartepunkt treiben | anlegen; der Runner nimmt die aktiven Start-Tokens beim nächsten Poll auf |

Danach wird — best effort — `IWorkflowWorkerWake.Poke(environment, tenantId)` gerufen, damit ein im
selben Prozess laufender Worker nicht bis zum Max-Linger wartet. Ohne Worker-Betrieb ist der Service
nicht registriert → stiller No-op.

### Der Tenant der neuen Instanz

Die Engine kennt **keinen** Tenant-Parameter. Der Store schreibt beim Anlegen
`instance.TenantId ?? ctx.CurrentTenant` fest — und ob der Workflow-Kontext von sich aus einen Tenant
hat, entscheidet die **Registrierung im Host**: der Weg über die `IDbContextFactory` ist bewusst
filterfrei und liefert `null`.

Der Start-Handler setzt deshalb **explizit** den ambienten Ausführungs-Scope, bevor er die Engine ruft:

```csharp
string tenant = services.GetService<IPermissionScope>()?.PermissionPrefix?.ToLower();
using (WorkflowExecutionScope.UseTenant(tenant)) { /* CreateInstance / StartWorkflow */ }
```

Das ist derselbe Mechanismus, mit dem der tenant-übergreifende Runner jede Instanz unter *ihrem*
Tenant vorantreibt (`WorkflowEngine.RunBranch`). Er gewinnt gegen die Kontext-Registrierung und deckt
zugleich den **Inline-Vortrieb** ab, der im Web-Only-Betrieb unmittelbar nach dem Anlegen Aktivitäten
ausführt.

**Warum das zwingend ist:** `WebToolkitActivityHost.OpenScope(instance)` öffnet seinen Plugin-Scope aus
`instance.TenantId`. Eine tenant-los angelegte Instanz scheitert später mit
`Fuer den ActivityRef 'X' konnte im Tenant '(none)' kein Workflow-Aktivitaets-Plugin aufgeloest werden.`
Bleibt der Tenant trotzdem leer (Ein-Mandanten-Host), wird das als Warnung protokolliert statt still
hingenommen.

**Ebenfalls explizit gefiltert** — und nicht dem Query-Filter überlassen — wird der Tenant in der
Startauswahl und beim Laden der Start-Maske: dieselbe Begründung wie bei der Arbeitsliste (§9). Tenant-lose
Definitionen sind öffentlich und bleiben im Kontext jedes Tenants startbar; `StartInstanceAsync` prüft
die Zugehörigkeit vor dem Anlegen noch einmal selbst, damit das Erraten einer fremden Definition-Id
nicht genügt.

### Was in der Auswahl erscheint

Gelistet wird je Definition-Id nur die **höchste Version** (die, die ein Start ohnehin erwischt), und
davon nicht: für den Start gesperrte Definitionen (`DisabledForStart`, vom Validator beim Speichern
gesetzt) und solche ohne Start-Knoten. Beide würden beim Klick nur mit einer Ausnahme quittieren.

Schlägt der Start doch fehl (Definition zwischenzeitlich gesperrt, Start-Parameter nicht auflösbar),
zeigt der Dialog **den Grund** aus der Engine-Ausnahme — nicht bloß „hat nicht geklappt" — und
protokolliert ihn zusätzlich.

**Keine Schema-Änderung, keine Migration.** `FormFields`/`FormDescription` stecken im Definitions-JSON;
alte Definitionen deserialisieren mit leerer Feldliste und verhalten sich unverändert.

---

## 11. Fehlgeschlagene Instanzen wieder aufnehmen (Retry)

In der Instanz-Übersicht hat eine **fehlgeschlagene** Instanz einen Retry-Knopf: Daten korrigieren und
den Schritt, an dem es scheiterte, erneut ausführen.

### Warum das ohne Zurückspulen funktioniert

Beim Fault passiert weniger, als man denkt: `WorkflowEngine.Fault` setzt `Status = Faulted`, schreibt
`FaultMessage` und einen Protokolleintrag — **der Token bleibt aktiv auf seinem Knoten stehen**. Er wird
weder bewegt noch verbraucht. Der Wiederaufsatzpunkt ist also bereits da; `RetryFaulted` muss ihn nur
wieder freigeben:

```csharp
engine.RetryFaulted(instanceId, variableUpdates, note);   // Status -> Running, Variablen korrigiert
```

Danach läuft der Vortrieb wie überall (Runner oder `Advance`) und führt **genau diesen** Schritt erneut
aus. Es wird nichts zurückgespult und nichts übersprungen — bereits abgeschlossene Schritte laufen nicht
noch einmal.

Den Punkt bestimmt `WorkflowEngine.FindRetryPoint`: der Knoten aus dem letzten `Faulted`-Eintrag. Das ist
nötig, weil bei parallelen Zweigen mehrere Tokens aktiv geblieben sein können — der Vortrieb bricht ab,
sobald *ein* Zweig faultet. Dieselbe Methode benutzt die Oberfläche für die Anzeige, damit sie nicht
etwas anderes behauptet, als der Retry dann tut.

### Wohin die Korrekturen gehen

In den Scope **des fehlgeschlagenen Tokens** (`WorkflowEngine.ScopeOf`) — innerhalb einer parallelen
Region ist das der Zweig-Scope, sonst der Instanz-Scope. Also genau dorthin, wo die Aktivität beim
nächsten Versuch liest; eine Korrektur im Instanz-Scope käme in einem Zweig sonst nie an.

Die Maske zeigt die Variablen dieses Scopes, erlaubt das **Anlegen neuer** (der häufigste Fall ist eine
*fehlende* Variable) und schickt nur, was tatsächlich angefasst wurde. Zusammengesetzte Werte
(Objekte/Listen) sind read-only — sie im Textfeld zu bearbeiten wäre Raten. Die Rückübersetzung ist
typ-erhaltend (`WorkflowVariableValue`), weil CScript typ-empfindlich ist: `amount > 0` braucht eine
Zahl, keinen Text.

### Rechte, Tenant, Deployment

- Permission **`Workflow.Operate`** — kein eigenes Recht: das ist ein Eingriff an etwas Bestehendem,
  genau wie Signal und Abbruch.
- Der Tenant der Instanz wird **explizit** geprüft (`MayTouch`), damit das Erraten einer Instanz-Id
  nicht genügt. Der Vortrieb läuft unter dem Tenant **der Instanz**, nicht dem der Anfrage.
- Gleiche Weggabelung wie sonst: `Inline` advanced direkt im Web-Prozess (der Benutzer sieht sofort, ob
  die Korrektur reichte), `Runner` lässt den Zweig vom Runner aufnehmen. Danach `Poke`.

### Parallele Zweige

Ein Fault stoppt **den ganzen Prozess**, nicht nur den fehlgeschlagenen Zweig — die Vortriebs-Schleife
läuft nur, solange die Instanz nicht `Faulted` ist. Was das für den Nachbarzweig heißt, hängt davon ab,
wo er gerade stand:

| Zustand von Zweig A beim Fault in B | Nach dem Retry |
|---|---|
| noch **aktiv** (irgendwo in seiner Kette) | läuft weiter, wo er stand — nichts wird wiederholt |
| bereits am Join **geparkt** (`Joining`) | bleibt geparkt und wartet auf B; läuft **nicht** noch einmal |
| noch gar nicht gelaufen | startet jetzt |

`RetryFaulted` fasst dabei **keine Tokens an** — es setzt nur den Status zurück. Die anderen Zweige
resümieren also von selbst, weil ihre Tokens ohnehin noch aktiv (bzw. wartend) sind. Im
Runner-Betrieb reiht der Poll alle aktiven Tokens einer `Running`-Instanz wieder als Zweig-Tasks ein
(`FindRunnable` liefert nur `Running`, deshalb ruht eine gefaultete Instanz vorher komplett).

**Sequenziell vs. nebenläufig:** im Inline-/`Advance`-Betrieb laufen Zweige nicht verschränkt, sondern
einer nach dem anderen bis zu seiner nächsten Barriere. Ob A beim Fault von B schon durch ist oder noch
gar nicht lief, entscheidet daher schlicht die Reihenfolge der Split-Kanten. Im Runner-Betrieb laufen
sie echt parallel; ein Zweig, der währenddessen fertig wird, committet sein Delta noch (Joins und
Endstatus werden bei gefaulteter Instanz aber nicht mehr aufgelöst).

Die Korrektur landet im Scope **des fehlgeschlagenen Zweigs** — der Nachbarzweig hat seinen eigenen und
sieht sie nicht. Das ist Absicht: der Join führt die Scopes anschließend zusammen.

#### Wenn *mehrere* Zweige gescheitert sind

Das kann nur im **Runner-Betrieb** passieren (sequenziell bricht der Vortrieb beim ersten Fault ab, der
zweite Zweig läuft dann gar nicht erst). `RunBranch` hat keine Faulted-Sperre: ein bereits laufender
Zweig führt seinen Schritt zu Ende und committet, auch wenn die Instanz inzwischen gefaultet ist. Es
können also mehrere Tokens auf je eigener Fehlerstelle stehen.

**Ein Retry stößt alle wieder an** — wieder, weil er keine Tokens anfasst: alle bleiben aktiv, der
Runner reiht alle wieder ein. Belegt in `TwoFaultedBranches_BothTokensStayActive_AndBothRestartOnRetry`
über Ausführungszähler.

**Korrigiert wird je Zweig.** `WorkflowEngine.FindStalledBranches` liefert *alle* stehen gebliebenen
Zweige (gescheiterte zuerst, zuletzt gemeldeter Fehler vorne), und
`RetryFaultedBranches(instanceId, updatesByTokenId)` nimmt einen eigenen Satz Korrekturen **je
Token-Id** entgegen — die Token-Id ist der Zweig. Ein Durchgang genügt also auch bei mehreren kaputten
Zweigen (`TwoFaultedBranches_PerBranchCorrections_FixBothInOneGo`).

Der einfache Aufruf `RetryFaulted(instanceId, variableUpdates)` bleibt bestehen und meint den
*Wiederaufsatzpunkt* — praktisch für den Normalfall mit genau einer Fehlerstelle. Er ist damit der
Sonderfall des allgemeinen Wegs, nicht eine zweite Mechanik.

Unbekannte Token-Ids werden protokolliert und übergangen: ein Zweig kann zwischen Anzeige und Absenden
weitergelaufen sein, und das darf den Wiederaufsatz nicht scheitern lassen
(`RetryWithUnknownTokenId_IsIgnored_AndDoesNotBlockTheResume`).

Beachte: `instance.FaultMessage` trägt nur den **zuletzt** gemeldeten Fehler (jeder Commit überschreibt
ihn) — die einzelnen Meldungen stehen in der History und werden von dort je Zweig herausgesucht.

#### In der Oberfläche

- Die **Graph-Ansicht** des Instanz-Details markiert alle Fehlerstellen rot (`FaultedNodeIds`, gewinnt
  gegen die blaue Token-Hervorhebung). Ein **Doppelklick** auf einen Knoten öffnet die Korrektur-Maske
  direkt auf diesem Zweig.
- Die **Korrektur-Maske** hat einen Reiter je stehen gebliebenem Zweig, jeder mit *seinem* Scope. Ein
  Klick auf „Retry all branches" schickt alle Korrekturen zusammen.
- Zweige ohne eigenen Fehler erscheinen ebenfalls (sie kamen nur nicht mehr dran) — mit einem Hinweis,
  dass sie beim Retry ohnehin weiterlaufen und Korrektur dort optional ist.
- Der Retry-Knopf in der Instanz-Liste bleibt als Schnellweg und öffnet dieselbe Maske ohne Vorauswahl.

Die Klickbarkeit kommt aus `wwwroot/workflow-graph.js`: das SVG wird als `MarkupString` erzeugt, kann
also keine Blazor-Handler tragen. Das Modul hängt **einen** Listener an den Container und ordnet über
`closest('[data-wf-node]')` zu — das überlebt jedes Neu-Rendern des SVG-Körpers. Ohne JS bleibt der
Graph als Anzeige nutzbar (der Ausfall wird protokolliert).

Beides ist als Test festgehalten (`WorkflowRetryTest.ParallelFault_*`), inklusive der Zusicherung, dass
jede Aktivität genau einmal läuft — nur die fehlgeschlagene zweimal.

### Aufgeben: Abbrechen einer fehlgeschlagenen Instanz

`CancelWorkflow` akzeptiert **auch `Faulted`**. Eine fehlgeschlagene Instanz ist nicht beendet, sondern
hängt — ihre Tokens stehen noch auf ihren Knoten. Wer den Wiederaufsatz aufgibt, muss den Fall
schließen können, sonst bliebe er für immer in der Übersicht liegen. Der Protokolleintrag hält die
ursprüngliche Fehlermeldung fest (`given up after: …`), weil sie nach dem Statuswechsel sonst nirgends
mehr sichtbar wäre. Beendete Instanzen (`Completed`, `Cancelled`) bleiben wie bisher unverändert.

### Abgrenzung zum Fehler-Ausgang

Der **Fehler-Ausgang** (`ErrorFlowId`, §8) ist der *modellierte* Weg: erwartete Fehler, im Graphen
behandelt, mit Zähler und Retry-Schleife. Der Retry-Knopf ist der *unerwartete* Fall — der Prozess ist
bereits gefaultet, ein Mensch schaut hin. Wer denselben Fehler regelmäßig von Hand repariert, sollte ihn
stattdessen modellieren.

**Nicht wiederaufsetzbar** sind Fehler, die an keinem Schritt hängen (kein aktives Token, z.B. eine nicht
mehr ladbare Definition). Der Dialog sagt das mit Grund, statt einen willkürlichen Punkt zu wählen.

**Keine Schema-Änderung, keine Migration.**

---

## 12. Fristen am Schritt (Boundary-Timer) und Eskalation

`UserActivityNode.DueInHours` ist reine Anzeige-Information — eine überfällige Aufgabe läuft davon
**nicht** weiter. Wer will, dass eine Frist etwas *auslöst*, hängt einen **`BoundaryTimerNode`** an den
Schritt.

> **Korrektur zur früheren Empfehlung.** In älteren Ständen stand hier „für Eskalation einen `TimerNode`
> in einen parallelen Zweig". Das ist **falsch** und sollte nicht mehr so gebaut werden: der AND-Join
> feuert erst, wenn auf *jeder* eingehenden Kante ein Token liegt — der Hauptfluss hinge also bis zum
> Ablauf der Frist, selbst wenn die Aufgabe längst erledigt ist. Und die Eskalation liefe unbedingt, denn
> nach dem Split hat jeder Strang seine eigene Scope-Kopie: der Timer-Zweig kann gar nicht prüfen, ob die
> Aufgabe fertig ist.

### Was der Boundary-Timer tut

- Er **hängt** an einem Schritt (`AttachedToNodeId`) und wird scharf, wenn das Token dort **parkt** —
  Benutzer-Aufgabe, Subworkflow-Aufruf, oder Aktivität mit `ExecutionTarget`. An einem Schritt, den das
  Token synchron durchläuft, könnte er nie feuern; der Validator warnt.
- Läuft die Frist ab, entsteht ein **zusätzliches Token** auf seiner ausgehenden Kante — der
  **Nebenpfad**. Der Hauptfluss läuft unverändert weiter und wartet auf nichts.
- Der Nebenpfad arbeitet auf einer **Kopie** des Scopes des Haupt-Tokens. Was er schreibt, fliesst
  **nicht** zurück.
- Zieht das Haupt-Token weiter, werden Timer **und** ein noch laufender Nebenpfad verworfen.

### Wiederholung

`Deadlines` ist eine Liste von **Ausdrücken**, die der Reihe nach abgearbeitet wird: die erste Frist zählt
ab dem Parken, jede weitere ab der vorigen Auslösung. Ist die Liste durch, schweigt der Timer — es sei
denn, `RepeatLast` ist gesetzt: dann wird die letzte Frist endlos wiederholt („danach alle 2 Stunden").
`CountVariable` bekommt die Nummer der Auslösung (1 beim ersten Mal) in den Scope des Nebenpfads, damit
die dritte Mahnung anders klingen kann als die erste.

Jede Frist ist ein CScript-Feld mit Modus-Schalter (Ausdruck oder Block mit `return`, siehe §16) und darf
**dreierlei** liefern — dieselbe Konvention wie beim gewöhnlichen `TimerNode`, um eine Kurzform erweitert:

| Ergebnis | Bedeutung | Beispiel |
| --- | --- | --- |
| Zahl | Dauer in **Stunden** | `24`, `tageBisFrist * 24` |
| `TimeSpan` | Dauer | `'System.TimeSpan'.FromHours(36)` |
| `DateTime` | absoluter Zeitpunkt | `faelligAm` |

Statische Aufrufe schreibt CScript mit dem **Typnamen in Anführungszeichen**; `TimeSpan.FromHours(36)`
ohne sie läuft auf einen Aufruf gegen null und scheitert zur Laufzeit.

Eine Dauer von **null oder weniger** ist ein Fehler, kein „sofort" — zusammen mit `RepeatLast` feuerte sie
endlos. Aus demselben Grund passt ein **absoluter Zeitpunkt nicht zu `RepeatLast`**: er bliebe für immer
derselbe und wäre ab der zweiten Runde vergangen. Die Engine lässt den Timer dann verstummen (Warnung im
Log und in der Historie), statt in einer Schleife zu feuern.

**Wenn ein Fristen-Ausdruck scheitert** (Tippfehler, fehlende Variable), hängt es an der Art des Timers:
ein **nicht unterbrechender** Timer wird nicht scharf, der Grund landet in Log *und* Instanz-Historie
(`BoundaryTimerFailed`, Severity Error) — der Schritt selbst läuft normal weiter, denn eine tadellose
Aufgabe wegen einer kaputten Erinnerung abzuschiessen wäre schlimmer als die fehlende Erinnerung. Ein
**unterbrechender** Timer dagegen ist die einzige Ausstiegstür des Schritts; fällt er aus, faultet die
Instanz, statt für immer stehenzubleiben.

> **Altbestand:** Das frühere Feld `IntervalsInHours` (reine Stundenzahlen) wird weiterhin **gelesen** —
> bereits gespeicherte Definitionen laufen unverändert. Beim Öffnen im Editor wird es einmalig nach
> `Deadlines` übernommen (aus `24` wird der Ausdruck `24`), und das nächste Speichern schreibt die neue
> Form. Zu entfernen war es nicht: die Definitionen liegen als JSON in der Datenbank, ein verschwundenes
> Feld hätte bestehende Timer still verstummen lassen.

### Nebenpfad-Ende

Ein Nebenpfad darf den **Workflow nicht beenden**. Er endet deshalb an einem **`SidePathEndNode`**: das
Token wird verbraucht, sonst passiert nichts — kein Ergebnis-Re-Base, kein Beitrag zum Abschluss. Ohne
diesen Knoten scheiterte der letzte Schritt des Nebenpfads an der Regel „genau eine ausgehende Kante",
und ein regulärer End-Knoten würde das Ergebnis der ganzen Instanz festschreiben, obwohl nur die
Eskalation durchgelaufen ist. Der Validator prüft, dass der Nebenpfad den End-Knoten nicht erreichen kann.

### Unterbrechend

`Interrupting = true` dreht die Bedeutung um: statt eines Nebenpfads nimmt das **Haupt-Token** die Kante.
Der Schritt gilt damit als abgebrochen, eine wartende Aufgabe verschwindet aus der Arbeitsliste
(„Frist verstrichen → automatisch abgelehnt"). Das feuert naturgemäss **einmal** — danach steht das Token
woanders; weitere Intervalle und `RepeatLast` haben keine Wirkung, der Validator weist darauf hin. Da der
Pfad hier der Hauptfluss ist, **darf** er zum End-Knoten führen.

### Technisch

Scharfgestellt wird über `Token.DueUtc` — also über den bestehenden, **indizierten** Timer-Aufgriff
(`ClaimDueTimers`/`ReactivateTimers`). Kein neuer Sweep, keine neue Abfrage, und der Runner nimmt es ohne
Änderung auf. `Token.TaskDueUtc` bleibt was es war: Anzeige und Sortierung der Arbeitsliste.

Zwei Stellen tragen die Lebensdauer: `Token.BoundaryOwnerTokenId` (Timer **und** Nebenpfad-Tokens zeigen
auf ihr Haupt-Token) und `Token.BoundaryIteration` (der Zähler am wartenden Timer). Aufgeräumt wird
zweifach — eifrig in `MoveToken`, sobald das Haupt-Token weiterzieht, und als Sicherheitsnetz in
`UpdateTerminalStatus` für die Wege, auf denen ein Haupt-Token *ohne* Bewegung verschwindet (Ende, Join,
Abbruch). Ohne das Netz bliebe ein wartendes Timer-Token stehen und die Instanz könnte nie abschliessen.

**Schema-Änderung:** zwei nullable Spalten auf `Tokens` (`BoundaryOwnerTokenId`, `BoundaryIteration`) →
Migration **`BoundaryTimers`** je Provider-Projekt. Laufende Instanzen bleiben gültig (beide Spalten null
= kein Boundary-Token).

**Schema-Änderung (Timer-Ansprüche):** zwei weitere nullable Spalten auf `Tokens`
(`TimerLeaseOwner`, `TimerLeaseUntilUtc`) → Migration **`TimerLease`** je Provider-Projekt. Ebenfalls
rückwärtsverträglich: null = kein Anspruch, und ein abgelaufener wird ohnehin überholt. Der
`TimerLeaseOwner` trägt `<Owner>#<Aufruf-Guid>` — der Owner-Teil, damit `ReleaseLocksOfOwner` ihn beim
Neustart findet, die Guid, damit ein Aufgriff exakt zurücklesen kann, welche Zeilen *er* bekommen hat.
Nicht zu verwechseln mit `ClaimedBy`/`ClaimedUntil` auf derselben Zeile: das ist die weiche Sperre der
**Oberfläche** auf einer Benutzer-Aufgabe.

## 13. Dringlichkeit (Priorität) von Instanzen

Nicht jeder Workflow ist gleich eilig. Eine nächtliche Aufräum-Kaskade darf warten; eine Freigabe, an
deren Ende ein Kunde auf eine Antwort wartet, nicht. `WorkflowInstance.Priority` ist genau diese Angabe.

**Kleinere Zahl = wichtiger** — dieselbe Konvention wie `ITVComponents.ParallelProcessing.ITask.Priority`,
damit der Wert unverändert durchgereicht werden kann. Die benannten Stufen stehen in `WorkflowPriority`:

| Stufe | Wert | gedacht für |
| --- | --- | --- |
| `Highest` | 0 | drängt sich vor allem anderen vor |
| `High` | 1 | Abläufe mit wartendem Menschen |
| `Normal` | 2 | **der Standard** |
| `Low` | 3 | Fleißarbeit |
| `Lowest` | 4 | Hintergrund-Massenläufe |

### Woher die Stufe kommt

1. Was der Starter angibt (`StartWorkflow`/`CreateInstance`-Parameter, Start-Dialog) — gewinnt.
2. Sonst die Vorgabe der Definition (`WorkflowDefinition.DefaultPriority`, Designer → Zahnrad
   „Workflow settings").
3. Sonst `Normal`.

Ein **Subworkflow erbt die Stufe seines Aufrufers**, nicht seine eigene Vorgabe: der Aufrufer wartet auf
ihn, ein langsamerer Unterschritt würde also genau den dringenden Prozess ausbremsen.

Nachträglich ändern: `WorkflowEngine.SetPriority(instanceId, priority)` bzw. im Monitoring-Detail
(Recht `Workflow.Operate`). Die Änderung läuft über den versionsgeprüften Commit — verliert sie das
Rennen gegen einen gerade laufenden Zweig, liefert sie `false` und will wiederholt werden. Der Vorgang
landet als `PriorityChanged` im Ablauf-Protokoll.

### Was die Stufe bewirkt — je nach Betriebsart

**Immer** (beide Betriebsarten): der Store liefert lauffähige und fällige Instanzen **nach Dringlichkeit
sortiert**, und der Poll reiht sie in dieser Reihenfolge ein. Beim gedeckelten Timer-Batch
(`MaxTimerBatch`) bekommen die dringenden die Plätze — wer abgeschnitten wird, ist der unwichtigste.

**Web-Worker (`ITVComponents.Workflow.WebWorker`):** vollständig. Der Antrieb arbeitet eine nach
Dringlichkeit sortierte Arbeitsliste ab; Folge-Zweige erben die Stufe ihres Auslösers. Kein Kompromiss,
keine Einstellung nötig.

**Runner (`ITVComponents.Workflow.ParallelProcessing`):** hier ist das Überholen *in der Warteschlange*
eine **bewusste Zuschaltung**. Der `ParallelTaskProcessor` führt je Stufe eine eigene Warteschlange und
gewichtet sie stark (eine Stufe bekommt `((niedrigste − stufe) + 1)³` Plätze im Auswahl-Zyklus). Der Preis
steht in `TaskProcessor.Work`: der Worker wartet **je Platz 50 ms**. Die Zahl der Plätze ist damit direkt
die Aufgriffs-Verzögerung:

| Band | Plätze | Verzögerung bis ein Auftrag drankommt |
| --- | --- | --- |
| 1 Stufe (**Standard**) | 1 | ~50 ms |
| 2 Stufen, z.B. `Normal`..`Low` | 9 | ~0,5 s |
| 3 Stufen, `High`..`Low` | 36 | ~1,8 s |
| 5 Stufen, `Highest`..`Lowest` | 225 | **~11 s** |

Deshalb ist der Standard `HighestPriority = LowestPriority = WorkflowPriority.Normal` — eine
Warteschlange, unverändertes Zeitverhalten. Wer das Überholen will, nimmt **zwei bis drei benachbarte
Stufen**:

```csharp
new WorkflowRunnerOptions
{
    HighestPriority = WorkflowPriority.High,   // 1
    LowestPriority  = WorkflowPriority.Low,    // 3  -> 36 Plaetze, ~1,8 s
    WorkerCount = 4
}
```

Die Faustregel: die Verzögerung muss **klein gegen die Dauer eines Workflow-Schritts** sein. Für Läufe,
die minutenlang rechnen, sind 1,8 s nichts; für Instanzen, die im Sekundentakt Schritte machen, ist es
viel — dort lieber beim Ein-Stufen-Band bleiben und sich auf die Einreihungs-Reihenfolge verlassen.

Eine Stufe **außerhalb** des konfigurierten Bandes wird darauf beschnitten (`WorkflowPriority.Clamp`) —
eine Instanz aus einer Umgebung mit anderem Band soll laufen, nicht scheitern. Zu beachten außerdem: der
Processor teilt seine Worker in Bänder auf, die unwichtigste Stufe wird von genau **einem** Worker
bedient. Bei einem breiten Band und wenigen Workern ist das gewollt, aber es heißt auch: die
niedrigste Stufe hat keine Nebenläufigkeit mehr.

### Schema

Eine Spalte auf `WorkflowInstances` — SQL siehe [§16](#16-migration-die-priority-spalte).

## 14. Eine Aktivität über eine Sammlung parallelisieren

Der typische Fall: ein Ablauf ist über weite Strecken seriell, aber **ein** Schritt verarbeitet 1000
Dateien. Ein paralleles Gateway ist dafür das falsche Werkzeug — es modelliert eine feste Zahl fachlich
*verschiedener* Stränge, und jedes der 1000 Elemente kostete eine Token-Zeile, eine Zweig-Sperre und
einen Commit.

`AutomatedActivityNode.Iteration` (`ActivityIteration`) macht stattdessen aus **einem** Knoten eine Serie:
die Aktivität läuft einmal je Element, wahlweise mehrere Elemente gleichzeitig. Der Zweig bleibt **ein**
Zweig; für die Persistenz ist der Knoten derselbe atomare Schritt wie eine gewöhnliche Aktivität (ein
Absturz mittendrin wiederholt ihn ganz).

| Feld | Bedeutung |
| --- | --- |
| `ItemsInput` | Name des **Eingabeparameters**, dessen aufgelöster Wert die Sammlung ist. Muss eine Eingabe-Bindung des Knotens sein. |
| `ItemParameter` | Unter welchem Parameter das einzelne Element ankommt. Leer = unter `ItemsInput` (die Aktivität sieht statt der Sammlung ein Element). Anderer Name = die Sammlung bleibt zusätzlich sichtbar. |
| `IndexParameter` | Optional: der 0-basierte Index. |
| `MaxParallel` | 1 (Standard) = streng nacheinander. 0 oder kleiner = so viele wie Prozessorkerne. |
| `ContinueOnError` | Aus: erster Fehler bricht ab. An: alle Elemente werden versucht, der Knoten scheitert am Ende. |
| `FailedItemsOutput` | Die **Fehler**: je gescheitertem Element ein `IterationFailure` (Element + Ursache). Diagnose. |
| `PendingItemsOutput` | Alles **noch Offene**: gescheiterte **plus** nie versuchte. Der richtige Retry-Eingang. |
| `ItemResultOutput` | Welcher Ausgabeparameter des Einzeldurchlaufs das **fertige Element** ist. |
| `SucceededItemsOutput` | Die **erfolgreich verarbeiteten** Elemente (die `ItemResultOutput`-Werte), lückenlos. |
| `CarryOverInput` | Eingabeparameter, dessen Elemente den neu erfolgreichen **vorangestellt** werden. |
| `SucceededCountOutput` | Anzahl der in **diesem** Durchlauf erfolgreichen Elemente (ohne Übernahme). |

### Die Formen: Wiederanlauf ≠ Diagnose ≠ Ergebnis

Die drei Listen sind bewusst **verschieden geformt**, und genau das lässt eine Wiederholungs-Schleife
zusammenpassen:

- `PendingItems` trägt die **Original-Eingabewerte** — sie müssen wieder in die Sammlung passen, aus der
  iteriert wird. **Das ist der Retry-Eingang, immer.**
- `SucceededItems` trägt die **Ergebnisse** (`ItemResultOutput`; ohne dessen Angabe die Eingabe-Elemente).
- `FailedItems` trägt je Fehler ein `IterationFailure` — die **Diagnose**-Sicht. Weil der Wiederanlauf
  über `PendingItems` läuft, darf diese Liste eine reichere Form haben:

| Feld | Inhalt |
| --- | --- |
| `Item` | das unveränderte Element aus der Eingabe |
| `Index` | seine Position in der Eingabe-Sammlung |
| `Message` | die Meldung — aus `ctx.Fail(…)` oder `Exception.Message` |
| `ExceptionType` | Typname der geworfenen Ausnahme, oder **`null` bei kontrolliertem `Fail`** |
| `ExceptionDetail` | die ausgeschriebene Ausnahme inkl. Stacktrace, sonst `null` |
| `WasThrown` | abgeleitet: `ExceptionType != null` |

Der Unterschied „abgelehnt" (`ctx.Fail`) gegen „abgestürzt" (Exception) ist bei der Fehlersuche der
wichtigste — deshalb ist er an `ExceptionType`/`WasThrown` ablesbar und nicht nur an der Meldung.

Die Ausnahme steht bewusst als **Daten** drin und nicht als Objekt: die Fehlerliste landet über die
Ausgabe-Bindung in einer Variable und damit im JSON des Commits. Ein `Exception`-Objekt ließe den
scheitern (`System.Text.Json` stolpert über `Exception.TargetSite`) — ausgerechnet dann, wenn die Arbeit
schon getan ist.

### Wenn die Schleife zwischendurch parkt

Ein Wiederholungs-Flow hat fast immer eine Wartestelle zwischen den Versuchen — eine Benutzer-Aufgabe
(„diese 3 ansehen"), einen Timer, ein Signal. **Die Fehlerkante selbst ist keine solche Stelle**: sie wird
immer erst genommen, wenn die *ganze* Iteration durch ist, und der Zweig läuft danach ohne Unterbrechung
weiter, bis er auf einen Wartepunkt trifft. Bis dahin sind alle Listen echte Objekte.

Beim **Parken** werden die Variablen committet und damit als JSON abgelegt. Damit dabei nicht aus jedem
Datensatz ein namenloses Dictionary wird, legt der Store den Variablen-Stack **je Variable mit einer
eigenen Typkennung** ab. Ein Datensatz-Typ übersteht den Park typtreu, sobald er **angemeldet** ist:

```csharp
// einmal beim Start der Anwendung:
WorkflowJson.RegisterVariableType<SignItem>("sign-item");
```

Der Kurzname gehört zum abgelegten Format und darf sich nicht mehr ändern — die **Klasse dahinter darf
umziehen, umbenannt werden, die Assembly wechseln**. Genau darum ist es ein Kurzname und kein
`AssemblyQualifiedName`: eine laufende Instanz überlebt damit ein Refactoring. Sammlungen brauchen keine
eigene Anmeldung; eine Liste oder ein Array angemeldeter Elemente wird über den Elementnamen abgelegt und
kommt als **Array** zurück. Grundtypen (`string`, `int`, `DateTime`, `Guid`, …) sind ab Werk angemeldet,
`IterationFailure` ebenfalls.

Beim Lesen gilt eine **Positivliste**: aus der Instanz-Tabelle wird kein beliebiger .NET-Typ geladen,
auch wenn im Datenstrom einer benannt ist. Was sich nicht auflösen lässt, geht trotzdem **nicht
verloren** — es kommt untypisiert zurück (Liste, `Dictionary`, Primitive, wie bisher), und der Grund
steht im Log. Ein Wert ist also entweder typisiert oder brauchbar, nie weg.

Konsequenz für die Iteration: ihre Ausgabe-Listen sind **Arrays** — typisiert, wenn alle Elemente
denselben Laufzeittyp haben, sonst `object[]`. Damit trägt eine Ergebnis-Sammlung ihren Elementtyp in die
Ablage, statt ihn als `List<object>` zu verlieren.

Was Sie trotzdem wissen sollten: **innerhalb** eines nicht angemeldeten Datensatzes bleiben
`object`-Felder untypisiert. Wer so etwas braucht, macht es wie `IterationFailure` und implementiert
`IManualSerializer` — dann trägt jedes Feld seine eigene Typkennung.

Für den Rest steht `WorkflowJson.Materialize(value)` bereit, das aus einem rohen JSON-Knoten wieder
Liste/`Dictionary`/Primitive macht.

`PendingItems` statt `FailedItems` in den Retry zu geben ist kein Detail: bei `ContinueOnError = false`
bricht der Lauf beim ersten Fehler ab, und von 1000 Elementen können 1 gescheitert, 4 fertig und **495
nie versucht** sein. Wer nur die gescheiterten wiederholt, verliert die 495 lautlos. Mit
`ContinueOnError = true` sind beide Listen identisch. Der Validator warnt, wenn der Abbruch-Modus mit
`FailedItemsOutput`, aber ohne `PendingItemsOutput` konfiguriert ist.

### Die Wiederholungs-Schleife

`CarryOverInput` löst das Problem, dass jeder Durchlauf des Knotens seine Ausgabe-Variablen **setzt** und
nicht merged — Durchlauf 2 mit 3 Elementen würde die 997 aus Durchlauf 1 sonst überschreiben. Die
übernommene Liste wird ausschließlich `SucceededItems` vorangestellt; alle anderen Ergebnis-Listen bleiben
streng „was DIESER Durchlauf erzeugt hat".

```
Knoten "sign", Fehlerkante zeigt auf sich selbst zurück:

  Inputs:    todo        ← Variable Items        (Iterations-Sammlung)
             alreadyDone ← Variable Signed       (Übernahme)
             JobId       ← Variable ID           (geht unverändert an JEDEN Einzeldurchlauf)
  Iteration: ItemsInput = todo, ItemParameter = ItemToProcess,
             ItemResultOutput = ProcessedItem,
             SucceededItemsOutput = Done, PendingItemsOutput = StillOpen,
             FailedItemsOutput = Problems,
             CarryOverInput = alreadyDone, ContinueOnError = true
  Outputs:   Done      → Signed
             StillOpen → Items
             Problems  → LastErrors    (nur für Anzeige/Protokoll, nicht für den Wiederanlauf)

Durchlauf 1: Items = 1000  → Signed = 997 Ergebnisse, Items = 3 Originale  → Fehlerkante
Durchlauf 2: Items = 3     → Signed = 997 + 3 = 1000, Items = leer         → Erfolgskante
```

Zwei Punkte, die der Validator als Fehler meldet, weil sie sonst still das Falsche tun:
`CarryOverInput` auf einen nicht gebundenen Parameter (verlöre bei jedem Versuch alles Vorige) und
`CarryOverInput == ItemsInput` (würde fertige Elemente erneut verarbeiten).

Ein **fehlender** Eingabeparameter ist immer ein Modellierungsfehler — auch bei der Übernahme. Eine
gebundene, aber noch nicht gesetzte Variable steht als `null` in den Inputs und gilt als leere Liste;
das ist der normale erste Durchlauf. „Gar nicht gebunden" faultet dagegen.

Schreibt ein erfolgreicher Durchlauf den deklarierten `ItemResultOutput` nicht, entsteht ein `null` in
der Erfolgsliste — das meldet die Engine gesammelt als `IterationResultMissing` (Warnung), statt es
stehen zu lassen.

### Ergebnisse

Jeder Ausgabeparameter, den irgendein Element gesetzt hat, wird zu einer **Liste in Eingabe-Reihenfolge**
(nicht Fertigstellungs-Reihenfolge — sonst wäre das Ergebnis eines parallelen Laufs von Mal zu Mal anders
sortiert). Elemente ohne Wert stehen als `null` drin, damit die Positionen zur Eingabeliste passen.
Abgebildet wird wie immer über die `Outputs`-Bindungen des Knotens.

### Zwei Dinge, die man wissen muss

**Die Aktivität muss thread-sicher sein**, sobald `MaxParallel > 1` ist. Die Engine löst sie **einmal**
auf und ruft dieselbe Instanz aus mehreren Threads (ein Plugin wird unter seinem Namen geteilt — es je
Element neu zu laden wäre bei 1000 Elementen keine Option). Zustand gehört in lokale Variablen, nicht in
Felder. Genau deshalb ist `MaxParallel = 1` der Standard: Parallelität ist ein bewusster Griff.

**Schreibzugriffe auf `ctx.Variables` werden verworfen.** Jeder Element-Lauf arbeitet auf einer *Kopie*
des Variablen-Scopes — welcher von 1000 Läufen hätte sonst recht? Ergebnisse gehören in `ctx.Outputs`,
die sammelt die Engine ein. Still passiert das nicht: die erkannten Namen landen als
`IterationVariablesDiscarded` (Warnung) im Ablauf-Protokoll und im Log.

### Fehler

Ein fehlgeschlagenes Element (Exception **oder** `ctx.Fail`) macht den Knoten zu einem gescheiterten
Aktivitäts-Knoten — mit **Fehler-Ausgang** (`ErrorFlowId`) nimmt der Token diese Kante, ohne faultet die
Instanz. Die **Teilergebnisse werden in beiden Fällen übernommen**, auch beim Abbruch: sie sind das, was
den Fehlerpfad brauchbar macht. Zusammen mit `ContinueOnError` + `FailedItemsOutput` ergibt das das
naheliegende Muster — 997 Dateien signiert, 3 nicht, die drei gehen über die Fehlerkante in eine
Wiederholung oder auf den Tisch eines Menschen.

Der Validator meldet: unbekannter Sammlungs-Parameter (Fehler), Element- und Index-Parameter gleich
benannt (Fehler), `ContinueOnError` ohne Fehler-Ausgang (Warnung), leerer Iterations-Block (Warnung).

Editor: Reiter „Iteration" am Aktivitäts-Panel.

## 15. Wie gesprächig das Ablauf-Protokoll ist

Die Engine schreibt je Knoten zwei `Verbose`-Einträge (`Entered`/`Completed`), dazu `Mapped`,
`Parameters` und mehr. Bei einem Workflow mit vielen Knoten und vielen Instanzen füllt das die
`HistoryEntries`-Tabelle mit Zeilen, die niemand liest. `IWorkflowHistoryFilter` entscheidet, was
überhaupt geschrieben wird — analog zu den Filtern eines `ITVComponents.Logging`-Log-Ziels.

Der Filter greift auf der **Schreib**-Seite: ein herausgefilterter Eintrag entsteht gar nicht erst und
wird damit auch nicht persistiert.

```csharp
// Einmal beim Start der Anwendung - der uebliche Griff:
WorkflowHistoryFilter.Default = new WorkflowHistoryFilter
{
    MinSeverity = HistorySeverity.Info    // die Schritt-fuer-Schritt-Eintraege fallen weg
};
```

| Einstellung | Wirkung |
| --- | --- |
| `MinSeverity` | Mindest-Stufe. Standard `Verbose` = alles (bisheriges Verhalten). |
| `SuppressedEvents` | Sperrliste von Ereignis-Namen, `*` als Platzhalter (`"Boundary*"`). |
| `AllowedEvents` | Nicht leer = Positivliste; nur diese Ereignisse werden geschrieben. |
| `AlwaysLogFrom` | Ab dieser Stufe passiert ein Eintrag **immer**. Standard `Error`. |

**Fehler kommen immer durch** — auch bei der schärfsten Konfiguration. Ein stillgelegtes Protokoll darf
nicht dazu führen, dass eine gefaultete Instanz keine Spur hinterlässt.

Drei Ebenen, die letzte gewinnt:

1. `WorkflowHistoryFilter.Default` — prozessweit, der Ort für die Anwendungs-Einstellung.
2. Der Filter der Engine (`new WorkflowEngine(store, activities, historyFilter: …)`). Der Web-Worker
   nimmt dafür ein registriertes `IWorkflowHistoryFilter` aus der DI, wenn es eines gibt.
3. `WorkflowDefinition.MinHistorySeverity` — übersteuert die Mindest-Stufe für **diese** Definition
   (Designer → Zahnrad „Workflow settings" → „Execution log detail"). Damit lässt sich ein einzelner
   Workflow ausführlich mitschreiben, während der Rest knapp bleibt.

## 16. Migration: die `Priority`-Spalte

Die einzige Schema-Änderung dieser drei Features: eine Spalte `Priority` auf `WorkflowInstances` plus der
Index `(Status, Priority)` → Migration **`InstancePriority`** je Provider-Projekt (SqlServer und
PostgreSql), wie bei `BranchScopes`, `UserTasks`, `BoundaryTimers` und `TimerLease`. Der Host zieht sie
mit seinem üblichen `Migrate()`; nichts von Hand nötig.

Der entscheidende Teil steckt im `DEFAULT (2)`: `0` ist die **höchste** Stufe, ohne Vorgabewert bekämen
ausgerechnet alle Alt-Instanzen Vorfahrt. Bestehende Zeilen erhalten den Wert über den `DEFAULT`, ein
zusätzliches `UPDATE` ist nicht nötig. Rückwärtsverträglich: laufende Instanzen bleiben gültig und laufen
auf `Normal` weiter.

Wer sein Schema **nicht** über die Provider-Migrationen zieht (eigenes Deployment, DBA-Skript), nimmt
dies:

**SQL Server:**

```sql
ALTER TABLE [WorkflowInstances]
    ADD [Priority] int NOT NULL CONSTRAINT [DF_WorkflowInstances_Priority] DEFAULT (2);
GO
CREATE INDEX [IX_WorkflowInstances_Status_Priority]
    ON [WorkflowInstances] ([Status], [Priority]);
GO
```

**PostgreSQL:**

```sql
ALTER TABLE "WorkflowInstances"
    ADD COLUMN "Priority" integer NOT NULL DEFAULT 2;
CREATE INDEX "IX_WorkflowInstances_Status_Priority"
    ON "WorkflowInstances" ("Status", "Priority");
```

`2` ist `WorkflowPriority.Normal`. Der Index bedient die Sortierung des Aufgriffs („die dringendsten der
laufenden zuerst").

## 17. Ereignisse: Rennen, Rundruf und Abbruch

Drei Bausteine, die zusammen den häufigsten Rest an Ereignis-Logik abdecken. Alle drei sind
**rückwärtsverträglich**, bis auf eine bewusste Ausnahme (siehe „Was sich ändert" am Ende).

### Ereignisbasiertes Gateway — „was zuerst kommt, gewinnt"

`EventGatewayNode` wartet auf mehrere Ereignisse gleichzeitig; das erste, das eintrifft, gewinnt, die
übrigen werden verworfen. Umgesetzt als **Rennen echter Tokens**: das Gateway verbraucht sein Token und
setzt je Ausgang ein Kind-Token auf den dahinterliegenden Wartepunkt.

Das ist der Grund, warum es keinen Sonderweg im Store braucht — es sind ganz gewöhnliche wartende Tokens,
und die Aufgriffs-Abfragen für Signale und Timer bleiben unverändert. Im Monitoring stehen alle Kandidaten
nebeneinander.

Hinter jedem Ausgang muss ein Knoten stehen, der **wartet** (Wait, Timer, User task) — der Validator
lehnt alles andere als Fehler ab. Eine Aktivität liefe sofort durch und gewänne immer.

Die Zweige bekommen **keine** eigenen Variablen-Kopien wie bei einem AND-Split: es überlebt genau einer,
es gibt nichts zusammenzuführen.

### Terminate — die ganze Instanz beenden

`TerminateEndNode` verwirft alle Tokens und bricht laufende Subworkflows ab; die Instanz gilt danach als
regulär beendet (`Completed`), nicht als abgebrochen. Beliebig viele je Definition — er zählt nicht als
*der* eine End-Knoten.

Er trägt ein eigenes **Result**, und das sollte man setzen: sonst endet der Workflow ohne jede Aussage
darüber, warum. Quelle ist der Scope des **terminierenden Zweigs** (er wird dafür in den Instanz-Scope
veröffentlicht) — innerhalb einer parallelen Region steht der Instanz-Stack noch auf dem Stand des Splits,
und nur der abbrechende Zweig kennt den Grund.

### Message gegen Signal

Bisher gab es nur „ein Wartepunkt wartet auf einen Namen", und wer wen erreicht, entschied allein der
Aufrufer. Jetzt sagt es der **Knoten**:

| `WaitKind` | Bedeutung |
| --- | --- |
| `Message` (Standard) | Gerichtet. Erreicht nur den Wartepunkt, zu dem die Zustellung korreliert. **Ohne passenden Schlüssel kommt sie nicht an.** |
| `Signal` | Rundruf. Erreicht jeden gleichnamigen Wartepunkt in jeder laufenden Instanz, ohne Korrelation. |

Dazu wurde der **Korrelationsschlüssel des Wartepunkts** überhaupt erst wirksam. `CorrelationExpression`
war zwar deklariert und im Editor sichtbar, wurde aber **nirgends ausgewertet** — korreliert wurde
ausschließlich über den Schlüssel der Instanz, und der steht beim Anlegen fest. Jetzt wird der Ausdruck
beim **Parken** ausgewertet und am Token abgelegt. Damit lässt sich auf etwas korrelieren, das der Prozess
selbst gerade erzeugt hat, und dieselbe Instanz kann an mehreren Stellen auf verschiedene Schlüssel warten.
Der Schlüssel des Wartepunkts schlägt den der Instanz — er ist der spezifischere.

Ein Ausdruck, der nicht auswertbar ist, **faultet** die Instanz. Ein Wartepunkt, dessen Schlüssel sich
nicht berechnen lässt, wäre nie erreichbar, und das fände man erst, wenn die Nachricht ausbleibt.

Die API:

```csharp
engine.SignalWorkflow(instanceId, name, payload, correlationKey);  // gezielt an eine Instanz
engine.DeliverSignal(name, correlationKey, payload);               // korreliert über alle Instanzen
engine.BroadcastSignal(name, payload);                             // Rundruf
runner.Signal(instanceId, name, payload, priority, correlationKey);
runner.Broadcast(name, payload);                                   // fächert auf N Aufträge auf
```

Der Rundruf über den Runner erzeugt bewusst **einen Auftrag je Instanz**: jeder bekommt seinen eigenen
versionsgeprüften Commit und seine eigene Stufe. Ein Rundruf kann tausende Instanzen treffen — eine
davon, die gerade anderweitig committet, darf die restlichen nicht mitreißen.

### Was sich ändert

**`DeliverSignal(name)` ohne Korrelationsschlüssel erreicht keine Message-Wartepunkte mehr**, sondern nur
noch Rundruf-Wartepunkte. Vorher traf ein schlüsselloser Aufruf **jede** Instanz, die auf den Namen
wartete — zwei Vorgänge desselben Musters weckten einander. Wer das wirklich will, sagt es jetzt mit
`BroadcastSignal` und stellt die betroffenen Wartepunkte auf `WaitKind.Signal`.

### Schema

Drei nullable Spalten auf `Tokens` (`RaceTokenId`, `WaitingCorrelation`, `WaitingKind`) plus zwei Indizes
→ Migration **`EventGatewayAndSignalKind`** je Provider-Projekt. Laufende Instanzen bleiben gültig: alle
drei Spalten null bedeutet „kein Rennen, kein eigener Schlüssel, gerichtete Nachricht" — also das
bisherige Verhalten.

## 18. Eingebettete Abschnitte (Subprozess)

Ein `SubProcessNode` gruppiert mehrere Schritte zu einem **Abschnitt**: eigene Knoten im *selben* Graphen,
eigener Variablen-Scope — aber **keine eigene Instanz**.

Der Unterschied zum `CallWorkflowNode` ist der Preis. Eine Kind-Instanz kostet eine eigene Zeile, eigenes
Monitoring, eigene Versionsbindung und einen Rück-Link. Das ist richtig, wenn der Teilablauf für sich
steht (eigene Definition, eigene Version, wiederverwendbar) — und zu viel, wenn er nur ein *Abschnitt*
desselben Prozesses ist.

### Der eigentliche Gewinn: eine Frist über mehrere Schritte

Der Subprozess-Knoten **parkt**, während innen gearbeitet wird. Damit erfüllt er `CanHost`, und ein
Fristen-Timer lässt sich an den **ganzen Abschnitt** hängen: „die komplette Prüfung muss in 48 Stunden
durch sein". Vorher ließ sich das nicht modellieren — nur je Einzelschritt.

Unterbricht die Frist, werden **alle inneren Tokens** verworfen (rekursiv, samt geschachtelter Abschnitte
und deren eigener Fristen).

### Flach im Modell

Die Zugehörigkeit hängt am Kind (`WorkflowNode.ParentNodeId`), nicht als Knotenliste am Subprozess. Der
Graph bleibt dadurch **flach** — Knotenindex, ausgehende Kanten, Validierung, Layout und Serialisierung
arbeiten alle weiter über die eine flache Liste. Ein verschachteltes Modell hätte jede dieser Stellen
angefasst, und der Gewinn wäre nur die Baumform im JSON gewesen.

### Regeln

- **Je Ebene ein Start und ein Ende.** Die Definition selbst und jeder Abschnitt haben je genau einen —
  aus demselben Grund wie oben: nur so ist die Signatur bzw. das Ergebnis der Ebene eindeutig. Der
  Validator prüft je Behälter.
- **Ein Ende innen beendet den Abschnitt, nicht den Workflow.** Es zählt auch nicht als Ergebnis-Knoten
  der Instanz — sonst bestimmte ein Abschnitt das Ergebnis des ganzen Prozesses.
- **Ergebnis des Abschnitts:** ohne Deklaration fließt **alles** nach außen, was innen entstanden ist (wie
  bei einem parallelen Zweig ohne Join-Mapping). Mit `Outputs` genau das Deklarierte; `ScopeMode =
  Replace` macht den Abschnitt zur Konsolidierung.

### Im Designer

Ein aufgeklappter Abschnitt wird als **Rahmen** um seine Knoten gezeichnet, mit seinem Namen im Kopfband.
Die Geometrie ist **abgeleitet**: der Rahmen wird aus den Kindern aufgezogen (wie die Position eines
angedockten Fristen-Timers). Daraus folgt zweierlei — einen Knoten *in* den Rahmen zu ziehen weist ihn dem
Abschnitt zu (bei Schachtelung gewinnt der innerste), und den *Rahmen* zu ziehen verschiebt den ganzen
Inhalt.

**Zugeklappt** wird der Abschnitt als einzelner Knoten gezeichnet; sein Inhalt und die Kanten dazwischen
werden nicht gezeichnet. Der Zustand steht in der Definition, nicht in der Sitzung — ein Leser des
Diagramms sieht dasselbe Bild wie der Autor.

Das automatische Layout **schachtelt**: erst die Knoten innerhalb der Abschnitte, dann die äußere Ebene,
wobei ein Abschnitt mit der Größe zählt, die seine Kinder ergeben haben. Ohne das wären die inneren Knoten
ein zusammenhangloser Teilgraph und lägen quer über allem.

### Schema

Zwei nullable Spalten auf `Tokens` (`ArrivedViaFlowId`, `SubProcessOwnerTokenId`) → Migration
**`JoinArrivalAndSubProcess`** je Provider-Projekt.

## 19. Join: Zählung je eingehender Kante

Ein paralleler Join feuerte bisher, sobald die **Anzahl** wartender Tokens der Zahl seiner eingehenden
Kanten entsprach. Das hält nur bei balancierten Graphen. Laufen über *eine* Kante zwei Tokens ein, während
eine andere leer bleibt — möglich, sobald eine Schleife über denselben Join zurückführt —, dann stimmt die
Summe, und der Join feuert **mit halber Mannschaft**. Sichtbar wird das erst im Ergebnis (ein Zweig fehlt
im Merge), nicht in der Ursache.

Jetzt merkt sich jedes Token, über welche **Kante** es angekommen ist (`Token.ArrivedViaFlowId`), und der
Join feuert erst, wenn **jede** eingehende Kante geliefert hat. Je Kante wird das älteste wartende Token
genommen; was übrig bleibt, gehört zur nächsten Runde und wartet weiter.

**Rückfall für laufende Instanzen:** Tokens, die beim Deployment schon am Join warteten, kennen ihre Kante
nicht. Für die gilt weiterhin die alte Zählung, mit einem Hinweis im Log — sonst würde ein Join, an dem
gerade jemand wartet, nie mehr feuern und die Instanz hinge für immer. Sobald diese Tokens durch sind,
greift die genaue Regel von allein.

## 20. Rückabwicklung (Kompensation / Saga)

Ein langlaufender Prozess kann nicht in eine Datenbank-Transaktion. Wenn Schritt 4 scheitert, sind die
Schritte 1–3 längst passiert: Ware reserviert, Zahlung ausgelöst, Freigabe erteilt. Ein `rollback` gibt es
dafür nicht — jeder dieser Schritte braucht seine **eigene fachliche Gegenbuchung**. Genau das ist das
Saga-Muster, und es ist der größte fachliche Unterschied zwischen „Ablaufsteuerung" und
„Prozess-Engine".

### Die beiden Knoten

**`CompensationNode` — der Rückabwicklungs-Pfad.** Er hängt an einem Schritt (`AttachedToNodeId`), genau
wie ein Fristen-Timer: keine eingehende Kante, genau eine ausgehende, die zu den Schritten führt, die die
Arbeit zurücknehmen. Der Pfad endet in einem `SidePathEndNode`.

**`CompensateNode` — der Auslöser.** Er steht *im* Fluss (eine Kante rein, eine raus). Wird er erreicht,
wird zurückgenommen, was bereits getan wurde — und der Zweig **wartet**, bis das durch ist.

### Wann ein Schritt vorgemerkt wird

Beim **erfolgreichen Abschluss** — nicht beim Betreten und nicht über die Fehlerkante. Was gescheitert ist,
hat nichts hinterlassen, das zurückzunehmen wäre. Eine Benutzer-Aufgabe wird vorgemerkt, wenn sie
*abgeschlossen* wird, nicht wenn sie in der Arbeitsliste erscheint.

Vorgemerkt werden können Aktivitäten, Benutzer-Aufgaben, Subworkflow-Aufrufe und Abschnitte
(`CompensationNode.CanCompensate`). Ein Wartepunkt oder ein Gateway hinterlässt nichts — ein Pfad dort wäre
ein stiller Nicht-Effekt, und der Validator lehnt ihn ab.

### Der Schnappschuss — der Punkt der ganzen Sache

Beim Vormerken wird der **Variablen-Stand von genau diesem Moment** mitgespeichert, und der
Rückabwicklungs-Pfad läuft später damit. Ohne das wäre die Buchungsnummer, die er zum Stornieren braucht,
bis dahin längst von einem späteren Schritt überschrieben.

```
reserve  → ticket = "R-1"      (vorgemerkt mit ticket = "R-1")
pay      → ticket = "P-9"      (vorgemerkt mit ticket = "P-9")
undo     → refund sieht "P-9", unreserve sieht "R-1"
```

Was ein Rückabwicklungs-Pfad selbst rechnet, fließt **nicht** in den Hauptzweig zurück — wie beim
Eskalations-Nebenpfad.

### Reihenfolge und Umfang

**Rückwärts, einer nach dem anderen.** Erst die Zahlung stornieren, dann die Buchung, dann die
Reservierung — die Schritte bauen aufeinander auf, parallel wäre das falsch.

**Ohne Ziel** nimmt ein Auslöser zurück, was auf **seiner eigenen Ebene** geschehen ist: ein Auslöser in
einem Abschnitt wickelt diesen Abschnitt ab, nicht den ganzen Prozess. **Mit `TargetNodeId`** genau den
einen genannten Schritt.

Ein Schritt wird **höchstens einmal** zurückgenommen; er gilt ab dem Start seines Pfads als erledigt (nicht
erst an dessen Ende — sonst fände der nächste Durchgang denselben Eintrag noch einmal).

**Nichts vorgemerkt ist kein Fehler:** der Zweig läuft einfach weiter. Das ist der Normalfall eines
Ablaufs, der noch nichts getan hat, was zurückzunehmen wäre.

### Wo es endet

Bricht der auslösende Zweig weg (Abbruch, unterbrechende Frist, Terminate), während die Rückabwicklung
läuft, wird der Rest **nicht** mehr abgearbeitet — mit einem Eintrag im Log, damit der Fall sichtbar
bleibt und nicht als „alles zurückgenommen" durchgeht.

### Im Designer

Der Rückabwicklungs-Pfad klebt an der **linken** unteren Ecke seines Schritts, der Fristen-Timer an der
rechten — die beiden Aussagen („wenn die Frist reißt" gegen „wenn zurückgenommen wird") wären an derselben
Stelle nicht auseinanderzuhalten. Angedockt wird per Ziehen, wie beim Timer; welche Schritte in Frage
kommen, entscheidet das Modell.

Beide Knoten tragen dasselbe Zeichen (`↺`): der Auslöser als Kreis im Fluss, der Pfad als kleines Sechseck
am Schritt.

### Schema

Eine nullable Spalte auf `Tokens` (`CompensationOwnerTokenId`) und eine auf `WorkflowInstances`
(`CompensationsJson`) → Migration **`Compensation`** je Provider-Projekt.

Die Vormerkungen liegen als JSON-Spalte und nicht als eigene Tabelle: sie werden immer als *Ganzes* gelesen
(beim Rückabwickeln) und nie einzeln abgefragt — ein Index darauf hätte keinen Abnehmer. Der mitgeführte
Variablen-Stand läuft durch dieselbe typerhaltende Ablage wie die Instanz-Variablen (siehe
`WorkflowJson.RegisterVariableType`), sonst käme ein eigener Datensatz-Typ als untypisierte Struktur zurück.

## 21. Inklusives Gateway (OR) — der strukturierte Weg

Der `InclusiveGatewayNode` ist die dritte Gateway-Art:

| | Split | Join |
|---|---|---|
| **XOR** | genau *ein* Ausgang | der erste Ankömmling läuft weiter |
| **AND** | *alle* Ausgänge | wartet auf *alle* Eingänge |
| **OR** | *alle zutreffenden* — 1 bis n | wartet auf **genau die aktivierten** |

Beispiel: Bestellung prüfen → *Bonität*, *Exportkontrolle*, *Grossauftrag-Freigabe*. Bei dieser Bestellung
greifen zwei Bedingungen. Auf drei zu warten wäre ein Deadlock; nach dem ersten weiterzulaufen führte
alles hinter dem Join dreimal aus.

### Warum „strukturiert"

Der Join müsste allgemein beantworten: „kann mich noch irgendein Token erreichen?" Das ist über
Bedingungen (CScript über Laufzeitwerte) und Schleifen hinweg nicht entscheidbar, und beide Fehlrichtungen
sind teuer — ewiges Warten oder ein Merge, in dem still ein Zweig fehlt. Die BPMN-Spec definiert das über
Erreichbarkeits-Analyse; alle bekannten Engines implementieren davon eine Näherung.

Hier läuft es anders herum: **der Split weiss, wie viele Zweige er aktiviert hat** — er hat die
Bedingungen gerade ausgewertet. Er stempelt die Zahl auf seine Tokens (`Token.SplitBranchCount`), der
Join zählt. Aus der unentscheidbaren Frage wird dieselbe Zählung, mit der AND-Split und AND-Join schon
immer arbeiten.

### Die Regeln des Splits

- Jede Kante, deren **Bedingung zutrifft**, wird genommen. Eine Kante **ohne** Bedingung wird immer
  genommen — genau darin unterscheidet sich das OR vom XOR, das die erste passende nimmt und aufhört.
- Die **Standard-Kante** (`DefaultFlowId`) nimmt an der Auswahl nicht teil; sie greift nur, wenn sonst
  nichts zutrifft. Ohne sie faultet ein Lauf ohne Treffer — stillschweigend gar nicht weiterzulaufen hiesse,
  den Zweig spurlos zu verlieren.
- Auch bei **einem** zutreffenden Zweig wirkt es als Split (eigene Scope-Kopie, eigener Stempel). Sonst
  behielte das Token die Zweig-Herkunft der umgebenden Ebene, und der Join zählte es der falschen Region zu.

### Der Preis: Split und Join sind ein Paar

Der Validator besteht darauf, und zwar als **Fehler**:

- Jeder Zweig des Splits muss denselben inklusiven Join erreichen, und der muss so viele Eingänge haben,
  wie der Split Ausgänge hat. Ein Zweig, der am Join vorbeiläuft, lässt dessen Zahl nie voll werden.
- Ein inklusiver Join braucht einen passenden Split *oberhalb*. Ohne den wartet er auf eine Zahl, die
  niemand anmeldet.
- Ein Knoten darf nicht Split **und** Join sein (beim AND ist das erlaubt) — er müsste gleichzeitig zählen
  und anmelden.
- Ein Split, bei dem **keine** Kante eine Bedingung trägt, ist ein AND. Der Validator sagt das.

Das schliesst keine sinnvollen Modelle aus. Ein Merge zweier Kanten in einen *gewöhnlichen* Knoten bleibt
erlaubt und harmlos wie bisher — er ist kein Join.

### Zur Laufzeit

Trifft trotzdem ein Token am inklusiven Join ein, das **keinen** Stempel trägt (eine Kante, die jemand
direkt auf den Join gezogen hat, oder ein Zweig aus einem AND-Split), **faultet die Instanz sofort** mit
einer Meldung, die den Token nennt. Sie würde sonst für immer warten, und die Ursache stünde nirgends.

Verschachtelung ist unproblematisch: der Join eines inneren Splits gibt einen Träger-Token zurück, der die
Herkunft der äusseren Ebene weiterträgt. Auch eine **Schleife** über denselben Split funktioniert — jede
Aktivierung erzeugt eine neue Split-Token-Id, die Runden vermischen sich also nicht.

Das Zusammenführen der Zweig-Scopes (Merge, `Outputs`, `ScopeMode`) ist mit dem AND-Join **dieselbe**
Ausführung (`IMergingGateway`) — sonst liefen die beiden beim nächsten Detail auseinander, ohne dass es
jemandem auffiele.

### Schema

Eine nullable Spalte auf `Tokens` (`SplitBranchCount`) → Migration **`InclusiveGateway`** je
Provider-Projekt.

## 22. Definitionen: technischer Schlüssel, Mandant und Sichtbarkeit

### Warum der Schlüssel nicht mehr `(Id, Version)` ist

Die Eindeutigkeit einer Definition gilt **je Mandant**: „Onboarding v1" darf es einmal pro Mandant geben,
und einmal öffentlich. Als Primärschlüssel ist das nicht formulierbar — `TenantId` ist nullable (das *ist*
die Kennzeichnung „öffentlich"), und eine NULL-Spalte darf in keinem Primärschlüssel stehen. Solange der
Schlüssel `(Id, Version)` war, konnten zwei Mandanten deshalb **nicht** denselben fachlichen Namen
benutzen — der zweite lief in einen Primärschlüssel-Verstoss.

Jetzt trägt jede Zeile einen technischen Schlüssel (`DefinitionKey`, Identity), und die fachliche Regel
steht in einem **eindeutigen Index** `(TenantId, Id, Version)`.

> Zwei Fallstricke, die beide *stillschweigend* gewesen wären:
> - SQL Server hängt an einen eindeutigen Index über nullable Spalten von selbst ein
>   `WHERE TenantId IS NOT NULL` — und schlösse damit ausgerechnet die öffentlichen Definitionen von der
>   Prüfung aus. Das Modell setzt deshalb ausdrücklich `HasFilter(null)`. Ohne Filter zählen NULLs bei
>   SQL Server als *gleich*: höchstens eine öffentliche je Id/Version. Genau das ist gemeint.
> - PostgreSQL zählt NULLs im eindeutigen Index als *verschieden* — dort kämen zwei öffentliche
>   Definitionen durch. Die Migration legt den Index deshalb als Ausdrucks-Index über
>   `COALESCE("TenantId", '')` an. Bewusst nicht `NULLS NOT DISTINCT`: das gibt es erst ab PostgreSQL 15,
>   und ein Skript, das je nach Serverversion scheitert, wäre der schlechtere Handel.

### Die Instanz verweist über den Schlüssel

`WorkflowInstance.DefinitionKey` ist ein **echter Fremdschlüssel** (`Restrict` — eine Definition, an der
noch Instanzen hängen, lässt sich nicht nebenbei löschen). `DefinitionId` und `DefinitionVersion` stehen
weiterhin daneben, aber als **Anzeige und Filter**.

Das ist keine Formsache. Eine Instanz eines *öffentlichen* Workflows gehört trotzdem ihrem Mandanten.
Löste die Engine bei jedem Vortrieb über Name und Version auf, dann geschähe Folgendes, sobald dieser
Mandant eine eigene Fassung desselben Namens anlegt:

```
Instanz läuft auf der öffentlichen „Onboarding" v1
Mandant legt eigene „Onboarding" v1 an
nächster Vortrieb  →  lädt einen ANDEREN Graphen
                   →  das Token steht auf einem Knoten, den es dort nicht gibt
```

Über den Schlüssel bleibt eine laufende Instanz an genau der Definition, mit der sie gestartet wurde.

### Öffentlich ist eine ausdrückliche Entscheidung

`WorkflowDefinition.IsPublic` sagt es; `TenantId` ist das Ergebnis. Beim Speichern gilt:

| `IsPublic` | `TenantId` am Modell | gespeichert als |
|---|---|---|
| false | gesetzt | dieser Mandant |
| false | leer | **der Mandant des laufenden Kontexts** |
| true | leer | öffentlich |
| true | gesetzt | **abgelehnt** — Widerspruch, keine Auslegungsfrage |

„Nichts gesetzt" darf nicht stillschweigend zu „öffentlich" werden — sonst legt der Editor Definitionen
an, die jeder andere Mandant sieht und starten kann.

Anlegen **und Ändern** einer öffentlichen Definition verlangt die Berechtigung **`Workflow.DesignPublic`**
zusätzlich zu `Workflow.Design`. Auch das Ändern: eine bestehende öffentliche Definition wirkt auf alle
Mandanten. Geprüft wird im Handler, nicht nur im Editor — sonst genügte ein gesetztes Flag im Modell.

### Auflösen über den Namen: eigener Mandant vor öffentlich

`GetDefinition(id, version, tenantId)` liefert die **eigene** Definition des Mandanten, ersatzweise die
öffentliche. Eine mandanteneigene Fassung ist die Verfeinerung und überdeckt die allgemeine. Ohne diese
Regel entschiede die Reihenfolge der Datenbank, also der Zufall.

Für einen Verweis, der stehen bleiben soll, gehört dagegen `GetDefinition(definitionKey)` — welcher Name
gerade welche Zeile meint, kann sich ändern.

Der technische Schlüssel ist **nicht Teil des Austauschformats** (`[JsonIgnore]`): er gilt in genau einer
Ablage und wäre in einer exportierten Datei eine Zahl, die anderswo auf etwas anderes zeigt.

### Schema

Migration **`DefinitionKey`** je Provider-Projekt. **Sie verwirft den Instanz-Bestand**
(`WorkflowInstances`, `Tokens`, `HistoryEntries`, `BranchLocks`) — die Verweis-Spalte ist ein
Fremdschlüssel und darf nicht null sein, und für bestehende Zeilen gäbe es keinen Wert, den man erfinden
könnte. Die Definitionen selbst bleiben erhalten.

## 23. Nachrichten senden — der `SendMessageNode`

Das Gegenstück zum Wartepunkt. Der Wartepunkt empfängt, dieser Knoten sendet — mit derselben
Unterscheidung: **Nachricht** (gerichtet, über einen Korrelationsschlüssel) oder **Rundruf** (alle
gleichnamigen `Signal`-Wartepunkte).

```
Bestellprozess:   … → ⏳ CustomerPaid [orderId_customerId] → verbuchen → …
Zahlungsprozess:  … → 👤 Zuweisung durch Sachbearbeiter → 📨 CustomerPaid [orderId_customerId] → Ende
```

Der Korrelationsausdruck wird **beim Senden** über den Variablen-Stand des sendenden Zweigs ausgewertet
— genau wie der des Wartepunkts beim Parken, und aus demselben Grund: nur dort ist der Wert eindeutig.
Beide Seiten müssen denselben Wert ergeben. Die **Nutzdaten** sind gewöhnliche Eingabe-Bindungen; sie
landen im Variablen-Stack des empfangenden Zweigs, bevor er weiterläuft.

### Zugestellt wird nach dem Commit

Das ist die wichtigste Eigenschaft, und sie gilt **auch für Zustellungen aus einer Aktivität heraus**
(`DeliverSignal`, `BroadcastSignal`, `SignalWorkflow`). Läuft gerade ein Vortrieb, wird gepuffert; die
Nachrichten gehen raus, wenn der sendende Zweig seinen Halt erreicht hat.

Der Grund: der Empfänger wird beim Zustellen **selbst vorangetrieben, auf dem Thread des Senders**. Sofort
zugestellt liefe er auf einem Stand des Senders, den es in der Datenbank noch gar nicht gibt — und ein
Fehler des Empfängers schlüge mitten in der Aktivität des Senders auf.

Ein Aufruf von **aussen** (Controller, Handler, Job) läuft unverändert sofort — dort gibt es keinen
laufenden Vortrieb, auf dessen Ende man warten könnte.

Was ein Empfänger seinerseits sendet, landet in derselben Warteschlange und wird in derselben Runde
abgearbeitet — nicht rekursiv aufgestapelt. Eine gegenseitige Benachrichtigung (A weckt B, B weckt A)
läuft deshalb in eine **Obergrenze** und wird als Modellfehler gemeldet, statt den Stack zu sprengen.

### Die Grenze, die daraus folgt

**Wie viele Empfänger erreicht wurden, steht beim Ausführen des Knotens noch nicht fest** — zu dem
Zeitpunkt ist noch nichts zugestellt. Die Zahl kann deshalb nicht in eine Variable fliessen und im Prozess
nicht verzweigt werden. „Niemand hat gewartet" wird ins System-Log gemeldet.

Wer darauf verzweigen **muss**, nimmt weiterhin eine Aktivität, die selbst zustellt und den Rückgabewert
auswertet — dann allerdings mit der Verschränkung, die der Knoten gerade vermeidet.

### Regeln

- Signalname ist Pflicht.
- Eine **Nachricht ohne Korrelation** ist ein Fehler: sie würde zum Rundruf und jeden gleichnamigen
  Wartepunkt wecken. Wer alle meint, stellt den Knoten auf Rundruf — dann steht es im Modell.
- Genau ein Ausgang. Senden verzweigt den Fluss nicht.
- Ein Fehler im Korrelationsausdruck **faultet** die Instanz. Eine Nachricht, deren Schlüssel nicht
  berechenbar ist, käme nirgends an — und das fiele erst auf, wenn die Gegenseite ausbleibt.

Empfänger und Sender müssen im selben **Mandanten** und in derselben **Workflow-Umgebung** (Store)
liegen; die Abfrage nach Wartepunkten läuft im Store des Senders.

**Kein Schema-Eingriff** — es wird nichts persistiert.
