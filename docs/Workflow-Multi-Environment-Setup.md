# Mehrere Workflow-Umgebungen — Host-Verdrahtung (DI)

Diese Seite beschreibt, wie ein Host mehrere **Workflow-Umgebungen** (getrennte Stores/Datenbanken)
einrichtet, sodass Designer, Monitoring und Aufgaben-Views wahlweise über einen Picker oder ein thematisches
Navigations-Segment auf einer gewählten Umgebung arbeiten. Sie baut auf der Ein-Umgebungs-Verdrahtung aus dem
[Workflow-Integration-Guide](Workflow-Integration-Guide.md) §5 auf (frischer Kontext pro Operation +
Engine-Factory).

> **Fallback (wichtig):** Ist **keine** `WorkflowEnvironmentSettings`-Einstellung hinterlegt (oder ihre
> `Environments`-Liste leer), ändert sich **nichts** — die Views nutzen die eine, per DI registrierte Umgebung
> ohne Umgebungs-Auswahl. Die Mehr-Umgebungen-Verdrahtung unten ist rein **additiv**.

---

## 1. Das Prinzip in einem Satz

Jede View-Operation zieht ihren `WorkflowContext` pro Aufruf frisch über **einen** Seam
(`IFreshInjectablePlugin<WorkflowContext>`) und leaset ihn **namentlich**. Im Ein-Umgebungs-Betrieb ist der
Name fest (`"WorkflowContext"`); im Mehr-Umgebungen-Betrieb ist der Name der **Store-Plugin-Name der gewählten
Umgebung**. Der Host registriert also je Umgebung eine eigene scope-owned `WorkflowContext`-Dependency unter
diesem Namen — mehr braucht die Store-Umschaltung nicht.

Ablauf zur Laufzeit:

```
View (Picker/Nav-Segment) ──gewählte Umgebung (String)──▶ Handler-Methode(..., environment)
   │
   ▼
WorkflowEnvironmentResolver: environment ──▶ Settings ──▶ env.WorkflowStorePluginName
   │
   ▼
WorkflowOperation.LeaseContext(): freshContext.Lease(storeName ?? "WorkflowContext")
   │
   ▼
scope[storeName, true]  ==  die vom Host registrierte WorkflowContext-Dependency dieser Umgebung
```

---

## 2. Voraussetzung: der Ein-Umgebungs-Seam

Wie im Integration-Guide §5 beschrieben — das ist zugleich der Fallback und die Grundlage:

1. `UseInjectablePlugins(...)` (registriert `IFreshInjectablePlugin<>`).
2. Eine scope-owned `WorkflowContext`-Dependency unter dem **Standard-Namen** `"WorkflowContext"`:

   ```csharp
   factoryOptions.AddDependency("WorkflowContext",
       sp => sp.GetRequiredService<IDbContextFactory<WorkflowContext>>().CreateDbContext(),
       disposeWithScope: true);   // disposeWithScope: true ist Pflicht
   ```

3. Eine `WorkflowEngineFactory` (für Signal/Abbruch/Aufgaben-Abschluss):

   ```csharp
   services.AddSingleton<WorkflowEngineFactory>(sp => store =>
       new WorkflowEngine(store, sp.GetRequiredService<IActivityHost>()));
   ```

Bleibt es dabei (ohne die Einstellung aus Abschnitt 3), läuft alles wie bisher.

---

## 3. Schritt 1 — `WorkflowEnvironmentSettings` konfigurieren

Die Einstellung wird über `IHierarchySettings<WorkflowEnvironmentSettings>` (scoped vor global) unter dem
Schlüssel **`WorkflowEnvironmentSettings`** geladen. (Der Typ liegt UI-neutral in
`ITVComponents.Workflow.WebWorker.Configuration`, damit Views **und** Background-Worker ihn teilen.) Beispiel
(global):

