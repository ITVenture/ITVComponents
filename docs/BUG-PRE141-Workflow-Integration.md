# BUG (PRE141): Workflow-Einbindung — zwei Blocker + drei Doku-Lücken im Einbindungs-Leitfaden

> **Gemeldet aus der MLM-Konsumenten-Session, 2026-07-27.** Erste Adoption des Workflow-Subsystems
> nach `docs/Workflow-Integration-Guide.md`, **Szenario a) Web-Only**, **globaler (filterfreier)
> Kontext**, Schema in der **bestehenden** System-DB des Hosts (keine separate Workflow-DB).
>
> Ergebnis: die Einbindung läuft (Routen erreichbar, Runner pollt sauber), aber **zwei Punkte des
> Leitfadens sind so nicht lauffähig** — einer davon verhindert den Host-Start komplett. Alle Punkte
> sind konsumenten-seitig umgangen; die Umgehungen stehen jeweils dabei, damit ihr sehen könnt, ob ihr
> sie library-seitig übernehmen wollt.

## Übersicht

| # | Was | Schwere | Betroffen |
|---|---|---|---|
| 1 | `AddDbContextFactory<WorkflowContext>` verhindert den Host-Start (3 ctors, `ActivatorUtilities` kann nicht wählen) | **Blocker** | Library + Leitfaden §2 |
| 2 | Leitfaden-Code `sp.GetRequiredService<PluginFactory>()` — `PluginFactory` ist nirgends in der DI | **Blocker** (Leitfaden-Code) | Leitfaden §3 |
| 3 | `WorkflowContextDesignTimeHelper` verdrahtet hart eine **fremde** DB (`IWCWorkflow`) | Doku/Design | Library + Leitfaden §6 |
| 4 | Shared-DB-Deployment: kein Wort zu `MigrationsHistoryTable`; dazu generische Tabellennamen | Doku/Design | Leitfaden §6 |
| 5 | Monaco-Skripte: der Leitfaden verweist auf „wie die AdminViews", die es beim Konsumenten nicht gab | Doku | Leitfaden §2.4 — **erledigt**, Modul liefert die Skripte selbst |

---

## 1. `AddDbContextFactory<WorkflowContext>` killt den Host-Start — **Blocker**

### Symptom

```
fail: Microsoft.Extensions.Hosting.Internal.Host[11]
      Hosting failed to start
      System.InvalidOperationException: Multiple constructors accepting all given argument types
      have been found in type 'ITVComponents.Workflow.EntityFramework.WorkflowContext'.
      There should only be one applicable constructor.
         at Microsoft.Extensions.DependencyInjection.ActivatorUtilities.TryFindMatchingConstructor(...)
         at Microsoft.EntityFrameworkCore.Internal.DbContextFactorySource`1.CreateActivator()
         ...
         at ITVComponents.Workflow.EntityFramework.EfWorkflowStore.ReleaseLocksOfOwner(String owner)
```

Der Fehler kommt **nicht** beim ersten Workflow-Zugriff, sondern beim Hochfahren, sobald irgendetwas
die Factory zieht (bei uns der `WorkflowRunner` in seinem Start-Aufräumlauf). Die ganze App startet
nicht mehr.

### Root Cause

`WorkflowContext` hat drei public Konstruktoren
(`ITVComponents.Workflow.EntityFramework/WorkflowContext.cs:219`, `:226`, `:238`):

```csharp
public WorkflowContext(DbContextOptions<WorkflowContext> options)
public WorkflowContext(DbContextOptions options, DbContextModelBuilderOptions<WorkflowContext> modelOptions,
                       IUserAwareContext userContext)
public WorkflowContext(ContextOptionsLoader<WorkflowContext> dbOptions, IUserAwareContext userContext,
                       bool useTenantFilter, IOptions<DbContextModelBuilderOptions<WorkflowContext>> modelOptions)
