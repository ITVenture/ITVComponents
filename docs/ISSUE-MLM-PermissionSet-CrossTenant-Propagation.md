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
  TreeProc` auch Permission-Auflösung): neue **parametrisierte** Inline-TVF `GetEffectiveTenantUserRoles(@tenantUserId)`
  (rekursive intra-tenant Closure der Rollen **genau eines** Tenant-Users); die 6 Rollen-Tree-Anker joinen sie nicht als
  DB-weiten Join, sondern per **`CROSS APPLY … (tu.TenantUserId) … where er.RoleId = pr.RoleId`** auf den bereits im
  Scope stehenden Tenant-User — semantisch identisch zum alten Inner-Join, aber die Rekursion berührt nur die Zeilen
  des aktuellen Users (Kosten skalieren mit dem Query-Scope, nicht mit der Gesamt-`TenantUserRoles`; behebt die unten
  gemessene Skalierungs-Regression). **Erfordert eine neue Konsumenten-Migration**, die `ConfigureViews` erneut
  ausführt (DROP-if-exists + CREATE, idempotent), damit die TVF + neu-generierten Prozeduren deployt werden.

Monotonie/Safety: effektive Menge ⊇ direkte Rollen ⇒ kein bestehender Zugriff wird entzogen, nur die intendierte
intra-tenant Vererbung ergänzt. Terminierung: zyklische Rollen-Vererbung ist verhindert (zusätzlich Guard/`distinct`).

**Offen:** Dev-DB-Validierung der neu generierten SQL-Objekte (im Toolkit-Env nicht ausführbar) inkl. Perf-Nachmessung
der jetzt parametrisierten TVF (Erwartung: Overhead skaliert mit dem User-Scope statt der Gesamt-DB), sowie ob
`TenantAccessTreeUp/Down` (separater Feature-Pfad, hier bewusst unangetastet) dieselbe Weitergabe braucht.

## Perf-Nachmessung PRE113 vs PRE114 (MLM-Session, 2026-07-07) — SKALIERUNGS-REGRESSION

Scratch-DB `MLM_Perf114`: 9-är-Baum von 5000 Tenants, pro Tenant eine **intra-tenant Rollen-Kette der Tiefe 5**
(4 Vererbungs-Hops), cross-tenant Kante parent-TOP→child-BASE, jeder User hält direkt nur die Basisrolle → die
Closure muss die ganze Kette expandieren. Beide Proc-Chains (`_old`=PRE113 Anker auf `TenantUserRoles`, `_new`=PRE114
Anker auf `GetEffectiveTenantUserRoles()`) nebeneinander, gleiche Session (apples-to-apples). `GetDownwardsRoleTreeProc`
via INSERT..EXEC gemessen (Absolutzeiten plan-noisy, s.u.), 3 Läufe + Warm-up.

**Kernbefund — die parameterlose TVF skaliert mit der GESAMTZAHL der TenantUserRoles, nicht mit dem Query-Scope:**
`GetEffectiveTenantUserRoles()` hat **keine Parameter** und materialisiert die effektive Rollenmenge für ALLE
Tenant-User bei jedem Aufruf. Standalone `SELECT COUNT(*) FROM GetEffectiveTenantUserRoles()` (plan-stabil):
- 5000 User / 25000 direkte TUR → 25000 Closure-Rows: **235 ms**
- 25000 User / 125000 direkte TUR → 125000 Closure-Rows: **~1210 ms** (≈ linear ×5)

Der Prädikat-Pushdown `tur.TenantUserId = tu.TenantUserId` greift durch die **rekursive** CTE nicht zuverlässig →
jede Auth-/Tree-Auflösung zahlt (potenziell) die volle DB-weite Closure, auch ein triviales Leaf-Query.

**End-to-end Proc (plan-instabil, aber Trend eindeutig):**
- @5000 User: leaf 274→485 ms, mid 276→481 ms, root 504→917 ms (old→new), also ~1.7–1.9×; konstanter ~210 ms Aufschlag = die volle Closure.
- @25000 User: leaf/mid ~gleich (Optimizer pusht dort teils den Filter), aber **root 503 ms → 7703 ms (~15×)** — beim teuren Viewpoint eskaliert der kombinierte Plan (Closure groß + Tenant-Tree-Rekursion groß + Table-Variable-1-Row-Estimates).

**Bewertung:** Bei kleiner DB (aktuelle Dev-DB, wenige tausend User) unkritisch (~200 ms, ~1.8×). Aber es **skaliert
nicht**: der Fix-Overhead wächst linear mit der DB-weiten TenantUserRoles-Menge und ist beim teuren Viewpoint
plan-instabil bis ~15× langsamer. Für größere Produktiv-Userbasis = ernsthafte Per-Request-Regression.

**Vorschlag:** Die Closure auf den aufzulösenden User **parametrisieren/filtern** (TenantUserId in die TVF/CTE
hineinreichen, statt DB-weit), damit die Kosten mit dem Scope statt mit der Gesamt-DB skalieren. (Perf-Absolutzahlen
sind INSERT..EXEC/Table-Variable-Artefakte — nur die Relationen/das Skalierungsverhalten sind aussagekräftig; Lehre
aus früheren Messungen: rekursive-CTE+Table-Variable immer in derselben Session vergleichen.)

**Umgesetzt (Toolkit, 2026-07-07):** TVF ist jetzt `GetEffectiveTenantUserRoles(@tenantUserId)` und wird per
`CROSS APPLY(tu.TenantUserId)` aufgerufen — die Rekursion läuft nur noch über die Rollen des einen aufzulösenden
Tenant-Users, nicht mehr DB-weit. Erwartung: der ~210 ms-Sockel und die ~15×-Root-Eskalation @25000 fallen weg
(Scope-lokale Kosten). **Nachmessung auf `MLM_Perf114` (alte vs. neue Proc-Chain, gleiche Session) noch offen.**

## Perf-Nachmessung PRE115 (parametrisierte TVF) — MLM-Session, 2026-07-07

PRE115 macht die Closure-TVF parametrisiert: `GetEffectiveTenantUserRoles(@tenantUserId int)` (nur der eine User),
Anker via `CROSS APPLY (SELECT TOP 1 1 FROM GetEffectiveTenantUserRoles(tu.TenantUserId) er WHERE er.RoleId = pr.RoleId)`.
MLM auf PRE115 + Migration `RecreateToolkitViewsPre115` (ConfigureViews-Redeploy). Funktionsfix intakt (kurt→Nadja(5)/Upline@5).

Gemessen auf der stehenden `MLM_Perf114` (25000 User), dritte Chain `_new2` (=PRE115) neben `_old`/`_new`.

**Deterministischer Kern (plan-stabil) — das eigentliche Ziel:**
- PRE114 parameterlos: `COUNT(*) FROM GetEffectiveTenantUserRoles()` = **125000 Rows, ~1200 ms** — DB-weit, jeder Aufruf.
- PRE115 parametrisiert, 1 User: **5 Rows, ~1 ms**.
- PRE115 künstlich über ALLE 25000 User (CROSS APPLY): 125000 Rows, ~1285 ms (gleiche Gesamtarbeit — bestätigt: kein Zusatz-Overhead, nur nicht mehr DB-weit erzwungen).
→ **Skalierung gefixt: Closure kostet jetzt O(aufzulösender User) statt O(alle DB-User).**

**End-to-end Proc (plan-instabil, Trend über mehrere Läufe):**
- leaf/mid: `_old`≈`_new`≈`_new2` ≈ 280–330 ms (Scope klein → Closure vernachlässigbar in allen Versionen). **Kein Overhead durch PRE115.**
- root (Admin löst 5000er-Subtree in EINEM Call): `_new`(114) schwankt wild 685–8138 ms; `_new2`(115) stabiler aber ~3600–5300 ms; `_old`(113) ~700 ms — aber `_old` liefert nur 1 Row (propagiert die Downline gar nicht), ist also keine gleiche-Arbeit-Baseline. `_new` vs `_new2` (beide 14 Rows, gleiche korrekte Arbeit) sind grob vergleichbar; `_new2` planstabiler.
  Der Root-Whole-Tree-Fall wird von der (vorbestehenden, planinstabilen) Tenant-Tree-Rekursion + Table-Variable-1-Row-Estimates dominiert — NICHT von der Closure; PRE115 verschlechtert das nicht.

**Fazit:** Die gemeldete Skalierungs-Regression (O(alle DB-User) Fixkosten je Aufruf) ist mit PRE115 behoben. Für den
realen Per-User-Auflösungspfad (leaf/mid) kein messbarer Overhead ggü. PRE113. Bleibt nur der von jeher plan-instabile
Whole-Subtree-Root-Call (alle Versionen) — separater, vorbestehender Punkt (Table-Variables→#temp/RECOMPILE), unabhängig vom Closure-Fix.
