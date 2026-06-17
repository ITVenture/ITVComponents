# EmployeeRoleMapping + Onboarding-Admin-Editoren (Branch `Future_10`)

Companion zu den übrigen `Migration-Future_10-MLM*`-Guides.

Betrifft die **Onboarding-Lib** (`ITVComponents.WebCoreToolkit.EntityFramework.Onboarding`) und
die Blazor-Admin-Views. Toolkit-Commit `41c61b4d`.

> **TL;DR:** **Breaking** (Contract + Schema).
> 1. **Pflicht-Code-Change:** `ApplicationDbContext` braucht **eine neue DbSet-Property**
>    `EmployeeRoleMappings` — sonst Compile-Break (das Onboarding-Interface verlangt sie jetzt).
> 2. **Neue EF-Migration:** neue Tabelle `EmployeeRoleMappings` **plus FK-Umbau an `EmployeeRoles`**
>    (`RoleId` → `EmployeeRoleMappingId`). ⚠️ Bestehende `EmployeeRole`-Zuordnungen verlieren ihren
>    Rollen-Bezug (siehe §3, Daten-Migration).
> 3. **Kein DI-/WebPart-Change nötig** — der Materialisierungs-Interceptor und der Template-Part-Handler
>    registrieren sich automatisch, sobald die Onboarding-Filter aktiv sind (`ActivateFilters`).
> 4. **Betrieb:** Permission `ManageEmployees` vergeben + Rollen-Mappings anlegen (§5).
>
> ⚠️ **Nachtrag 2026-06-16 (siehe §0):** Mit der neuen PRE-Version ist Punkt 3 oben **überholt** —
> es ist jetzt **doch ein `OnModelCreating`-Change Pflicht** (`builder.ConfigureOnboardingModel()`),
> und es kommt eine weitere Migration (`TenantInvitation`-FK) hinzu. Nach dem Paket-Update **zuerst §0 lesen.**

---

## 0. ⚠️ Nachtrag 2026-06-16: Onboarding Model/Filter-Split + UserId/UserMail-Fix (neue PRE-Version, nach PRE076)

> Gilt **zusätzlich** zu allem unten und **korrigiert** die Aussage „keine OnModelCreating-Änderung nötig"
> aus §2/§4. **Gekoppelt ans Paket-Update** — nicht einzeln machbar.

### Was sich im Toolkit geändert hat

1. **UserId/UserMail-Replacer** sind jetzt in den Basis-Security-Contexts (`AspNetTreeSecurityContext` /
   `AspNetSecurityContext`) registriert — aus den Claims `NameIdentifier` / `Email`, überschreibbar via
   `protected virtual UserIdClaimType` / `UserMailClaimType`. Behebt
   `ArgumentException: No replacer found for Property UserId` beim Start mit `ActivateFilters:true`.
   **Konsument muss dafür nichts tun.**
2. **Onboarding: Struktur von Filtern getrennt.** `ConfigureDefaultFilters` (WebPart, gated auf
   `ActivateFilters`) registriert jetzt **nur noch die Query-Filter**. Die **FKs**
   (EmployeeRole→Mapping, Mapping→Tenant/Role, alle `Restrict`) liegen jetzt in der neuen,
   konsumenten-seitigen Extension **`modelBuilder.ConfigureOnboardingModel()`**.
3. **`TenantInvitation.ParentTenantId`** ist jetzt ein **echter** navigationsloser Restrict-FK (kommt
   ebenfalls aus `ConfigureOnboardingModel()`). `ChildTenantId` / `AcceptedByUserId` /
   `CreatedByTenantUserId` bleiben **bewusst** FK-los (Audit-/Snapshot-Spalten).

**Warum:** Der WebPart läuft nicht zur Design-Time → schema-formende Config (FKs), die nur dort sass,
fehlte den Migrations (Drift; außerdem Doppel-Konfiguration zur Laufzeit, mutmaßlicher Auslöser des
`Role.PermissiveRoles`-Modellfehlers). `OnModelCreating` läuft immer — daher gehören die FKs dorthin.

### Pflicht-Code-Change in `ApplicationDbContext.OnModelCreating`

```csharp
base.OnModelCreating(builder);

// NEU & PFLICHT: strukturelles Onboarding-Modell (FKs) — IMMER aufrufen, unabhängig von ActivateFilters.
// using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree.Extensions;
builder.ConfigureOnboardingModel();

// ... Views (nur Design-Time), Passkey, ConfigureBilling(), ConfigureBillingFeatureGrants(),
//     ApplyConfigurationsFromAssembly(...) bleiben unverändert ...
```

