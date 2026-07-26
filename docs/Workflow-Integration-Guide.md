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
   `Workflow.Design` (Konstanten in `WorkflowSecurity`) sind **DB-getrieben** zu aktivieren
   (Navigation + Feature-Freischaltung sind Host-Sache).

4. **BlazorMonaco-Skripte im Host.** Der CScript-/Ausdrucks-Editor braucht — wie die AdminViews —
   die drei BlazorMonaco-Skripte (`jsInterop.js`, `loader.js`, `editor.main.js`) **nach** dem
   Blazor-Skript in der Host-Seite. Ohne sie rendern die CScript-Felder nicht.

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
- **Konflikt-Policy.** Schreiben zwei **parallele** Zweige dieselbe Variable auf **verschiedene**
  Werte, faultet die Instanz (kein stiller last-writer); gleicher Wert ist harmlos. Der Validator
  warnt dafür bereits zur Design-Zeit (parallele Zweige mit gleicher Output-Variable).
- **Migrationen.** `WorkflowContext`-Schemaänderungen in den provider-spezifischen Migrations-Projekten
  nachziehen. Betroffen: `TokenRow` (inkl. `WaitingTarget`, `WaitingForChildInstanceId`),
  `WorkflowBranchLockRow`, `WorkflowInstanceRow.Version`, die eigene **`HistoryEntryRow`**-Tabelle
  (Protokoll append-only mit Severity, löst den früheren `HistoryJson`-Blob ab) und die
  Subworkflow-Spalten (`ParentInstanceId`, `ParentTokenId`, `RootInstanceId`, `CallDepth`).
  **Kein** Schema-Bedarf für Fehler-Ausgänge und JSON-Export/Import (rein im Definition-JSON / normale
  Variablen).

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
  der Aufrufer parkt, bis der Subworkflow endet, und übernimmt dessen Ergebnis (Fault propagiert
  standardmäßig; Abbruch kaskadiert auf laufende Kinder). Der Subworkflow ist eine **eigene Instanz**
  (eigenes Monitoring), erbt den Tenant, ist beliebig verschachtelbar. **Getrieben vom Runner** — reines
  inline `StartWorkflow` ohne Runner treibt Kinder NICHT (Web-Only hostet den Runner in-proc, wie
  empfohlen). Aggregierte History: Kind-Einträge tragen die `RootInstanceId` des Elternbaums → das
  Protokoll des ganzen Baums ist über die eine Wurzel lesbar. Editor: Palette „Subworkflow" +
  Config-Panel (Definition/Version + Bindungen).

- **JSON Export/Import.** `ITVComponents.Workflow.Serialization.WorkflowJson` (öffentlich) ist das
  kanonische, portable Format (stabile `"kind"`-Diskriminatoren, **typnamen-unabhängig**) — dasselbe,
  das der Store persistiert. `ExportDefinition` / `ImportDefinition`; im Editor Export-/Import-Buttons
  (Import ersetzt die Zeichenfläche und validiert, speichert aber nicht automatisch). Ermöglicht Teilen
  als Datei, Versionierung (Git), Transport zwischen Umgebungen und Tooling.

- **Protokoll mit Schweregrad.** Das Ausführungsprotokoll liegt als eigene Tabelle (`HistoryEntryRow`,
  append-only) mit `Severity` (Verbose/Info/Warning/Error) — filter-/abfragbar; das Monitoring-Detail
  zeigt eine farbige Severity-Spalte. Bei Subworkflows über die `RootInstanceId` baumweit aggregierbar.
