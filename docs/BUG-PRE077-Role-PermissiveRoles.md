# BUG (PRE077): `Role.PermissiveRoles` — "Unable to determine the relationship" beim App-Start

> ## ✅ GELÖST (Toolkit-Seite, 2026-06-17) — Fix in der nächsten Version (PRE078)
>
> **Root Cause:** Die self-referenzierende M:N-Beziehung `Role` ↔ `RoleRole` ist „über Kreuz"
> verdrahtet (`Role.PermissiveRoles` ↔ `RoleRole.PermittedRole`, `Role.PermittedRoles` ↔
> `RoleRole.PermissiveRole`) — also **entgegen** der EF-Namensgleichheits-Konvention. Bisher wurde
> die Paarung nur **fluent** in `OnModelCreating` gesetzt (`HasMany(...).WithOne(...)`). Diese
> Fluent-Paarung ist gegenüber einer späten, konventionsgetriebenen Re-Discovery von `Role`
> **nicht stabil**: Sobald genügend zusätzliche Entitäten/Registrierungen im Modell sind (im
> MLM-Vollstart durch den **Onboarding-WebPart** ausgelöst — reproduzierbar bereits durch dessen
> bloße Aktivierung, unabhängig von Filtern/Interceptor), lässt EF eine der beiden `RoleRole`-
> Collections auf `Role` **ungemappt** → Modell-Validierungsfehler. Reines Design-Time (Migrations,
> `useFilters=false`, kleineres Modell) traf den Kipppunkt nie → dort lief es immer.
>
> Das erklärt **alle** Beobachtungen unten: Publish == Source, keine Typ-Duplikate, und warum eine
> konsumenten-seitige Re-Konfiguration **nach** `base.OnModelCreating` nicht half (die Konvention
> kippt das Mapping bei der Finalisierung, also nach dem Consumer-Code).
>
> **Fix:** Die Navigations-Paarung wird jetzt **deterministisch per `[InverseProperty]`-Annotation**
> am Basis-`RoleRole` festgenagelt (`Shared/Models/Base/RoleRole.cs`):
> `PermittedRole` → `[InverseProperty("PermissiveRoles")]`, `PermissiveRole` →
> `[InverseProperty("PermittedRoles")]`. Annotationen werden früh und stabil von der
> `InversePropertyAttributeConvention` aufgelöst und können nicht mehr „weggekippt" werden. Greift
> für **alle** Strategien (Flat/Tree/Basic) über die gemeinsame Basisklasse.
>
> **Schema unverändert → keine neue Migration nötig.** Der Fix pinnt exakt dieselbe Paarung +
> dieselben FK-Spalten (`PermissiveRoleId`/`PermittedRoleId`) + dasselbe Delete-Verhalten
> (`ClientSetNull`) wie der bestehende `ApplicationDbContextModelSnapshot`. MLM muss nach dem Update
> **nur** auf die Fix-Version aktualisieren (PRE078); `dotnet ef migrations add` erzeugt eine
> leere Migration.
>
> **Verifiziert** durch einen Voll-Bootstrap-Repro (echter `WebPartManager` + MLM-`appsettings-parts.json`
> + echter `ApplicationDbContext`): vor dem Fix reproduziert (identischer Stacktrace), nach dem Fix
> `MODEL OK` mit unveränderter `RoleRole`-FK-Abbildung.

---