```

Die Default-Factory hinter `AddDbContextFactory<TContext>` instanziert über `ActivatorUtilities` und
bricht bei dieser Mehrdeutigkeit ab. Derselbe Grund lässt auch **`dotnet ef`** scheitern
(„Multiple constructors …" beim `DbContext`-Discovery), solange keine Design-Time-Factory existiert.

Der Leitfaden setzt `IDbContextFactory<WorkflowContext>` an mehreren Stellen als gegeben voraus
(§2.2 Store-Registrierung, §2.5 `AddDependency`-Beispiel), sagt aber nirgends, **wie** die Factory zu
registrieren ist — und der naheliegende Weg funktioniert eben nicht.

### Umgehung beim Konsumenten

Eigene `IDbContextFactory<WorkflowContext>`-Implementierung, die den options-only-ctor **explizit** ruft,
registriert über die Zwei-Typ-Überladung:

```csharp
public sealed class WorkflowContextFactory : IDbContextFactory<WorkflowContext>
{
    private readonly DbContextOptions<WorkflowContext> options;
    public WorkflowContextFactory(DbContextOptions<WorkflowContext> options) => this.options = options;
    public WorkflowContext CreateDbContext() => new(options);
}

builder.Services.AddDbContextFactory<WorkflowContext, WorkflowContextFactory>(o => o.UseSqlServer(...));
```

Nebeneffekt, der hier sogar erwünscht ist: der options-only-ctor lässt `modelOptions` null → **kein
Tenant-Query-Filter**, also genau der globale/filterfreie Kontext, den §2.5 für die Inline-Ausführung
verlangt.

### Vorschlag

Entweder (a) eine `AddWorkflowContextFactory(...)`-Extension mitliefern, die das kapselt — dann ist der
globale Fall ein Einzeiler und die ctor-Wahl nicht mehr dem Konsumenten überlassen; oder (b) die ctors
entschärfen (z.B. zwei davon `protected`/`internal`, oder per `[ActivatorUtilitiesConstructor]` am
options-only-ctor markieren); mindestens aber (c) den Leitfaden um die Factory-Registrierung ergänzen.
Variante (b) mit `[ActivatorUtilitiesConstructor]` wäre die kleinste Änderung und würde
`AddDbContextFactory<WorkflowContext>` **und** `dotnet ef` in einem Zug reparieren.

### Erledigt

Variante (b) ist umgesetzt: der options-only-Ctor trägt `[ActivatorUtilitiesConstructor]`, damit ist
`AddDbContextFactory<WorkflowContext>` ohne eigene Factory-Implementierung wieder möglich (die
konsumenten-seitige `WorkflowContextFactory` bleibt gültig — sie ruft den Ctor ohnehin explizit).

Dazu ein vierter Ctor für den DI-Weg, der den Tenant-Schalter als `IOptions<WorkflowContextOptions>`
statt als `bool` nimmt und per Ctor-Verkettung auf den bool-Ctor umleitet — der bleibt für den
Plugin-/Mehr-Umgebungs-Weg der praktischere. **Wichtig für die Wahl:** die Factory ist und bleibt der
*filterfreie* Weg. Wer die Mandanten-Filterung in den Views will, muss den Kontext über einen der beiden
tenant-fähigen Ctors bauen — siehe die Tabelle in `Workflow-Integration-Guide.md` §2.1.

---

## 2. `sp.GetRequiredService<PluginFactory>()` aus dem Leitfaden existiert nicht — **Blocker**

### Symptom

Der Leitfaden-Code für den Editor-Katalog (`docs/Workflow-Integration-Guide.md:173-174`, identisch in
§3):

```csharp
services.AddSingleton<IWorkflowActivityCatalog>(sp =>
    new PluginActivityCatalog(sp.GetRequiredService<PluginFactory>()));