- **Entfernen:** die drei manuellen Onboarding-FK-Blöcke (`HierarchyEmployeeRole`→RoleMapping,
  `HierarchyEmployeeRoleMapping`→Tenant, →Role) **inkl. eines etwaigen Design-Time-Gatings** — das macht
  jetzt `ConfigureOnboardingModel()` (Laufzeit **und** Design-Time, einmalig).
- **Entfernen:** `builder.Entity<HierarchyEmployee>().HasKey(e => e.EmployeeId)` — `EmployeeBase.EmployeeId`
  trägt jetzt `[Key]`, mappt also per Konvention.
- **Bleiben:** Views (Design-Time-gated; zur Laufzeit liefert sie der SqlServer-WebPart), Passkey,
  `ConfigureBilling()` / `ConfigureBillingFeatureGrants()`, `ApplyConfigurationsFromAssembly(...)`.

> ⚠️ **Ohne diesen Change** konfiguriert nach dem Update **niemand** mehr die Onboarding-FKs zur Laufzeit
> (der WebPart macht es nicht mehr) → fehlende/falsche FKs. Der Change muss **zusammen** mit dem
> Paket-Update erfolgen.

### Neue Migration

`TenantInvitation.ParentTenantId` bekommt einen FK auf `Tenants` → eigene Migration generieren + anwenden:

```
dotnet ef migrations add OnboardingTenantInvitationFk --context ApplicationDbContext
```

(Die EmployeeRole/EmployeeRoleMapping-FKs sind delete-verhaltensgleich zu vorher — dafür entsteht i.d.R.
kein Schema-Diff; der neue `TenantInvitation`-FK schon.)

### `Role.PermissiveRoles`-Fehler — GELÖST in PRE078 (2026-06-17)

Der in PRE077 aufgetretene `Role.PermissiveRoles`-Modellfehler war **nicht** die Onboarding-Doppel-
Konfiguration, sondern eine instabile Navigations-Paarung der `Role`↔`RoleRole`-Selbstreferenz
(„über Kreuz", entgegen der EF-Namenskonvention; nur fluent gesetzt → bei großem Modell von einer
späten Konvention weggekippt). Fix toolkit-seitig per `[InverseProperty]` am Basis-`RoleRole`
(siehe `docs/BUG-PRE077-Role-PermissiveRoles.md`). **Schema unverändert → keine neue Migration nötig.**

→ **Auf PRE078 (oder neuer) aktualisieren.** Danach startet die App mit `ActivateFilters:true`
ohne den Fehler; eine konsumenten-seitige `Role`/`RoleRole`-Workaround-Konfiguration ist **nicht**
nötig (und half ohnehin nicht).

---

## 1. Was sich im Toolkit geändert hat

Neues Konzept **`EmployeeRoleMapping`** — eine pro-Mandant freundlich-benannte Hülle um eine
Security-`Role`, klassifiziert als:

- **`DirectRole`** — konkrete, einem Mitarbeiter zuweisbare Rolle.
- **`PermissionSet`** — Baustein-Bündel (seine Role trägt die feingranularen Rechte), das auf einer
  DirectRole **aktiviert/deaktiviert** wird.

Folgen im Datenmodell:

| Änderung | Detail |
|---|---|
| **Neue Entity** | `HierarchyEmployeeRoleMapping` (Tree) / `EmployeeRoleMapping` (Flat): `EmployeeRoleMappingId`, `TenantId`, `RoleId`(FK→Role), `Kind` (enum `DirectRole=0`/`PermissionSet=1`), `DisplayNameJson` (nullable, mehrsprachiges Label) |
| **FK-Umbau** | `HierarchyEmployeeRole.RoleId` **entfällt**, ersetzt durch `EmployeeRoleMappingId` (FK→EmployeeRoleMapping, **kein** Cascade) |
| **Komposition** | „PermissionSet auf DirectRole aktiviert" = ein `RoleRole`-Grant (DirectRole.Role erbt PermissionSet.Role) → der bestehende `SecurityModificationInterceptor` propagiert die Rechte. **Keine eigene Tabelle.** |
| **Cascade** | EmployeeRoleMapping→Tenant, →Role und EmployeeRole→Mapping sind **`Restrict`** (gegen SQL-Server-Multi-Cascade-Path) |
| **Interceptor** | neuer `HierarchyEmployeeRoleMaterializationInterceptor`: legt beim Zuweisen einer EmployeeRole die passende `UserRole` für den verknüpften User an (und entfernt sie wieder) |
| **Tenant-Template** | neue Extensions-Sektion `OnboardingEmployeeRoleMappings` (extract/apply des Mapping-Katalogs per Role-Name) |