```json
{
  "WorkflowEnvironmentSettings": {
    "Environments": [
      {
        "Name": "PaymentFlows",
        "DisplayName": "Zahlungs-Workflows",
        "UseDesigner": true,
        "UseAdmin": true,
        "UseForms": true,
        "UseWorker": true,
        "WorkflowStorePluginName": "wf-payments",
        "MaxLingerSeconds": 30,
        "Instances": [
          {
            "Name": "payments-backend",
            "DisplayName": "Backend-Worker",
            "ActivityCatalogPluginName": "catalog-payments"
          }
        ]
      },
      {
        "Name": "OnboardingFlows",
        "DisplayName": "Onboarding",
        "UseDesigner": true,
        "UseAdmin": true,
        "UseForms": false,
        "WorkflowStorePluginName": "wf-onboarding",
        "Instances": [
          { "Name": "onboarding-web", "ActivityCatalogPluginName": "catalog-onboarding" }
        ]
      }
    ]
  }
}
```

| Feld | Bedeutung |
|---|---|
| `Environment.Name` | Technischer, eindeutiger Name. Dient als Schlüssel **und** als Navigations-Segment (`/Workflow/env/{Name}/…`). |
| `DisplayName` | Anzeigename im Picker (fällt auf `Name` zurück). |
| `UseDesigner`/`UseAdmin`/`UseForms`/`UseWorker` | Welche Views diese Umgebung anbieten — der Picker der jeweiligen View filtert danach. |
| `WorkflowStorePluginName` | **Name der scope-owned `WorkflowContext`-Dependency** dieser Umgebung (siehe Schritt 2). |
| `MaxLingerSeconds` | *(optional)* Poll-Obergrenze des Workers für diese Umgebung, in Sekunden; `null` = globaler Worker-Default. Siehe Abschnitt 7. |
| `Instance.Name` | Name des Workers. Entspricht dem `ExecutionTarget` einer automatisierten Aktivität. |
| `Instance.ActivityCatalogPluginName` | Name des WebPlugins, das den `IWorkflowActivityCatalog` dieses Workers liefert (Schritt 3). |
| `Instance.Services` | Freies Dictionary für weitere instanz-spezifische Service-Plugin-Namen (offen für später). |

---

## 4. Schritt 2 — Einen `WorkflowContext`-Store je Umgebung registrieren

Für **jede** Umgebung eine scope-owned Dependency, deren **Name exakt** dem `WorkflowStorePluginName` der
Umgebung entspricht. Jedes Delegate liefert einen `WorkflowContext`, der auf die DB dieser Umgebung zeigt —
z.B. über eine **keyed** `IDbContextFactory<WorkflowContext>`:

```csharp
// Host: je Umgebung eine DbContext-Factory unter ihrem Key (Beispiel: verschiedene Connectionstrings)
services.AddDbContextFactory<WorkflowContext>(/* payments */)   // als keyed registrieren, siehe Host-Setup
        ...;

factoryOptions.AddDependency("wf-payments",
    sp => sp.GetRequiredKeyedService<IDbContextFactory<WorkflowContext>>("payments").CreateDbContext(),
    disposeWithScope: true);

factoryOptions.AddDependency("wf-onboarding",
    sp => sp.GetRequiredKeyedService<IDbContextFactory<WorkflowContext>>("onboarding").CreateDbContext(),
    disposeWithScope: true);
```

- Der **Name** (`"wf-payments"`) muss mit `Environment.WorkflowStorePluginName` übereinstimmen — daran leaset
  die `WorkflowOperation` den richtigen Kontext.
- `disposeWithScope: true` ist auch hier Pflicht (frisch je Operation, mit dem Op-Scope disponiert).
- Der Standard-Name `"WorkflowContext"` darf zusätzlich registriert bleiben — er ist die Umgebung, die eine
  View **ohne** gewählte Umgebung (Fallback) bedient.

> **Tenant vs. global:** Wie im Ein-Umgebungs-Fall entscheidet allein das Delegate, ob der Kontext global
> (filterfrei) oder tenant-fähig gebaut wird. Die Views sehen den Unterschied nicht.

---

## 5. Schritt 3 — ActivityCatalog je Worker-Instanz (`WebPluginActivityCatalog`)