```

wirft zur Laufzeit, weil **`PluginFactory` in der gesamten Toolkit-DI nirgends registriert ist**.
Repo-weiter Gegencheck: kein einziges `AddSingleton<PluginFactory>` / `AddScoped<PluginFactory>` /
`AddTransient<PluginFactory>`.

### Root Cause

Die `PluginFactory` kommt ausschließlich aus dem **scoped** `IWebPluginHelper`
(`ITVComponents.WebCoreToolkit/Extensions/DependencyExtensions.cs:68` registriert
`AddScoped<IWebPluginHelper, WebPluginHelper>`; `WebPluginHelper.GetFactory()` liefert sie).
`PluginActivityCatalog` verlangt sie aber direkt im ctor
(`ITVComponents.Workflow.Plugins/PluginActivityCatalog.cs:31`).

Dazu kommt: selbst wenn `PluginFactory` in der DI läge, wäre die Registrierung als **Singleton** falsch —
sie hinge an einer scoped Abhängigkeit (Captive Dependency, unter Blazor tenant-übergreifend falsch).

### Umgehung beim Konsumenten

Scoped registrieren und die Factory über den Helper ziehen:

```csharp
builder.Services.AddScoped<IWorkflowActivityCatalog>(sp =>
    new PluginActivityCatalog(sp.GetRequiredService<IWebPluginHelper>().GetFactory()));
```

Das passt auch besser zum Konsumpfad: der Editor holt den Katalog ohnehin scope-lokal
(`…WorkflowViews/Design/Components/WorkflowEditor.razor:273`:
`catalog ??= Services.GetService<IWorkflowActivityCatalog>();`), nie als Singleton-Konstruktorabhängigkeit.

### Vorschlag

Leitfaden-Snippet auf `IWebPluginHelper.GetFactory()` **und** `AddScoped` korrigieren. Alternativ einen
ctor-Overload `PluginActivityCatalog(IWebPluginHelper)` anbieten, dann bleibt das Snippet kurz.

---

## 3. Design-Time-Factory verdrahtet eine fremde Datenbank

`ITVComponents.Workflow.EntityFramework.SqlServer/Designer/WorkflowContextDesignTimeHelper.cs:22`:

```csharp
optionsBuilder.UseSqlServer(
    @"Server=(localdb)\mssqllocaldb;Database=IWCWorkflow;Trusted_Connection=True;",
    so => so.MigrationsAssembly(typeof(WorkflowContextDesignTimeHelper).Assembly.FullName));
