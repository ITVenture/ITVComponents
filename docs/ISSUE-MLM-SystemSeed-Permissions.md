# Issue: System-Seed um fehlende „Kern"-Permissions erweitern (Anstoß aus MLM)

**Status:** GELÖST (Toolkit-Session 2026-06-25) — statt statischer Seed-Liste eine
config-gegatete Auto-Registrierung. Siehe Abschnitt „Umsetzung" unten. Build grün, Host-Test offen.
**Datum:** 2026-06-23
**Quelle:** MLMManager-Session (Konsument). MLM hat seine DB „plattgemacht" und den ADM-Seed
aus Migrationen + einem Startup-Seed (`Program.cs`) reproduziert.

## Beobachtung

Auf einer **frisch angelegten** MLM-DB registriert das Toolkit beim Start **47** globale
Permissions (`Permissions` mit `TenantId IS NULL`). Die über die Zeit gewachsene alte MLM-DB
hatte **64**. Es fehlen also **17** Permissions, die das aktuelle Toolkit auf einer leeren DB
**nicht** automatisch registriert.

Davon referenziert der MLM-ADM-Seed (Navigation-Gating + Grants für die nicht-Admin GlobalRoles
`TenantOwner`/`DefaultUser`) **7 Stück**, die dadurch fehlten → Navigation-Einträge ohne
Permission, Grants für GlobalRole 2/3 leer.

## A) Die 7, die MLM aktuell selbst seedet (Workaround)

Diese werden in `MLMManager.Web/Program.cs` in einem **idempotenten** Startup-Block
(`INSERT INTO Permissions … WHERE NOT EXISTS …`, per Name, `TenantId = NULL`) **vor** den
GlobalRolePermission-Grants angelegt:

| Permission | wofür im MLM-Seed gebraucht |
|---|---|
| `Navigate` | Grant an GlobalRole `TenantOwner`(2) **und** `DefaultUser`(3) |
| `SwitchTenant` | Grant an `TenantOwner`(2) und `DefaultUser`(3); TenantSwitcher |
| `Invitations.CreateSub` | Grant an `TenantOwner`(2) |
| `Invitations.CreateEmp` | Grant an `TenantOwner`(2) |
| `ManageEmployees` | Grant an `TenantOwner`(2) |
| `GlobalRoles.Write` | Navigation-Eintrag „Global Roles" → `/Security/GlobalRoles` |
| `TrustedComponents.Write` | Navigation-Eintrag „Trusted Components" → `/Security/TrustedComponents` |

Besonders auffällig: `Navigate` und `SwitchTenant` wirken wie **Kern-Toolkit-Permissions** —
dass die auf einer frischen DB nicht auto-registriert werden, ist vermutlich der eigentliche Bug
(Permission-Scan unvollständig? nur bei Besuch bestimmter Pages? WebPart-abhängig?). Ein
`GET /` triggert keine Nachregistrierung (bleibt bei 47).

## B) Weitere 10, in der alten DB vorhanden, vom MLM-Seed NICHT referenziert

Diese braucht der MLM-ADM-Seed aktuell nicht (daher dort nicht geseedet), sie waren aber in der
alten DB global vorhanden. Toolkit-Seite entscheidet, ob sie in den System-Seed gehören:

- `Onboarding.Admin.BillingProfile.Write`
- `Onboarding.Admin.Employees.Write`
- `Onboarding.Admin.RoleMappings.DirectRole`
- `Onboarding.Admin.RoleMappings.PermissionSet`
- `Onboarding.Admin.RoleMappings.Write`
- `Onboarding.Admin.SubTenants.Write`
- `Roles.AssignRole`
- `Sequences.Write`
- `TenantTemplates.Write`
- `TrustedComponents.View`

Die `Onboarding.Admin.*`-Gruppe stammt aus den Onboarding-Flows — dass die auf einer frischen DB
nicht registriert sind, ist im Hinblick auf die Onboarding-WebParts ebenfalls verdächtig.

## Entscheidung (Toolkit-Session)

Festlegen, **welche dieser 17 in den Toolkit-System-Seed** (= automatische Registrierung /
Toolkit-eigener Seed) gehören. Kandidaten-Kategorien:

1. **Klar Toolkit-Kern** (sollten sowieso immer registriert sein): `Navigate`, `SwitchTenant`,
   ggf. `Roles.AssignRole`, `Sequences.Write`, `TenantTemplates.Write`, `TrustedComponents.View/Write`,
   `GlobalRoles.Write`.
