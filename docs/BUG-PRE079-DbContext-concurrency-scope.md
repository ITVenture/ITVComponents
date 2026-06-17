# BUG (PRE079): „A second operation was started on this context instance" bei der Scope-Auflösung (Login/NavMenu)

> **Gemeldet aus der MLM-Konsumenten-Session, 2026-06-17.** Folgefund nach dem PRE079-Tenant-Fix
> (`BUG-PRE078-Tenant-table-name.md`): Login läuft jetzt durch, aber beim Rendern (`NavMenu`,
> Permission-Check) wirft die Security-Pipeline `InvalidOperationException: A second operation was
> started on this context instance …`. Gleiche Klasse wie die früheren DbContext-Re-Entry-Fälle.

## Symptom (Stack, gekürzt)

```
NavMenu.OnInitialized → VerifyUserPermissions → IsAuthenticated
  → AspNetTreeSecurityContext.get_CurrentTenantId → get_CurrentTenant → PermissionScope.PermissionPrefix
  → ResolvingPermissionScope.GetCurrentScope → ReadScopeToken → GetEligibleScopes
  → DbSecurityRepository.GetEligibleScopes → DbSecurityAccessProvider.CreateForCaller
  → CreateForCallerInternal → (TrustedFullAccessComponents-Query) → SingleQueryingEnumerable.MoveNext()
  → InvalidOperationException: A second operation was started on this context instance
```

## Root Cause

`DbSecurityRepository`, `AspNetTreeSecurityContext` (die `CurrentTenantId`) und
`DbSecurityAccessProvider.SecurityDb` teilen sich **dieselbe circuit-scoped DbContext-Instanz**.

- `IsAuthenticated` liest als **erste** Zeile `securityContext.CurrentTenantId` — **vor** dem
  `isAuthenticatedCache`-Memoize. D.h. die teure Scope-Auflösung läuft ungeschützt bei jedem Aufruf.
- `CurrentTenantId` → `CurrentTenant` → `PermissionPrefix` → `GetCurrentScope` → `ReadScopeToken`.
  Beim **ersten** (neu/stale) Token pro Circuit ruft das `GetEligibleScopes`, das (a) eine
  Haupt-Query und (b) `CreateForCaller` (Trust-Lookup `TrustedFullAccessComponents.FirstOrDefault` =
  **DB-Query**) auf dem **geteilten** Context absetzt.
- Blazor Server führt Lifecycle-Callbacks (NavMenu + AuthenticationStateProvider/andere Komponenten)
  **nebenläufig** auf demselben scoped Context aus → während Flow A die Trust-/Scope-Query enumeriert,
  hat Flow B noch eine Operation offen → „second operation".

Die bestehenden Schutzmechanismen greifen hier nicht: `isAuthenticatedCache` deckt nur
`IsAuthenticatedCore` (läuft *nach* der Scope-Auflösung); der `resolvingCurrentTenantId`-Guard schützt
nur das `Tenants.FirstOrDefault` *innerhalb* von `CurrentTenantId`, nicht die `PermissionPrefix`-
Auflösung davor; das Scope-Token cached die `EligibleScopes` erst **nach** der ersten (parallel
rennenden) Auflösung.

## Fix (Toolkit, → PRE080)

**B — Scope-Reads auf dedizierter Context-Instanz** (analog Navigation-Builder f53cae61):
`DbSecurityRepository.GetEligibleScopes` (Tree) läuft jetzt auf einer **frischen, kurzlebigen**
Context-Instanz (`ActivatorUtilities.CreateInstance` im selben DI-Scope → selbe
`IPermissionScope`/`IContextUserProvider`, korrekt tenant-gefiltert, aber kollisionsfrei) statt auf
der geteilten `securityContext`-Instanz. Die Trust-Elevation (`CreateForCaller`) zielt ebenfalls auf
die detached Instanz; nach dem Build wird sie disposed. (Dafür bekommt das Tree-Security-Repository
den `IServiceProvider` injiziert — Konstruktor + `UseDbIdentities`-Factory angepasst, abwärtskompatibel
per Default-Parameter.)

**C — Trust-Lookup-Cache** (`DbSecurityAccessProvider`): die `TrustedFullAccessComponents` (globale,
selten geänderte Config) werden **einmal** pro Circuit in einen Dictionary-Cache geladen — und zwar
auf einer **dedizierten** Context-Instanz mit `IgnoreQueryFilters()` (so kann der Load weder den
geteilten Context belasten noch `CurrentTenantId`/Scope re-entrant auslösen). `CreateForCaller` (das
pro Render dutzendfach läuft) bedient sich danach aus dem Cache statt jedes Mal die DB abzufragen.
Damit verschwindet die im Stack gemeldete Query ganz aus dem Hot-Path.

Zusammen: die Scope-Auflösung berührt den geteilten Circuit-Context für DB-Reads **gar nicht** mehr →
die Race ist by-construction beseitigt.

**Trade-off (C):** der Trust-Cache ist circuit-scoped und wird nicht invalidiert; Änderungen an der
Trust-Konfiguration greifen erst nach Circuit-Neustart. Akzeptabel, da Trust-Components Infrastruktur-
Config sind (Cross-Assembly-Trust), die zur Laufzeit praktisch nie geändert wird.

## Verifikation (Stand 2026-06-17)

- Kompiliert sauber (TenantSecurity + SqlServer + Onboarding), keine weiteren Aufrufer/Subklassen der
  geänderten Konstruktoren.
- Modell baut weiter (Tenant-Fix intakt); das modifizierte Repo wird zur Laufzeit konstruiert.
- Das `ActivatorUtilities`-Detached-Context-Muster ist identisch zum bereits ausgelieferten,
  produktiv laufenden Navigation-Fix (f53cae61).
- **Nicht** im Harness reproduzierbar ist die eigentliche Race (braucht echte Nebenläufigkeit + befüllte
  DB). Der Fix wirkt by-construction (kein Shared-Context-DB-Zugriff mehr im Scope-Pfad); finaler
  Beweis = Host-Test beim Konsumenten.

→ Konsument: auf **PRE080** aktualisieren; kein MLM-Code-/Migrations-Change nötig.