Jede Service-Instanz (Worker) hat ihren eigenen Katalog der **zulässigen** Aktivitäten. Der neue
`WebPluginActivityCatalog` (in `ITVComponents.Workflow.Plugins.WebCoreToolkit`) löst Aktivitäts-Typen und
deren Parameter über die **WebPlugin-Schnittstelle** (`IWebPluginsSelector`) auf — nicht über einen
`IDynamicLoader` — und wird selbst als **Web-Plugin** geladen, konstruiert mit der Liste der erlaubten
Aktivitäts-Plugin-Namen:

```
// WebPlugin-Definition (Konstruktions-String), Name = Instance.ActivityCatalogPluginName:
//   catalog-payments  ->  new ...WebPluginActivityCatalog(factory, new string[]{ "ApproveInvoice", "PostLedger", ... })
```

- Die `factory` injiziert sich selbst; den `IWebPluginsSelector` bezieht der Katalog aus dem Factory-Kontext
  (`Global.PlugInSelectorName`).
- Je Aktivität holt der Katalog die WebPlugin-Definition (`UniqueName → Constructor`), löst den CLR-Typ
  reflection-only auf (`TryGetPluginType`), filtert auf `IActivityPlugin` und liest die Attribute
  (`WorkflowActivityAttribute`/`ActivityParameterAttribute`).
- Für die **Ausführung** durch einen Runner dient der bereits vorhandene `WebToolkitActivityHost`
  (`IActivityHost`), der Aktivitäten je Vortrieb tenant-fixiert über `IWebPluginHelper.CreateOperationScope`
  auflöst.

> **Aktueller Stand / Grenze:** Der **Editor** fragt den `IWorkflowActivityCatalog` heute noch aus dem
> DI-Container ab (ein Katalog). Ein Host, der die WebPlugin-Auflösung schon nutzen will, registriert den
> `WebPluginActivityCatalog` als diesen DI-`IWorkflowActivityCatalog`. Die **per-Instanz**-Auswahl im Editor
> (Katalog anhand des `ExecutionTarget`/Workers einer Aktivität) ist der nächste, noch offene Ausbauschritt —
> siehe [[workflow_multi_environment]].

---

## 6. Schritt 4 — Views/Handler (nichts zu tun) und Navigation

Die View-Handler (`IWorkflowDesignHandler`, `IWorkflowMonitorHandler`, `IWorkflowTaskHandler`) sind bereits
umgebungs-fähig: jede Methode hat einen optionalen `environment`-Parameter (Standard `null` = Fallback). Die
Views reichen die im Picker gewählte oder aus dem Nav-Segment stammende Umgebung durch — hier ist **keine**
Host-Registrierung nötig.

**Navigation für thematische Nav-Items:** neben den Default-Routen gibt es je Top-View eine env-scoped Route:

| View | Default | Umgebungs-scoped (für Nav-Items) |
|---|---|---|
| Definitionen | `/Workflow/Definitions` | `/Workflow/env/{Environment}/Definitions` |
| Editor (neu) | `/Workflow/Editor/new` | `/Workflow/env/{Environment}/Editor/new` |
| Editor (edit) | `/Workflow/Definitions/{id}/{v}/edit` | `/Workflow/env/{Environment}/Definitions/{id}/{v}/edit` |
| Graph | `/Workflow/Definitions/{id}/{v}` | `/Workflow/env/{Environment}/Definitions/{id}/{v}` |
| Instanzen | `/Workflow/Instances` | `/Workflow/env/{Environment}/Instances` |
| Aufgaben | `/Workflow/Tasks` | `/Workflow/env/{Environment}/Tasks` |

- Verweist ein Nav-Eintrag auf eine `…/env/{Environment}/…`-Route, ist die Umgebung **implizit** gesetzt — die
  View zeigt dann keinen Picker, sondern die Umgebung als Chip. Sub-Ansichten (Editor/Graph/Dialog) reichen die
  Umgebung weiter.
- Ohne Segment erscheint der **Picker** (gefiltert nach dem View-Flag der Umgebung); die erste passende
  Umgebung ist vorgewählt.

---