2. **Feature-gebunden (Onboarding)**: `Invitations.Create*`, `ManageEmployees`,
   `Onboarding.Admin.*` — gehören evtl. in den Onboarding-WebPart-Seed statt in den globalen Kern.

## Umsetzung (Toolkit-Session 2026-06-25)

Statt zu entscheiden, *welche* der 17 Permissions in eine statische Seed-Liste gehören, wurde der
Mechanismus generalisiert: Das Toolkit **materialisiert jede genuin angeforderte Permission beim
ersten echten Authorization-Gate** selbst (global, `TenantId == null`) und grantet sie optional an
eine konfigurierte GlobalRole. Eine frische DB füllt ihren Permission-Katalog damit von selbst,
sobald ein Admin durch die App navigiert — kein Seed-Abgleich Toolkit↔Konsument mehr nötig.

Bausteine:
- `AutoPermissionRegistration` (Core, statisch): `Enabled` + `GrantToGlobalRole` + Claim-basiertes
  Dedup (`TryClaim`/`ReleaseClaim`), je Name max. ein DB-Versuch pro Prozess → Hot-Path bleibt billig.
- `ISecurityRepository.EnsureRequestedPermissions(string[])` — Default-No-op; Impl in **Flat- und
  Tree-**`DbSecurityRepository` (per-Operation `LeaseContext()`, kollisionsfrei zum circuit-scoped
  Context; EntityWriteTracker hebt das Change-Signal → Grants wirken live). `SecurityRepository`
  (Wrapper) leitet weiter.
- Aufruf in `ServiceProviderExtensions.HasAnyPermission`-Pfad: nur wenn `Enabled`, authentifiziert
  und **nicht** `checkOnlyForKnownPermissions` (Known-only-Proben dürfen keine Permissions anlegen).
- Aktivierung über WebPart: `ActivationOptions.AutoRegisterRequestedPermissions` (bool) +
  `AutoRegisterPermissionsGrantRole` (string) → in `WebPartInit.RegisterServices` auf die Statics
  gespiegelt.

Damit ist die „welche 17"-Frage gegenstandslos: Wird eine Permission tatsächlich von einem Gate
verlangt, entsteht sie automatisch.

**Empfohlene MLM-Config:** `AutoRegisterRequestedPermissions=true`, `AutoRegisterPermissionsGrantRole`
= **Name der Admin-GlobalRole**. Begründung: Die Admin-GlobalRole ist *kein* Bypass — sie ist
permission-getrieben und hat nur die geseedeten Rechte. Der Auto-Grant an die Admin-Rolle ist also
genau der Sinn: Sie sammelt jede von irgendeinem Modul angeforderte Permission automatisch ein. Wer
die globale Admin-Rolle erhält (z.B. der TenantOwner des Admin-Tenants), erbt damit lückenlos alle
Rechte. Ein zusätzlicher Grant an `TenantOwner`/`DefaultUser` ist **nicht** nötig (diese GlobalRoles
bekommen ihre Rechte weiterhin über den expliziten ADM-Seed bzw. Tenant-Rollen).

## Konsequenz für MLM (Sync-Pflicht)

Was das Toolkit in seinen System-Seed übernimmt, **muss MLM aus dem `Program.cs`-„ensure"-Block
entfernen**, sonst Doppel-Pflege/Divergenz. Der MLM-Block ist idempotent (`WHERE NOT EXISTS`),
d.h. kurzfristig entsteht **kein** Konflikt/Duplikat (die computed `PermissionNameUniqueness`
würde ein Duplikat ohnehin verhindern) — aber beim **nächsten Plattmachen** müssen wir die Liste
abgleichen, damit die Permission-Verantwortung eindeutig auf einer Seite liegt.

**Verweis MLM-Seite:** `MLMManager.Web/Program.cs` (Startup-Seed-Scope direkt nach
`builder.Build()`): Block „Ensure permissions … NOT auto-registered on a fresh database".
Die restliche ADM-Referenzdaten (Tenant ADM, GlobalRoles 2/3, ADM-SecurityRoles, RoleRole,
GlobalToLocalRole-Closure, EmployeeRoleMappings, WebPlugins SysConfig, IdentityMail-GlobalSetting,
AssemblyDiagnostics-TenantSetting, TenantTemplates, TenantSwitcher-TrustedComponent, Navigation +
TenantNavigation) liegen in den MLM-Migrationen `SeedAdminTenantAndUser` /
`SeedTrustedComponents` / `AddEmployeeRoleMapping` bzw. im selben Startup-Block (Navigation +
Grants, weil PermissionId-abhängig).