> **Gemeldet aus der MLM-Konsumenten-Session, 2026-06-17.** Dies ist der vom
> Migrations-Leitfaden `Migration-Future_10-MLM-EmployeeRoleMapping.md` **§0** explizit
> vorgesehene Rückmeldungs-Fall ("Falls der `Role.PermissiveRoles`-Fehler nach dem Update
> bleibt … vollständigen Exception-Text sichern und an die Toolkit-Seite zurückmelden").

## Kurzfassung

Mit dem **publizierten Paket `5.0.0-PRE077`** wirft der Start einer konsumierenden Blazor-App
(`ApplicationDbContext : AspNetTreeSecurityContext<…>`) bei der **EF-Model-Validierung** eine
`InvalidOperationException` zu `Role.PermissiveRoles`. Der Konsument kann es **nicht** selbst
beheben — die Ursache liegt unterhalb der Konsumenten-Ebene (siehe „Was eliminiert wurde").

## Umgebung

- Toolkit-Pakete: **`5.0.0-PRE077`** (alle, via `PackageReference`).
- .NET 10 / EF Core 10 (`Microsoft.EntityFrameworkCore*` 10.0.8), SQL Server LocalDB.
- Context: `MLMManager.Infrastructure.Identity.ApplicationDbContext` :
  `AspNetTreeSecurityContext<ApplicationDbContext>`, `IHierarchySecurityContextWithOnboarding`,
  `IBillingContext`. Strategy **Tree**, `ActivateFilters: true`, `UseEntityTracker: true`,
  `UseLazyLoadingProxies` (vom Toolkit gesetzt).
- Trat **nach dem Update PRE076 → PRE077** auf. Die PRE077-Leitfaden-§0-Pflichtänderungen
  (`builder.ConfigureOnboardingModel()`, Entfernen der manuellen EmployeeRoleMapping-FK-Blöcke
  + `HierarchyEmployee.HasKey`, Migration `OnboardingTenantInvitationFk`) sind **korrekt
  umgesetzt und Build grün** — der Fehler ist davon unabhängig (siehe „Was eliminiert wurde").

## Exception (vollständig)

```
Unhandled exception. System.InvalidOperationException: Unable to determine the relationship
represented by navigation 'Role.PermissiveRoles' of type 'ICollection<RoleRole>'. Either
manually configure the relationship, or ignore this property using the '[NotMapped]' attribute
or by using 'EntityTypeBuilder.Ignore' in 'OnModelCreating'.
   at Microsoft.EntityFrameworkCore.Infrastructure.ModelValidator.ValidatePropertyMapping(IConventionTypeBase structuralType, IConventionModel model, IDiagnosticsLogger`1 logger)
   at Microsoft.EntityFrameworkCore.Infrastructure.ModelValidator.ValidatePropertyMapping(IModel model, IDiagnosticsLogger`1 logger)
   at Microsoft.EntityFrameworkCore.Infrastructure.ModelValidator.Validate(IModel model, IDiagnosticsLogger`1 logger)
   at Microsoft.EntityFrameworkCore.Infrastructure.RelationalModelValidator.Validate(IModel model, IDiagnosticsLogger`1 logger)
   at Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal.SqlServerModelValidator.Validate(IModel model, IDiagnosticsLogger`1 logger)
   at Microsoft.EntityFrameworkCore.Infrastructure.ModelRuntimeInitializer.Initialize(IModel model, Boolean designTime, IDiagnosticsLogger`1 validationLogger)
   at Microsoft.EntityFrameworkCore.Infrastructure.ModelSource.CreateModel(...)
   at Microsoft.EntityFrameworkCore.Infrastructure.ModelSource.GetModel(...)
   at Microsoft.EntityFrameworkCore.Internal.DbContextServices.CreateModel(Boolean designTime)
   at Microsoft.EntityFrameworkCore.Internal.DbContextServices.get_Model()
   … (DI-Resolution von ApplicationDbContext beim App-Start) …
   at Microsoft.EntityFrameworkCore.DbContext.Microsoft.EntityFrameworkCore.Infrastructure.IInfrastructure<System.IServiceProvider>.get_Instance()
```

Tritt während der **DI-Auflösung von `ApplicationDbContext` beim App-Start** auf (Model wird erstmals
gebaut → `ModelValidator.ValidatePropertyMapping`). Es ist **kein** Design-Time-/Migrations-Fehler
(Migrationen scaffolden sauber über die 2-Arg-Factory, die `useFilters=false` lässt).

## Betroffenes Modell

- `…CoreIdentityTree.Model.Role` (: `…Shared.Models.Base.Role<…>`):
  - `Base/Role.cs:43` `public virtual ICollection<TRoleRole> PermissiveRoles`
  - `Base/Role.cs:45` `public virtual ICollection<TRoleRole> PermittedRoles`
- `…CoreIdentityTree.Model.RoleRole` (: `…Shared.Models.Base.RoleRole<…>`):
  - `Base/RoleRole.cs:24/26` FKs `PermissiveRoleId` / `PermittedRoleId` (beide `int?`)
  - `Base/RoleRole.cs:28-32` Navs `[ForeignKey(PermittedRoleId)] PermittedRole`,
    `[ForeignKey(PermissiveRoleId)] PermissiveRole`

→ Self-referencing M:N (Role↔Role über RoleRole) mit **zwei** Nav-Paaren; EF kann es nicht per
Konvention auflösen und braucht explizite Konfiguration.

## Wo die Config im **Source** steht (und im publizierten Paket offenbar nicht zieht)

`CoreIdentityTree/AspNetTreeSecurityContext`1.cs`, `OnModelCreating` (Z. 1105–1133):

```csharp
modelBuilder.Entity<Role>().HasMany(n => n.PermittedRoles).WithOne(pr => pr.PermissiveRole)   // 1115
    .OnDelete(DeleteBehavior.ClientSetNull);
modelBuilder.Entity<Role>().HasMany(n => n.PermissiveRoles).WithOne(pr => pr.PermittedRole)    // 1117
    .OnDelete(DeleteBehavior.ClientSetNull);
```

Analog auch in `CoreIdentity/AspNetSecurityContext`1.cs:679-681` und `Basic/SecurityContext`1.cs:447-448`.
Diese Config ist im **aktuellen Source vorhanden** — der publizierte PRE077-Build verhält sich aber so,
als ob sie **fehlt** bzw. nicht greift.

## Was eliminiert wurde (Konsumenten-seitig getestet)

1. **Nicht die Onboarding-Doppel-Konfig.** Die §0-Pflichtänderungen sind sauber umgesetzt
   (`ConfigureOnboardingModel()` aufgerufen, manuelle FK-Blöcke + `HasKey` entfernt). Der Fehler
   bleibt mit der **guide-kanonischen** `OnModelCreating` (= ohne jeden manuellen Role-Workaround).
   Das ist der **sauberste Repro**.
2. **Konsumenten-seitige 1:1-Replikation hilft nicht.** Ein expliziter Nachbau der Config aus
   Z. 1115-1118 (zusätzlich mit `.HasForeignKey(pr => pr.PermissiveRoleId)` /
   `.HasForeignKey(pr => pr.PermittedRoleId)`, `ClientSetNull`) **im konsumierenden
   `OnModelCreating` nach `base.OnModelCreating(...)`** beseitigt den Fehler **nicht**. → Die
   Navigation bleibt unkonfiguriert, obwohl sowohl Basis als auch Konsument sie konfigurieren.
   Das deutet darauf hin, dass das Problem **unterhalb** der Konsumenten-OnModelCreating-Ebene liegt
   (im publizierten Paket selbst).
3. **DLL ist aktuell.** Der Build vor dem Start enthielt die Änderungen (verifiziert via
   `IEntityTypeConfiguration`-Scan-Warnung + DLL-Timestamp); kein Stale-Build.

## Verdacht / zu prüfen auf Toolkit-Seite

- **Publish-vs-Source-Divergenz.** Toolkit-HEAD ist `c5a98827`
  („Konsolidierung Phase D #18 (WIP): TenantSecurity 10->3 Merge baut gruen"), und der
  `<Version>`-Bump auf PRE077 ist im Working-Tree **unkommittet**. Wahrscheinlich wurde PRE077 aus
  einem Stand **vor** dem Hinzufügen/Wirksamwerden der Z. 1115-1118-Config publiziert, oder der
  Konsolidierungs-Merge hat die RoleRole-Konfiguration regressed.
  → **Bitte das publizierte `5.0.0-PRE077`-Assembly gegen den Source prüfen** (enthält die
  `OnModelCreating`-Methode von `AspNetTreeSecurityContext` im Paket die `PermissiveRoles`-Zeile?).
- Falls die Config im Paket **vorhanden** ist und trotzdem nicht greift: prüfen, ob der
  Konsolidierungs-Merge (`TenantSecurity 10→3`) die Vererbungskette so verändert hat, dass das
  konkrete `Role`/`RoleRole` im Modell ein anderer Typ ist als der, auf den
  `modelBuilder.Entity<Role>()` in der gemergten Klasse zeigt (Tree vs Basic vs CoreIdentity).
- EF-10-Verhaltensänderung bei self-ref M:N ohne explizites `HasForeignKey` ist **unwahrscheinlich**
  (Konsumenten-Replikation **mit** explizitem FK half ebenfalls nicht), aber zur Sicherheit beim
  Fix gleich `.HasForeignKey(...)` auf beiden Seiten mitkonfigurieren.

## Gewünschtes Ergebnis

Publiziertes Paket, mit dem `AspNetTreeSecurityContext`-basierte Konsumenten ohne manuelle
Role/RoleRole-Konfiguration starten (Model-Validierung grün). Danach kann §0-Checklistenpunkt
„Start mit `ActivateFilters:true` geprüft (kein `Role.PermissiveRoles`)" abgehakt werden.

## Konsumenten-Status (MLM), zur Einordnung

- PRE077 + Leitfaden-§0 vollständig umgesetzt, Build grün, Migration `OnboardingTenantInvitationFk`
  appliziert. `ApplicationDbContext.OnModelCreating` steht auf der guide-kanonischen Form.
- App-Start scheitert ausschließlich an obigem `Role.PermissiveRoles`-Fehler.