`AcceptInvitationAsync` (Toolkit) und der Telerik-`MyTenants` lesen jetzt
`EmployeeRole → EmployeeRoleMapping → Role` (toolkit-seitig erledigt).

---

## 2. Pflicht-Code-Change: DbSet ergänzen

`IHierarchySecurityContextWithOnboarding` verlangt jetzt zusätzlich
`DbSet<HierarchyEmployeeRoleMapping> EmployeeRoleMappings`. In
`src/MLMManager.Infrastructure/Identity/ApplicationDbContext.cs` neben den übrigen Onboarding-DbSets:

```csharp
public DbSet<HierarchyEmployee> Employees { get; set; } = default!;

public DbSet<HierarchyEmployeeRole> EmployeeRoles { get; set; } = default!;

// NEU (PRE0xx): Klassifizierungs-/Label-Schicht über Security-Roles (DirectRole vs PermissionSet).
public DbSet<HierarchyEmployeeRoleMapping> EmployeeRoleMappings { get; set; } = default!;
```

`using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree.Models;` ist bereits vorhanden
(dort liegt auch `HierarchyEmployeeRoleMapping`).

> ⚠️ **Überholt durch §0 (2026-06-16):** Der Satz „keine OnModelCreating-Änderung nötig, FK-Cascade kommt
> über die Filter-Registrierung" gilt **nicht mehr**. Die FKs kommen ab der neuen PRE-Version aus
> `builder.ConfigureOnboardingModel()`, das der Konsument **selbst** im `OnModelCreating` aufrufen muss
> (siehe §0). Die Global-Filter kommen weiterhin über `ActivateGlobalCobFilters` / `ActivateFilters`.

---

## 3. Neue EF-Migration (⚠️ Datenverlust beachten)

```
dotnet ef migrations add AddEmployeeRoleMapping --context ApplicationDbContext
```

Das Gerüst enthält:
- `CreateTable("EmployeeRoleMappings")` (+ Indizes/FKs auf `Tenants` und `AspNetRoles`/Role-Tabelle, beide `Restrict`).
- An `EmployeeRoles`: **`DropColumn("RoleId")`** (+ alter FK) und **`AddColumn("EmployeeRoleMappingId")`** (+ neuer FK, `Restrict`).

> ⚠️ **`EmployeeRoles.RoleId` wird gedroppt.** Bestehende Mitarbeiter-Rollen-Zuordnungen verlieren dabei
> ihren Bezug. Je nach Datenstand:
>
> - **Keine/irrelevante EmployeeRole-Daten** (typisch, wenn das Feature noch nicht produktiv genutzt wurde):
>   Migration unverändert anwenden.
> - **Produktive EmployeeRole-Daten vorhanden:** die generierte Migration **vor dem Apply** um eine
>   Daten-Migration ergänzen — Reihenfolge:
>   1. `EmployeeRoleMappings` anlegen + je benötigter Rolle eine `DirectRole`-Zeile
>      (`INSERT … SELECT DISTINCT TenantId, RoleId, 0 /*DirectRole*/ FROM EmployeeRoles JOIN …`).
>   2. `EmployeeRoles.EmployeeRoleMappingId` aus dem alten `RoleId` über die neue Mapping-Tabelle setzen
>      (`UPDATE er SET EmployeeRoleMappingId = m.EmployeeRoleMappingId FROM EmployeeRoles er JOIN EmployeeRoleMappings m ON m.RoleId = er.RoleId AND m.TenantId = …`).
>   3. **Erst dann** `DropColumn("RoleId")`.
>
>   (D.h. die `AddColumn`/`UPDATE`/`DropColumn`-Reihenfolge im generierten `Up()` manuell umsortieren und das
>   `INSERT`/`UPDATE` als `migrationBuilder.Sql(...)` dazwischen einfügen.)

Migrations-Projekt: dasselbe wie bisher (`MLMManager.Infrastructure/Persistence/Migrations` bzw. das
`MigrationContext`-Projekt, je nach eurem EF-Setup).

---

## 4. DI / WebPart — nichts zu tun, nur verifizieren

