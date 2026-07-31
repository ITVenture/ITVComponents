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
  Besitzer der Zweig-Sperren. Muss über Neustarts **gleich** bleiben (beim Start räumt der Runner
  seine eigenen, nach einem Absturz hängengebliebenen Sperren über diesen Namen ab).

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
| `TitleExpression` | CScript für einen Titel aus den Daten („Rechnung 4711"). Gewinnt gegen `Title`, ist dann aber Klartext und **nicht** mehrsprachig. |
| `Inputs` / `Outputs` | Was die Maske sieht bzw. zurückgibt (dieselbe Bindungs-Maschinerie wie an der Aktivität, inkl. `ScopeMode`/`RetainVariables`). |
| `FormFields` | Deklaration der **generischen Maske**. Leer = die Aufgabe wird nur bestätigt. |
| `DueInHours` | Frist ab dem Parken — reine Anzeige-/Sortierinformation. |

Zwei Dinge, die man nicht verwechseln darf: **Permission** = siehst du diese Sorte Aufgabe,
**Assignment** = ist dieser konkrete Fall deiner.

Ein Zuweisungs-Ausdruck, der scheitert, **faultet die Instanz** — bewusst: die Aufgabe läge sonst im Pool
und wäre für jeden mit der Permission sichtbar, also eine stille Sichtbarkeits-Ausweitung. Ein
scheiternder `TitleExpression` ist dagegen nur eine Log-Zeile (der Titel fällt auf `Title` zurück) — eine
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
(Payload, Feld-Deklaration, übersetzte Texte) und schließt mit `TaskContext.CompleteAsync(result)` ab.
Bewusst per Cascading und nicht über ein Parameter-Dictionary einer `DynamicComponent`: dessen
Parameternamen werden erst zur Laufzeit geprüft, und dieselbe Komponente läuft so unverändert im
Registry-Weg **und** direkt auf einer eigenen Seite.

Der Mantel (`UserTaskDialog`) gehört bewusst *einmal* der Bibliothek: der teure Teil ist nicht das
Anzeigen, sondern der Abschluss — Ergebnis-Mapping, weiche Sperre, Doppel-Klick, Versionskonflikt und
„war schon erledigt". Der Erledigen-Knopf des Mantels erscheint nur bei der generischen Maske; eine
eigene Komponente bringt ihre eigenen Aktionen mit (freigeben/ablehnen/…).

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
