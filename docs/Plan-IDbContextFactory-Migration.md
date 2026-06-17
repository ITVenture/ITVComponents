# Plan: Umstellung auf `IDbContextFactory` (Blazor-taugliche DbContext-Lebensdauer)

> Status: **Plan / freigegeben 2026-06-17**, Branch `Future_10`. Beide Seiten (Toolkit 5.0.0 + MLM) sind
> Preview → richtiger Zeitpunkt für die Fundament-Umstellung. Voraus-Arbeit (PRE079/PRE080) hat die
> akute „second operation"-Crash-Klasse im Security-Read-/Render-Pfad bereits per detached Reads entschärft;
> **diese** Migration ist die saubere Fundament-Architektur (gegen das EF-Anti-Pattern „langlebiger DbContext
> in Blazor": stale data, Change-Tracker-Bloat), nicht ein Crash-Notfall.

## 0. Das Leitprinzip (entscheidet den ganzen Scope)

| | Scope-Bedeutung | scoped DbContext direkt | Konsequenz |
|---|---|---|---|
| **MVC** | pro **Request** (kurz, single-flow) | **ok** | bleibt wie es ist |
| **Blazor Server** | pro **Circuit** (langlebig, nebenläufig) | **Anti-Pattern** | muss per-Operation/Factory |

Daraus die zwei tragenden Regeln (aus der a/b-Klärung):

- **Regel A — geteilte Datenzugriffs-Services:** Services, die Blazor *nebenläufig oder langlebig* nutzt,
  verwenden `IDbContextFactory<TContext>` + per-Operation. Das ist **auch in MVC korrekt** → einmal
  migrieren, beide Welten bedient, keine Doppelpflege. Reiner MVC-Code bleibt unangetastet. Zustandslose
  Services sind irrelevant.
- **Regel B — MVC-Kompatibilität via Shim:** `AddScoped<TContext>(sp => factory.CreateDbContext())` lässt
  MVC-Controller **unverändert** scoped `TContext` injizieren (1 Context/Request, kurz, DI-disposed). Derselbe
  Shim ist für Blazor nur ein **Übergangs-Krücke** (kompiliert weiter), macht Blazor aber **nicht** sicher.
  **Blazor-Code darf am Ende `TContext` nicht direkt injizieren — nur die Factory.**

## 1. Der technische Dreh- und Angelpunkt: Scoped-Lifetime-Factory

Der Security-Context trägt scoped State (IPermissionScope = aktueller Mandant, IContextUserProvider =
aktueller User); die globalen Query-Filter hängen daran. Die Factory muss daher **scoped** sein, damit
erzeugte Contexts den Scope-State bekommen.

**Wichtige Korrektur (Phase-0-Durchdenken):** Der **Standard-`AddDbContextFactory<T>` funktioniert hier
nicht** — er erwartet einen Konstruktor mit *nur* `DbContextOptions<T>`. Unser Context hat aber den
mehrarg. Runtime-Ctor (IPermissionScope, IContextUserProvider, …). → Wir verwenden eine **eigene
`IDbContextFactory<T>`-Implementierung auf Basis von `ActivatorUtilities.CreateInstance`** (genau der
Mechanismus, der via CreateDetachedContext schon läuft — wählt den reichsten Ctor inkl. Scope-Deps),
**scoped** registriert. `CreateDbContext()` liefert eine frische, per-Operation Instanz mit korrektem
Mandant/User-State.

Verdrahtung (additiv, risikoarm): **`AddDbContext<T>` bleibt** (= scoped `TContext` für MVC/Transition →
Regel B ist damit automatisch erfüllt, kein extra Shim nötig); die eigene `IDbContextFactory<T>` kommt
**daneben** (nutzt dieselben `DbContextOptions<T>`, kein Doppel-Register). Phase 4 entfernt später für
Blazor-Hosts den scoped `TContext` (nicht die Options/Factory).

## 2. Der harte Teil: Edit-Flows der Blazor-Admin-Handler

Heutiger Stand: **~25–40 Blazor-Admin-Handler** (`Blazor.MudBlazor.AdminViews`, TenantSecurityViews +
UserViews + OnboardingViews) injizieren je direkt `TContext db` und nutzen ihn für Lesen **und** Schreiben
(`SaveChanges`) über die Handler-Lebensdauer. Per-Operation-Context heißt: kein Change-Tracking über
UI-Roundtrips → Edit-Flows auf **„laden (detached) → im UI editieren → neuer Context → Attach/Update →
Save"** umstellen. **Zentralisierbar** in einem Helper/Handler-Basis, damit es nicht 30× neu erfunden wird.

(Die MVC-Telerik-Handler `Net.TelerikUi.AdminViews` sind ein **separates** Paket, MVC-only → bleiben dank
Regel B unangetastet.)

## 3. Phasenplan (jede Phase einzeln lieferbar/committbar)

- **Phase 0 — Factory bereitstellen, rein additiv, kein Verhaltenswechsel.**
  Eigene `ToolkitDbContextFactory<TContext>` (ActivatorUtilities-basiert) implementieren; in
  `UseDbIdentities<TImpl>` (CoreIdentityTree/Extensions/DependencyExtensions.cs) **zusätzlich** zur
  bestehenden `AddDbContext<TImpl>`-Registrierung als `AddScoped<IDbContextFactory<TImpl>>(...)` registrieren.
  Alle bestehenden Konsumenten (MVC + Blazor) laufen **unverändert** weiter (scoped TContext bleibt).
  Risiko: minimal (additiv). **Verifikation:** App startet; `IDbContextFactory<TImpl>` resolved; `CreateDbContext()`
  liefert eine vom scoped Context **verschiedene** Instanz mit korrektem Mandant/User-State.

- **Phase 1 — Security-Read-Pfad auf Factory. (ÜBERSPRUNGEN — kosmetisch.)**
  Die `ActivatorUtilities`-Detached-Helfer (`CreateDetachedContext`, `ReadDetached`, `ResolveTenantIdDetached`,
  Trust-Cache-Load) **sind bereits** exakt der Factory-Mechanismus. Eine Umstellung auf `IDbContextFactory`
  hat generische-Typparameter-Reibung (das generische Repo kennt `TImpl` nicht, nur den 46er-Interface-Typ;
  die Factory ist `IDbContextFactory<TImpl>`) bei **null** funktionalem Gewinn → bewusst übersprungen. Die
  Helfer bleiben; optional später vereinheitlichen.

- **Phase 2 — Pilot: ein Blazor-Admin-Handler (Liste + Edit).**
  Einen repräsentativen Handler auf Factory-per-Operation + **zentralen Detached-Edit-Helper** umstellen.
  Etabliert das Konsumenten-Muster + Boilerplate-Reduktion. Hier wird das Edit-Pattern festgenagelt.
  **Entscheidungspunkt:** danach Aufwand/Pattern real bewerten, dann über Phase 3/4 entscheiden.

- **Phase 3 — Rollout auf alle Blazor-Admin-Handler.** Mechanisch, aber breit (~30 Handler). Pro Handler:
  Factory-Injektion, per-Operation-Context, Detached-Edit-Helper.

- **Phase 4 — Default für Blazor umlegen.** Blazor-Host registriert den scoped `TContext`-Shim nicht mehr
  (nur Factory) → Blazor-Code kann `TContext` nicht mehr versehentlich direkt injizieren. MVC-Hosts behalten
  den Shim (permanent). MLM zieht nach.

## 4. Konsumenten-Regeln (Endzustand)

- **MVC-Controller/-Handler:** injizieren weiter scoped `TContext` (aus dem Shim). Keine Änderung.
- **Toolkit-Blazor-Handler/-Komponenten:** injizieren `IDbContextFactory<TContext>`, `using var db =
  factory.CreateDbContext()` pro Operation; Edits über den Detached-Helper.
- **Geteilte Datenzugriffs-Services (Repo/Navigation/Scope):** Factory intern (Phase 1).
- **Plugin-DbContexts:** siehe [[plugin_dbcontext_blazor_risk]] — in Blazor per-Operation/Factory, nicht
  circuit-geteilt.

## 5. Risiken / offene Punkte
- **Scoped-Factory mit scoped Ctor-Deps:** verifizieren, dass `AddDbContextFactory<T>(Scoped)` die scoped
  IPermissionScope/IContextUserProvider in erzeugte Contexts injiziert (erwartet ja). → Phase 0.
- **Edit-Flow-Korrektheit:** detached Attach/Update muss Concurrency-Token/Beziehungen korrekt behandeln →
  Pilot (Phase 2) + Host-Test.
- **MLM-Migration:** MLM-eigene Blazor-Komponenten, die ApplicationDbContext direkt injizieren, müssen in
  Phase 4 nachziehen (Preview → vertretbar). Im Migrations-Leitfaden dokumentieren.
- **Verifikation generell:** wie bei den Concurrency-Fixes ist die echte Race/Last nur host-testbar; Build +
  Pilot-Host-Test sind die Gates.

## 6. Reihenfolge des Loslegens
Phase 0 ✅ (Commit 848f9492) → Phase 1 übersprungen (kosmetisch) → **Phase 2 (Pilot) = nächster Schritt**.
Dann Review-Punkt: Aufwand Phase 3/4 final bewerten und einplanen.