Beides registriert sich **automatisch**, sobald die Onboarding-Filter aktiv sind
(`ActivationOptions.ActivateFilters == true`, bei euch bereits gesetzt). Die Onboarding-`WebPartInit`
wählt anhand `Strategy` die **Tree**-Varianten:

- `HierarchyEmployeeRoleMaterializationInterceptor` (über `[CustomConfigurator(DbContextOptionsBuilder)]`).
- `HierarchyEmployeeRoleMappingTemplatePartHandler` als `ITenantTemplatePartHandler` (scoped) → die
  Tenant-Template-Engine zieht ihn über `IEnumerable<ITenantTemplatePartHandler>`.

Falls ihr `ActivateFilters` **nicht** gesetzt habt: setzen (sonst materialisiert eine
EmployeeRole-Zuweisung keine `UserRole` und der Template-Teil läuft nicht).

---

## 5. Betrieb / Konsum

1. **Permission `ManageEmployees`** an die zuständigen (Tenant-)Admins vergeben — sie gated die neuen
   Admin-Editoren (`/Onboarding/BillingProfiles`, `/Onboarding/EmployeeRoleMappings`) und die zugehörigen
   Handler autoritativ.
2. **Rollen-Mappings anlegen** unter `/Onboarding/EmployeeRoleMappings`:
   - **PermissionSets** für die fachlichen Bausteine (z.B. „Stammdaten-Edit", „Stammdaten-View") — wrappen
     je eine vorhandene, rechtetragende Role.
   - **DirectRoles** für die zuweisbaren Rollen (z.B. „Sachbearbeiter") — neue Role aus Namen erzeugbar; auf
     ihnen die gewünschten PermissionSets aktivieren.
3. **Mitarbeitern Rollen zuweisen** im Employee-Editor (weist jetzt **DirectRole-Mappings** zu, nicht mehr
   rohe Roles). Der Interceptor legt die `UserRole` für den verknüpften User automatisch an.
4. Hatte das alte System bereits direkte `EmployeeRole→Role`-Zuordnungen, diese über die neuen DirectRole-
   Mappings nachbilden (vgl. Daten-Migration §3).

---

## 6. Tenant-Templates

`TenantTemplateMarkup` hat eine neue, neutrale `Extensions`-Map. Beim **Extrahieren** eines Templates aus
einem Mandanten landet der EmployeeRoleMapping-Katalog unter dem Key `OnboardingEmployeeRoleMappings`
(Role-Name + Kind + Label); beim **Anwenden** wird er per Role-Namen im Ziel-Mandanten neu angelegt. Die
PermissionSet-Komposition selbst steckt bereits in der bestehenden Rollen-/RoleGrants-Sektion des Templates.

- Wer Tenant-Templates **gespeichert** hat: nach dem Upgrade **neu aus einem Referenz-Mandanten
  extrahieren**, damit die neue Sektion enthalten ist. Alte Templates funktionieren weiter (nur ohne
  EmployeeRoleMappings).
- Greift nur im Single-Tenant-Apply-Pfad (`ApplyTemplate`), nicht im Bulk-`ApplyAllTenantsFor`.

---

## 7. Checkliste

- [ ] Toolkit-Pakete auf die PRE-Version mit Commit `41c61b4d` bumpen.
- [ ] `ApplicationDbContext`: `DbSet<HierarchyEmployeeRoleMapping> EmployeeRoleMappings` ergänzt (Compile grün).
- [ ] `ActivateFilters` aktiv (Interceptor + Part-Handler registriert).
- [ ] EF-Migration `AddEmployeeRoleMapping` generiert; bei produktiven EmployeeRole-Daten Daten-Migration eingefügt; appliziert.
- [ ] **(§0, neue PRE-Version) Pakete TenantSecurity + Onboarding gebumpt + konsumiert.**
- [ ] **(§0) `builder.ConfigureOnboardingModel();` im `OnModelCreating` ergänzt; manuelle Onboarding-FK-Blöcke + `HierarchyEmployee.HasKey` entfernt.**
- [ ] **(§0) EF-Migration `OnboardingTenantInvitationFk` generiert + appliziert.**
- [ ] **(§0) Start mit `ActivateFilters:true` geprüft (kein `No replacer for UserId`, kein `Role.PermissiveRoles`).**
- [ ] Permission `ManageEmployees` vergeben.
- [ ] PermissionSets + DirectRole-Mappings angelegt; Mitarbeiter-Zuweisungen geprüft (UserRole wird materialisiert).
- [ ] (Optional) Tenant-Templates neu extrahiert.
