# BUG (PRE078): Runtime mappt `Tenant`-TPH-Root → Tabelle `Tenant` statt `Tenants` → `Invalid object name 'Tenant'` beim Login

> ## ✅ GELÖST (Toolkit-Seite, 2026-06-17) — Fix in PRE079
>
> **Root Cause exakt bestätigt** (Voll-Bootstrap-Repro: echter `WebPartManager` + MLM-`appsettings-parts.json`
> + echter `ApplicationDbContext`): Zur **Laufzeit** zieht das Anwenden eines **Onboarding-Global-Filters**
> (reproduzierbar bereits durch **einen einzigen** Filter auf `HierarchyEmployeeRoleMapping`) den konkreten
> Basistyp **`Tenant`** als eigene Entity ins Modell. Mechanismus: Der Filter wird in
> `…OnModelCreating` Z. 1132 (`modelBuilderOptions.ConfigureModelBuilder`) **früh** angewandt — noch **vor**
> dem konsumenten-seitigen `ConfigureOnboardingModel()`. Die dadurch ausgelöste **konventionsgetriebene**
> Auflösung der `EmployeeRoleMapping.Tenant`-Navigation zieht den **schlüsseldefinierenden** Basistyp
> `Tenant` (`[Key] TenantId`) ein → TPH-Wurzel `Tenant` → Tabelle nach Root = **`Tenant`**. Die
> Security-Filter lösen es **nicht** aus (ihre `.Tenant`-Beziehungen sind zum Filterzeitpunkt bereits an
> `HierarchyTenant` gebunden). Design-Time (keine WebPart-Filter) trifft den Auslöser nie → dort immer `Tenants`.
> Das erklärt **beide** Symptome (SqlException + „pending model changes", da Runtime-Modell ≠ Snapshot).
>
> **Fix (Fix-Richtung 2, bevorzugt):** Im **Tree**-Security-Context (`AspNetTreeSecurityContext<T>.OnModelCreating`)
> wird der Basistyp jetzt explizit aus dem Modell genommen:
> `modelBuilder.Ignore<…Shared.Models.Tenant>();` (direkt nach `TableNamesFromProperties`). Damit ist
> `HierarchyTenant` in **beiden** Welten der alleinige Root → Tabelle `Tenants`, **keine** TPH, **kein**
> Discriminator. Nur im Tree-Context (Flat/Basic nutzen `Tenant` direkt — dort unverändert).
>
> **Schema unverändert → keine neue Migration.** Verifiziert: Runtime-Modell == Design-Time-Modell für
> `HierarchyTenant` (Tabelle `Tenants`, identische Spalten `TenantId,DisplayName,ParentTenantId,TenantDirty,
> TenantName,TenantPassword,TenantTypeId,TimeZone`, Navigationen `Children,ParentTenant,TenantType`); Basistyp
> `Tenant` in **keiner** der beiden Welten im Modell. Damit entfällt auch das „pending model changes"-Delta.
>
> → Konsument: auf **PRE079** aktualisieren; **kein** MLM-Code-/Migrations-Change nötig.

---


> **Gemeldet aus der MLM-Konsumenten-Session, 2026-06-17.** Folgefund nach dem PRE078-Fix des
> `Role.PermissiveRoles`-Blockers (`BUG-PRE077-Role-PermissiveRoles.md`): die App **startet** jetzt
> sauber, aber die **erste tenant-berührende Query nach erfolgreichem Login** schlägt fehl. Gleiche
> Klasse wie der Role-Bug: eine **Runtime-Model-Regression** (vermutlich aus dem
> `TenantSecurity 10→3`-Konsolidierungs-Merge), die im Design-Time-Model **nicht** auftritt.

## Symptom (Konsument, beim Login)

```
A database operation failed while processing the request.
SqlException: Invalid object name 'Tenant'.

There are pending model changes  (ApplicationDbContext)
```

- Tritt **nach** erfolgreicher Credential-Prüfung auf, beim Auflösen der Tenants/Scopes des Users
  (Security-Pipeline / `IPermissionScope` / `DbSecurityRepository`).
- App-Start selbst ist sauber (HTTP `/` + `/Account/Login` = 200); der Fehler kommt erst bei der
  ersten Query gegen die Tenant-Tabelle.

## Bestätigte Fakten

| Ebene | Tenant-Tabelle | Beleg |
|---|---|---|
| **DB** (LocalDB) | `Tenants` (Plural) | `sys.tables`: `Tenants` vorhanden, **kein** `Tenant` |
| **Migration / Snapshot** (Design-Time) | `Tenants` | `ApplicationDbContextModelSnapshot.cs`: `HierarchyTenant` → `b.ToTable("Tenants")`; **kein** `Discriminator`, **kein** `HasBaseType`, **keine** separate `Tenant`-Entity |
| **Runtime** | `Tenant` (Singular) | `SqlException: Invalid object name 'Tenant'` |

- `…TenantSecurity.Shared.Models.Tenant` ist **konkret** (nicht abstrakt), `[Key] int TenantId`.
- `…TreeShared.Models.HierarchyTenant : Tenant` (konkrete Ableitung) → bei EF ist das **TPH**, sobald
  **beide** Typen im Model sind; die Tabelle der Hierarchie wird über den **Root** (`Tenant`) benannt.
- `ModelBuilderExtensions.TableNamesFromProperties(this)` (im Security-`OnModelCreating`) setzt
  `ToTable(<DbSet-Property-Name>)` nur für die **DbSet-Typen** — also `DbSet<HierarchyTenant> Tenants`
  → `entityConfig = builder.Entity(HierarchyTenant); entityConfig.ToTable("Tenants")`. Das benennt die
  **abgeleitete** Entity, **nicht** den TPH-Root `Tenant`.

## Root-Cause-Schluss

- **Design-Time** (Migrations/Snapshot über die schlanke `ApplicationDbContextFactory`, 2-Arg-Ctor,
  ohne WebPart-Configurators): nur `HierarchyTenant` ist im Model → `HierarchyTenant` ist sein **eigener**
  Root → `ToTable("Tenants")` greift → **`Tenants`**. ✔ stimmt mit DB überein.
- **Runtime** (6-Arg-Ctor, `useFilters=true`, voller WebPart-/DI-`modelBuilderOptions`-Configurator-Satz,
  angewandt via `…OnModelCreating` Z. 1132 `modelBuilderOptions.ConfigureModelBuilder(modelBuilder)`):
  Die **runtime-only** registrierten Configurators ziehen den **Basistyp `Tenant`** als eigene Entity ins
  Model. Damit wird `Tenant` zum **TPH-Root** der `Tenant`/`HierarchyTenant`-Hierarchie; die
  Hierarchie-Tabelle heißt jetzt nach dem Root = **`Tenant`**. `TableNamesFromProperties` hat nur die
  abgeleitete `HierarchyTenant` auf `"Tenants"` gesetzt — das benennt den **TPH-Root nicht** → die
  effektive Tabelle wird `Tenant` → `Invalid object name 'Tenant'`.
- Das erklärt **beide** gemeldeten Punkte: den SqlException **und** „There are pending model changes"
  (Runtime-Model ≠ Snapshot, weil das Snapshot aus dem Design-Time-Model ohne Basis-`Tenant` stammt).

**Offen/zu prüfen (Toolkit-Seite):** *welcher* runtime-only Configurator den Basistyp `Tenant` ins Model
zieht. Verdacht: einer der `ConfigureGlobalFilter<…>`-Ausdrücke (`Shared`/`TreeShared`
`GlobalFilterBuilder.cs`) referenziert eine `.Tenant`-Navigation, die **nach dem 10→3-Merge** auf den
**Basistyp `Tenant`** statt auf `TTenant`/`HierarchyTenant` typisiert ist (oder ein neuer FK/Include auf
`Tenant`). Sobald eine solche Referenz den Basistyp einbringt, kippt die TPH-Root-Benennung. (Die
Filter selbst laufen nur zur Runtime, da sie über die DI-`modelBuilderOptions` registriert werden — daher
Design-Time grün.)

## Reproduktion

1. Konsument auf `5.0.0-PRE078`, Strategy **Tree**, `ActivateFilters: true`, eigener Security-Context
   `: AspNetTreeSecurityContext<…>`.
2. App startet (Model-Validierung grün dank PRE078-Role-Fix).
3. **Mit gültigen Credentials** einloggen → erste Tenant-Auflösung → `Invalid object name 'Tenant'`.
   (Mit *ungültigen* Credentials bleibt es beim User-Lookup `FROM [Users]` und der Fehler tritt **nicht**
   auf — der Tenant-Pfad wird erst nach erfolgreicher Auth betreten. Konsumenten-Session konnte deshalb
   den exakten Stacktrace der Tenant-Query nicht abgreifen; Modell-Diagnose oben ist aber eindeutig.)

## Vorgeschlagene Fix-Richtungen (Toolkit)

1. **TPH-Root-Tabelle explizit benennen:** im Security-`OnModelCreating`
   `modelBuilder.Entity<Tenant>().ToTable("Tenants")` (auf dem **Root**-Typ), bzw. `TableNamesFromProperties`
   so erweitern, dass für TPH-Hierarchien der **Root** benannt wird, nicht der DbSet-(Derived-)Typ.
   Robust gegen „Basis-Tenant ist im Model".
2. **Basistyp gar nicht einbringen:** sicherstellen, dass die Runtime-Configurators (Global-Filter/FKs)
   konsequent `TTenant`/`HierarchyTenant` referenzieren, nicht den Basistyp `Tenant` — dann bleibt das
   Runtime-Model wie Design-Time (single entity → `Tenants`). Bevorzugt, weil es auch das
   „pending model changes"-Delta beseitigt.
3. Gegencheck im 10→3-Merge: wurde eine `.Tenant`-Navigation oder ein `ConfigureGlobalFilter<…>`-Lambda
   von `HierarchyTenant`/`TTenant` auf den Basistyp `Tenant` umgestellt?

## Konsumenten-Status (MLM)

- PRE078 + Leitfaden-§0 vollständig umgesetzt, Build grün, App startet, `/`+`/Account/Login`=200.
- **Login blockiert** durch obigen `Tenant`-Tabellen-Fehler.
- **Kein sauberer Consumer-Fix:** `builder.Entity<Tenant>().ToTable("Tenants")` im konsumierenden
  `OnModelCreating` würde den Basistyp `Tenant` aber auch **design-time** ins Model ziehen → neues
  TPH-/Discriminator-Delta + evtl. Migration → riskant, daher **nicht** angewandt. Wir warten auf den
  Toolkit-Fix.