## 7. Der Background-Worker (`ITVComponents.Workflow.WebWorker`)

Der Worker treibt Workflows **außerhalb** eines UI-Requests voran: fällige Timer, Crash-Recovery liegen
gebliebener Instanzen, verteilter Handoff und die Fortsetzung signalgetriebener Arbeit. Er ist ein eigenes,
UI-neutrales Projekt und wird dort registriert, wo die Workflow-Arbeit tatsächlich laufen soll (im selben Host
wie die Web-App **oder** in einem dedizierten Worker-Host).

### Registrierung

```csharp
services.AddWorkflowWebWorker(o =>
{
    o.MaxConcurrency  = 12;                        // geteilter Pool für den GANZEN Prozess
    o.MinPollInterval = TimeSpan.FromSeconds(1);   // "heiß", solange Arbeit gefunden wird
    o.MaxPollInterval = TimeSpan.FromHours(1);     // globaler Max-Linger (pro Umgebung überschreibbar)
    o.RefreshInterval = TimeSpan.FromMinutes(2);   // Kadenz der Tenant/Umgebungs-Discovery
    o.LockOwnerName   = "worker-slot-A";           // siehe Betriebs-Hinweis unten
});
```

**Voraussetzung im Host:**
- Dieselben per-Umgebung `WorkflowContext`-Dependencies wie in Abschnitt 4 — der Worker leaset über
  **denselben Seam** wie die Views (`IFreshInjectablePlugin<WorkflowContext>.Lease(WorkflowStorePluginName)`).
- Ein `IActivityHost` (typischerweise `WebToolkitActivityHost`).
- Im **Tenant-Betrieb**: der `IAllTenantsReader` — er kommt **automatisch** über die Security-Kontexte
  (`[ExplicitlyExpose]` + die bestehende `RegisterExplicityInterfacesScoped`-Verdrahtung in `UseDbIdentities`),
  **kein** Host-Wiring nötig.

### Was der Worker tut

- **Discovery (langsam, alle `RefreshInterval`):** geht **einmal alle Tenants** durch (`IAllTenantsReader` —
  ungefiltert, ohne Per-Tenant-Permission, ohne `ShowAllTenants`), liest je Tenant seine
  `WorkflowEnvironmentSettings` und materialisiert je **(Tenant, Umgebung mit `UseWorker`)** einen passiven
  **Deskriptor** mit Store-Namen und `hostTargets` (= die `Instance.Name` der Umgebung). Ohne Tenant-Modell
  (kein `IAllTenantsReader`) → ein globaler, filterfreier Deskriptor je Umgebung.
- **Antrieb (schnell, geteilter Pool):** je Deskriptor die vier Store-Fragen (`FindRunnable`, `FindDueTimers`,
  `FindBranchesWaitingForTarget(hostTargets)`, `FindFinishedChildrenWithWaitingParent`) → Engine, mit
  prozessübergreifendem Branch-Lock. Der Store wird je Antrieb **frisch geleast** (wie `WorkflowOperation`);
  tenant-gepinnt (`PrepareBackgroundContext`) bzw. global filterfrei (`PrepareEmptyContext`).
- **Terminierung (kein blindes Polling):** der nächste Poll liegt exakt auf dem **nächsten fälligen Timer**
  (`PeekNextTimerDueUtc`), sonst auf dem **Max-Linger** (Sicherheitsnetz für Crash-Recovery/Handoff), und bleibt
  "heiß" (`MinPollInterval`), solange Arbeit gefunden wird.

### Einheitlich pro Tenant

Jeder Tenant bekommt seinen **eigenen, tenant-gepinnten** Deskriptor — unabhängig davon, ob seine
Umgebungs-Config global geerbt oder per Tenant überschrieben ist. Das ist bewusst so: Store-Plugins können pro
Tenant (über tenant-lokale Konstanten) auf **verschiedene** DBs formatiert werden, daher ist eine filterfreie
„ein Store für alle Tenants"-Abkürzung im Allgemeinen nicht sicher. Tenant-Pinning macht die Sichten disjunkt
→ **keine Doppelverarbeitung**, selbst wenn zwei Tenants zufällig dieselbe DB teilen. Der Footprint bleibt
überschaubar: geteilter Pool statt Threads-je-Tenant, passive Deskriptoren (wenige KB), Back-off/Terminierung
drosseln leerlaufende Tenants.