```

Für euer Scaffolding ist das richtig. Für den Konsumenten ist es eine **stille Falle**: wer die
Workflow-Migrationen ins Host-Schema anwenden will und sich auf die mitgelieferte Factory verlässt,
legt eine zweite Datenbank `IWCWorkflow` an und wundert sich, warum im Host-Schema nichts ankommt.
Der Klassenkommentar sagt zwar, dass der Host Provider und Connection selbst wählt — aber im
Migrations-Abschnitt §6 (Zeile 393 ff.) steht nicht, dass man dafür eine **eigene**
`IDesignTimeDbContextFactory<WorkflowContext>` braucht.

Wir haben eine eigene Design-Time-Factory im Host-Projekt angelegt (löst zugleich Punkt 1 fürs Tooling).

**Vorschlag:** in §6 einen Halbsatz ergänzen — „für `dotnet ef` im Host eine eigene
`IDesignTimeDbContextFactory<WorkflowContext>` bereitstellen; die im Provider-Paket mitgelieferte zeigt
bewusst auf eine Scaffolding-DB". Optional die Toolkit-Factory auf `internal` setzen, dann kann sie beim
Konsumenten gar nicht erst greifen.

---

## 4. Shared-DB-Deployment: `MigrationsHistoryTable` fehlt, Tabellennamen sind generisch

Der Leitfaden behandelt in §6 nur `MigrationsAssembly`. Der aus unserer Sicht häufigste Fall —
**Workflow-Schema in der ohnehin vorhandenen Anwendungs-DB** — bringt aber zwei Punkte mit, die der
Konsument selbst entscheiden muss:

**(a) Gemeinsame `__EFMigrationsHistory`.** Ohne eigene History-Tabelle buchen Toolkit-Migrationen in
dieselbe Buchhaltung wie die Migrationen des Hosts. Funktional geht das gut, aber fremd- und
selbstverwaltete Migrationen sind danach nicht mehr auseinanderzuhalten (relevant bei Rollback,
Aufräumen, Diffing). Repo-weiter Gegencheck: `MigrationsHistoryTable` / `HistoryRepository` kommt im
gesamten Workflow-EF-Projekt **nicht** vor. Wir setzen konsumenten-seitig
`sql.MigrationsHistoryTable("__EFMigrationsHistoryWorkflow")` — wichtig: **in der Laufzeit- UND der
Design-Time-Registrierung identisch**, sonst sucht das Tooling in der falschen Tabelle und will
`InitialWorkflow` erneut anwenden.

**(b) Generische Tabellennamen.** `InitialWorkflow` legt u.a. **`Tokens`**, **`HistoryEntries`** und
**`BranchLocks`** ohne Präfix und ohne eigenes Schema an. In einer geteilten DB ist das ein reales
Kollisionsrisiko (`Tokens` insbesondere). Wir haben vor dem Anwenden geprüft — in unserer DB waren alle
fünf Namen frei —, aber das sollte man nicht dem Zufall überlassen.

**Vorschlag:** §6 um einen kurzen Absatz „Workflow-Schema in einer geteilten Datenbank" ergänzen (eigene
History-Tabelle empfehlen, Kollisionsprüfung erwähnen). Für (b) ggf. ein eigenes DB-Schema
(`.HasDefaultSchema("workflow")`) oder Präfixe erwägen — das wäre allerdings ein Breaking Change und
lohnt sich nur, solange das Subsystem noch keine Konsumenten in Produktion hat.

---

## 5. Monaco-Skripte: der Verweis auf „wie die AdminViews" trägt nicht

§2.4 (Zeile 95 ff.) sagt, der CScript-Editor brauche „— wie die AdminViews —" die drei
BlazorMonaco-Skripte im Host. Bei uns hatte der Host **keine** Monaco-Skripte: die AdminViews sind seit
längerem eingebunden, ohne dass je welche nötig waren (`wwwroot/` hatte gar kein `js/`-Verzeichnis, kein
`BlazorMonaco`-PackageReference). Der Verweis führt also eher in die Irre — er suggeriert, das sei schon
erledigt.

Zusätzlich: in `…WorkflowViews/Design/Components/CScriptField.razor:11-14` stehen die drei Script-Tags
in einem `@* … *@`-Kommentar. Das ist als Doku gemeint, liest sich beim Überfliegen der Datei aber wie
gerenderte Markup — mit dem Ergebnis, dass man sie im Host weglässt und die CScript-Felder dann still
nicht rendern.

**Vorschlag:** den Vergleich mit den AdminViews streichen und die drei Tags direkt im Leitfaden als
Copy-Paste-Block zeigen (steht dort schon, nur eben mit dem irreführenden Vorsatz). Denkbar wäre auch,
die Skripte über den Toolkit-Client-Script-Mechanismus auszuliefern — dann müsste allerdings die
**Reihenfolge** garantiert sein (`loader.js` definiert das AMD-`require`, das `editor.main.js` braucht);
wir haben sie deshalb bewusst als feste Tags gesetzt statt über `AddToolkitClientScript`.

> **Erledigt (Library, 2026-07-27) — die zweite Variante, die Bedenken tragen nicht.**
> `WorkflowViews.WebPartInit.RegisterServices` meldet die drei Skripte jetzt selbst per
> `AddToolkitClientScript` an. Die Reihenfolge ist garantiert: `ClientResourceOptions.AddScript`
> hängt in Registrierungsreihenfolge an und ignoriert Duplikate (`ClientResourceOptions.cs:29-38`) —
> deshalb kollidiert es auch nicht damit, dass die AdminViews dieselben drei Zeilen anmelden.
> Der Host braucht nur noch `<ITVentureReferences />` in der statisch gerenderten Host-Seite; die
> handgesetzten Tags könnt ihr entfernen. Der irreführende `@* … *@`-Block in `CScriptField.razor`
> ist durch einen Hinweis auf genau das ersetzt, Leitfaden §2.4 neu geschrieben.
>
> **Achtung, im selben Zug gefunden:** die CScript-Felder wären auch *mit* korrekt geladenem Monaco
> nur wenige Pixel hoch gewesen — `CScriptField.razor` fehlte das CSS, das dem von Monaco erzeugten
> Kind-`div` eine Höhe gibt (die AdminViews haben es in `CodeEditor.razor.css`). Ebenfalls behoben
> (neues `CScriptField.razor.css`). Falls ihr die Felder schon mal gesehen habt und sie „leer"
> wirkten: das war vermutlich das, nicht die Skripte.

---

## Was beim Konsumenten verifiziert ist

Damit ihr wisst, worauf sich die Aussagen stützen — und worauf **nicht**:

- Build 0 Fehler; `has-pending-model-changes` für beide Kontexte clean.
- `InitialWorkflow` in die bestehende System-DB appliziert; 5 Tabellen angelegt, eigene
  History-Tabelle, Host-History unberührt, **keine** `IWCWorkflow`-DB entstanden.
- Routen `/Workflow`, `/Workflow/Instances`, `/Workflow/Definitions`, `/Workflow/Editor/new` liefern
  anonym **401**, erfundene Routen **404** → Routen existieren, `[Authorize]` greift.
- `WorkflowRunner` startet und pollt real gegen `WorkflowInstances`/`Tokens` (1 ms, fehlerfrei).

**Nachtrag (Zweig-Scopes):** Seit dieser Session gibt es eine **zweite Migration** `BranchScopes` (zwei
nullable Spalten auf `Tokens`: `VariablesJson`, `SplitTokenId`) in beiden Provider-Projekten — beim
nächsten Update also mit einzuspielen. Sie ist additiv; bereits laufende Instanzen bleiben gültig und
verhalten sich unverändert, solange beide Spalten null sind. Hintergrund im Leitfaden §8
(„Zweig-Scopes").

**Nachtrag (Benutzer-Aufgaben):** dazu kommt eine **dritte Migration** `UserTasks` (neun nullable
Spalten auf `Tokens` — `TenantId`, `TaskKey`, `TaskPermission`, `AssignedTo`, `TaskTitle`,
`TaskCreatedUtc`, `TaskDueUtc`, `ClaimedBy`, `ClaimedUntil` — plus zwei Indizes) in beiden
Provider-Projekten. Ebenfalls additiv: alle Spalten null = kein Aufgaben-Token, laufende Instanzen
bleiben unverändert. Neu ist ausserdem die Permission **`Workflow.Tasks`** (Benutzer-Arbeitsliste unter
`Workflow/Tasks`) — sie muss wie die drei bestehenden DB-seitig angelegt und Rollen zugeordnet werden,
sonst bleibt die Seite leer. Hintergrund im Leitfaden §9.

**Nicht verifiziert:** kein eingeloggter E2E — Editor-Rendering, Monaco-CScript-Felder, Anlegen/Speichern
einer Definition und Start/Signal einer Instanz sind alle noch offen. Der Aktivitäten-Katalog ist zudem
leer (keine Workflow-Aktivitäten in der `WebPlugins`-Tabelle), Punkt 2 ist also **nur** als
DI-Auflösung geprüft, nicht mit echten Aktivitäten. Wenn ihr Punkt 2 anfasst, wäre das der Test, der
noch fehlt.
