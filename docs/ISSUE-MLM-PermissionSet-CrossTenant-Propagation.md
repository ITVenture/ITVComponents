# ISSUE: PermissionSet-Aktivierung propagiert nicht über Tenant-Grenzen (Downline-Zugriff)

**Gemeldet von:** MLM-Session, 2026-07-07 · **Toolkit-Stand:** 5.0.0-PRE113
**Betrifft:** `EntityFramework.TenantSecurity(.SqlServer)` — Tenant-Tree/Role-Vererbungs-Auflösung
(`GetDownwardsRoleTreeProc`, `TenantAccessTreeUp`/`TenantAccessTreeDown`, `GetUpwardsRoleTree*`) **und** die
Lazy-Tree-Primitive `DbSecurityRepository.GetRootTenants`/`GetChildTenants` (der Konsistenz-Zwilling).

## Kurzfassung

Ein `EmployeeRoleMapping` vom Kind **`PermissionSet` (1)** wird laut Design **nicht direkt** zugewiesen, sondern via
`RoleRoles`-Kante auf eine **`DirectRole`** „aktiviert" (der DirectRole-Role erbt über den Set-Role). Diese Aktivierung
ist eine **intra-tenant** `RoleRoles`-Kante (Permissive und Permitted im selben Tenant).

Die **cross-tenant** Rollen-/Tenant-Vererbung (Downwards/Upwards-Role-Tree) folgt aber ausschließlich `RoleRoles`-Kanten,
die **genau eine Tenant-Ebene nach oben** springen (`pr.TenantId = r_2.nextparent`), und verankert die Kette auf
**direkt zugewiesenen** Rollen (`TenantUserRoles`). Eine intra-tenant PermissionSet-Aktivierung wird dabei **nie**
mitkomponiert. Trägt der Set-Role selbst eine cross-tenant Kante (typisch: „Downline-Zugriff" → Rolle im Kind-Tenant),
erreicht der Mitarbeiter das Kind-Tenant **nicht**, obwohl er den Set-Role im aktuellen Tenant effektiv hält.

Folge im Konsumenten (MLM): Der TenantSwitcher blendet sich für den Mitarbeiter aus („kein zugreifbarer Tenant"),
weil `GetChildTenants` (korrekt zur Auth konsistent) kein Kind-Tenant liefert.

## Repro-Daten (MLM-Dev-DB, minimal)

Tenant-Hierarchie: **ADM(1)** → **Horst Müller(4)** → **Nadja Wyler(5)**

Relevante SecurityRoles:

| RoleId | Name              | Tenant       |
|--------|-------------------|--------------|
| 29     | Employees         | Horst (4)    |
| 24     | Access_DownLine   | Horst (4)    |
| 27     | Upline            | Nadja (5)    |

`EmployeeRoleMappings` an Horst(4): `Employees(29)` = **DirectRole(0)**, `Access_DownLine(24)` = **PermissionSet(1)**.

Relevante `RoleRoles` (Semantik lt. Verwendung: *Permissive wird erreicht, wenn Permitted gehalten wird*):

| RoleRoleId | Permissive        | Permitted        | Typ           |
|------------|-------------------|------------------|---------------|
| 25         | Access_DownLine@4 | Employees@4      | **intra-tenant** (PermissionSet-Aktivierung: Employees erbt Access_DownLine) |
| 17         | Upline@5          | Access_DownLine@4| cross-tenant (Downline-Propagation ADM-Downline-Muster) |

`Upline@5(27)` → global `TenantOwner` (hat Permissions inkl. `SwitchTenant`) ⇒ Nadja *wäre* „accessible", wenn erreicht.

User **kurt.peters** (TenantUserId 6): direkte Zuweisung nur `Employees@4(29)` (DirectRole via `EmployeeRoles`).
Erwartung: Employees → (PermissionSet-Aktivierung, edge25) → Access_DownLine → (edge17) → Upline@Nadja ⇒ Zugriff auf Nadja.

## Erwartet vs. Ist

- **Erwartet:** kurt erreicht Nadja(5) mit Rolle Upline@5(27) (Downline-Zugriff via aktiviertem PermissionSet).
- **Ist:** kurt erreicht **nur** Horst(4); Nadja fehlt.

### Belege (auf der Dev-DB reproduziert)

`EXEC GetDownwardsRoleTreeProc @pUserId='<kurt>', @puserIsLabels=0, @pViewPoint='<Horst>'`
→ **nur** ein Row: ChildTenant=Horst(4), ResultingChildRole=Employees(29), ChildLevel=1. **Kein** Row für Nadja.

Gegenprobe mw (Systemadministrator@ADM, direkt zugewiesen): erreicht Nadja(5) via Systemadministrator@5(28),
ChildLevel 3 — funktioniert, weil die Kette aus **direkt zugewiesenen** Rollen + reinen cross-tenant Kanten (edges 9,18) besteht.

Transaktionaler Test (Rollback): weist man kurt `Access_DownLine@4(24)` **direkt** zu (`INSERT TenantUserRoles(6,24)`),
liefert dieselbe Proc **Nadja(5) mit Upline@5(27), ChildLevel 2**. ⇒ Die Downline-Propagation funktioniert grundsätzlich —
nur eben **nicht** über eine PermissionSet-Aktivierung (intra-tenant Kante), sondern nur über direkt gehaltene Rollen.

## Ursache (Code)

`SqlColumnsSyntaxHelper.cs` — die rekursiven Rollen-CTEs verlangen, dass jede `RoleRoles`-Kante zum **strukturellen
Eltern-Tenant** führt und keine intra-tenant Kante ist:

- `TenantAccessTreeUp` (~Z.263-266): `inner join RoleRoles roro on r_2.nextChildRole = roro.PermissiveRoleId inner join SecurityRoles pr on pr.RoleId = roro.PermittedRoleId and pr.TenantId = r_2.nextparent`
- `TenantAccessTreeDown` (~Z.249): `inner join RoleRoles tcr on tcr.PermittedRoleId = r_2.NextParentRoleId and tcr.PermissiveRoleId = ts.RoleId` (mit `tc.ParentTenantId = r_2.ChildTenantId`)
- `GetDownwardsRoleTreeProc` interne `r`-CTE (~Z.380-383): dito `pr.TenantId = r_2.nextparent`; Endauswahl verankert auf `TenantUserRoles tur ... tur.RoleId = pr.RoleId` (nur **direkt** zugewiesene Top-Rolle).

`pr.TenantId = nextparent` ⇒ Permitted-Rolle liegt zwingend im Eltern-Tenant ⇒ eine intra-tenant Kante (Permissive &
Permitted im selben Tenant, wie edge25) kann strukturell nie traversiert werden. Damit fließt die PermissionSet-
Aktivierung (die genau so eine intra-tenant Kante ist) nicht in die cross-tenant Propagation ein.

Der Lazy-Tree-Zwilling `DbSecurityRepository` verhält sich konsistent: die Seed-Rollen (`GetRootTenants`) sind nur die
direkten `TenantUserRoles`, und `ChildLevel` matcht Kind-Rollen nur über **eine** `RoleRoles`-Kante von den *carried*
(direkten) Rollen — keine intra-tenant Closure der carried-Menge. (Das ist also kein Primitive-Bug, sondern spiegelt
die Auth korrekt — die Lücke sitzt in der gemeinsamen Vererbungs-Semantik.)

## Kern der Inkonsistenz

Die PermissionSet-Aktivierung gewährt der DirectRole laut Doku (`EmployeeRoleMappingKind.PermissionSet`) die
**Permissions** des Set-Roles **innerhalb** des Tenants (in-tenant RoleRoles-Closure). Trägt der Set-Role zusätzlich
eine **cross-tenant** Kante, wird deren Reichweite **nicht** mitvererbt — der Mitarbeiter „hat" den Set-Role lokal,
aber nicht für die Tenant-Baum-Propagation. Für Downline-Zugriffs-Rollen (deren einziger Zweck die cross-tenant
Reichweite ist) macht das die PermissionSet-Aktivierung wirkungslos.

## Lösungsrichtung (Vorschlag)

Die Rollen-Vererbungs-Auflösung sollte pro Tenant die **effektiven** Rollen des Users bilden (inkl. intra-tenant
`RoleRoles`-Closure der direkt gehaltenen Rollen) und diese Menge propagieren — statt nur direkt zugewiesene Rollen als
Ketten-Anker zuzulassen. Konkret: die rekursiven CTEs müssten neben der cross-tenant Kante (`pr.TenantId = nextparent`)
auch intra-tenant Kanten (`pr.TenantId = <selber Tenant>`) als „Rolle-erbt-Rolle im selben Tenant"-Schritt zulassen
(ohne dabei die Tenant-Ebene zu wechseln), bzw. der `TenantUserRoles`-Anker auf die effektive Rollenmenge erweitert
werden. Zu klären: Perf (die Rekursion wächst) und ob die Semantik gewünscht ist (siehe unten).

## Offene Design-Frage / Workaround

Falls die aktuelle Semantik **beabsichtigt** ist (cross-tenant Reichweite nur über direkt gehaltene Rollen), ist das
kein Bug, sondern eine Doku-/Modellierungsfrage: Downline-Zugriff müsste dann als **DirectRole** (nicht PermissionSet)
modelliert und dem Mitarbeiter direkt zugewiesen werden. Verifizierter Workaround im MLM: kurt `Access_DownLine@4(24)`
direkt zuweisen (bzw. das „Zugriff auf Untergeordnete Mandanten"-Mapping als DirectRole führen und zuweisen) ⇒ Nadja
wird erreichbar. Bitte in der Toolkit-Session entscheiden, welche der beiden Semantiken die intendierte ist.

## Umsetzung (Toolkit, 2026-07-07)

Entschieden: PermissionSet-Aktivierung soll cross-tenant propagieren. Umgesetzt wurde der **Anker-auf-effektive-
Rollenmenge**-Ansatz („diskrete Weitergabe") — der cross-tenant Klettermechanismus bleibt unangetastet, erweitert wird
nur die Menge der „gehaltenen" Rollen um die **intra-tenant `RoleRoles`-Closure** (Permissive erreichbar wenn Permitted
gehalten, nur gleicher Tenant). Beide Vererbungs-Implementierungen wurden konsistent angepasst:

- **LINQ-Zwilling** (`DbSecurityRepository`, Switcher-Laufzeitpfad): neue Helper `ExpandIntraTenantClosure(ctx, roleIds,
  tenantId)`; `ChildLevel` expandiert die `carried`-Rollen vor dem Kanten-Match. Deckt Roots/Children/Pass-Through in
  einem Punkt ab. **Keine Migration nötig** (Live-LINQ).
- **SQL-Prozeduren/-Funktionen** (`SqlColumnsSyntaxHelper`, CoreIdentityTree; via `GetRawUserQuery`→`GetDownwardsRole
  TreeProc` auch Permission-Auflösung): neue Inline-TVF `GetEffectiveTenantUserRoles()` (rekursive intra-tenant Closure
  über `TenantUserRoles`); die 6 Rollen-Tree-Anker (`… tur.RoleId = pr.RoleId`) joinen jetzt diese TVF statt
  `TenantUserRoles` direkt. **Erfordert eine neue Konsumenten-Migration**, die `ConfigureViews` erneut ausführt
  (DROP-if-exists + CREATE, idempotent), damit die TVF + neu-generierten Prozeduren deployt werden.

Monotonie/Safety: effektive Menge ⊇ direkte Rollen ⇒ kein bestehender Zugriff wird entzogen, nur die intendierte
intra-tenant Vererbung ergänzt. Terminierung: zyklische Rollen-Vererbung ist verhindert (zusätzlich Guard/`distinct`).

**Offen:** Dev-DB-Validierung der neu generierten SQL-Objekte (im Toolkit-Env nicht ausführbar), Perf-Beobachtung der
Closure auf großen `TenantUserRoles`, sowie ob `TenantAccessTreeUp/Down` (separater Feature-Pfad, hier bewusst
unangetastet) dieselbe Weitergabe braucht.