### Max-Linger pro Umgebung

`WorkflowEnvironment.MaxLingerSeconds` (nullbar) überschreibt den globalen `MaxPollInterval` je Umgebung — eine
Zahlungs-Umgebung kann enger takten (30 s) als eine Archiv-Umgebung (3600 s).

### Betriebs-Hinweis: `LockOwnerName` (wichtig)

Branch-Locks haben **keine TTL**; der Worker gibt beim ersten Antrieb eines Deskriptors seine eigenen
verwaisten Locks frei (`ReleaseLocksOfOwner`). Der Owner ist **deskriptor-spezifisch**
(`{LockOwnerName}|{Umgebung}|{Tenant}`), räumt also nur die eigenen Leichen — nie aktive Locks eines anderen
Deskriptors oder Workers (die Lock-Tabelle hat kein `TenantId`; die Freigabe ist ein globales `ExecuteDelete`
nach Owner, darum die deskriptor-spezifische Kennung). **`LockOwnerName` muss je gleichzeitig laufender
Worker-Instanz eindeutig UND über deren Neustarts stabil sein** — sonst gäbe eine Instanz beim Start die aktiven
Locks einer anderen frei. Ein Worker pro Deployment → Default genügt; mehrere → Slot-/Maschinen-Name setzen.

### Wake-Hook (nur derselbe Prozess)

Läuft der Worker **im selben Prozess** wie die Web-App, wecken `IWorkflowMonitorHandler.SignalAsync` und
`IWorkflowTaskHandler.CompleteAsync` nach Erfolg den betroffenen Deskriptor sofort
(`IWorkflowWorkerWake.Poke(environment, tenant)`), statt ihn bis zum Max-Linger warten zu lassen. In **getrennten
Prozessen** greift der Hook nicht (kein geteilter DI-Singleton) — dort sorgen die präzise Timer-Terminierung und
der Max-Linger für die zeitnahe Aufnahme.

---

## 8. Zusammengefasst — Checkliste je Host

1. Ein-Umgebungs-Seam steht (Integration-Guide §5): `UseInjectablePlugins`, `WorkflowContext`-Dependency,
   `WorkflowEngineFactory`. (Zugleich der Fallback.)
2. `WorkflowEnvironmentSettings` konfiguriert (Abschnitt 3).
3. Je Umgebung eine `WorkflowContext`-Dependency unter Namen = `WorkflowStorePluginName` (Abschnitt 4).
4. Je Instanz ein `WebPluginActivityCatalog`-WebPlugin unter Namen = `ActivityCatalogPluginName`, mit der Liste
   der erlaubten Aktivitäten (Abschnitt 5). (Optional/Editor-Einbindung: siehe Grenze in Abschnitt 5.)
5. Optional: thematische Nav-Items auf `…/env/{Environment}/…` legen.
6. Wenn Workflows im Hintergrund voranlaufen sollen: `AddWorkflowWebWorker(…)` registrieren und `LockOwnerName`
   je gleichzeitig laufender Worker-Instanz eindeutig setzen (Abschnitt 7).

---

## 9. Grenzen / noch offen

- **Per-Instanz-Katalog im Editor:** Der Editor wählt den ActivityCatalog noch nicht anhand des Workers
  (`ExecutionTarget`) einer Aktivität aus — das ist der nächste Ausbauschritt.
- **Wake-Hook nur same-process:** Läuft der Worker in einem **getrennten** Prozess, greift der Poke nicht; die
  Aufnahme erfolgt dann über Timer-Terminierung und Max-Linger (Abschnitt 7).
- Die Nav-Segment- und Picker-Wege münden beide in denselben `environment`-Parameter; sie sind bewusst
  Blazor-nebenläufigkeits-sicher (kein geteilter Scope-Zustand).
