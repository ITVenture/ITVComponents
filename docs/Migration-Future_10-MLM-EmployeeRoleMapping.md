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
(dort liegt auch `HierarchyEmployeeRoleMapping`). **Keine** OnModelCreating-Änderung nötig — die Entity
ist convention-mapped, und FK-Cascade (`Restrict`) + Global-Filter kommen über die bereits aktive
Onboarding-Filter-Registrierung (`ActivateGlobalCobFilters`, via WebPart `ActivateFilters`).

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
- [ ] Permission `ManageEmployees` vergeben.
- [ ] PermissionSets + DirectRole-Mappings angelegt; Mitarbeiter-Zuweisungen geprüft (UserRole wird materialisiert).
- [ ] (Optional) Tenant-Templates neu extrahiert.
