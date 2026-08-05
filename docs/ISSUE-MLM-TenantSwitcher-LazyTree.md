# Issue: Lazy-Tree-Primitive für den Tenant-Switcher (Anstoß aus MLM)

**Status:** TOOLKIT-SEITIG ERLEDIGT — AP1 (Lazy-Primitive), AP2 (Anker-Fix, ~60×) und AP3-#1 (~2×) sind
umgesetzt, AP3-#2 bewusst verworfen (siehe „Abschluss-Bewertung" ganz unten). Offen ist nur noch, was in
MLM passiert: AP1-Host-Test und der Umbau des Switchers auf `MudTreeView`+`ServerData`.
*(Die Kopfzeile stand bis 2026-08-05 faelschlich auf OFFEN, obwohl der Rumpf die Umsetzung schon
dokumentierte.)*
**Datum:** 2026-07-06
**Quelle:** MLMManager-Session (Konsument). MLM baut den Tenant-Switcher als eigene Blazor-Komponente
(`MLMManager.Web/Components/Layout/TenantSwitcher.razor`) auf Basis der Tree-Security-API.
**Toolkit-Stand:** `5.0.0-PRE109` (Perf-Fix verifiziert gegen `5.0.0-PRE111` — siehe Abschnitt ganz unten).

## Ziel (Konsumentenwunsch)

Der Tenant-Switcher soll ohne Suche einen **aufklappbaren TreeView** zeigen: oberste (Wurzel-)Tenants
des Users sofort sichtbar, Kinder werden **beim Aufklappen lazy** nachgeladen. Motivation: sobald der
Tenant-Baum größer wird, soll das initiale Rendern nicht auf das Laden des kompletten Zugriffs-Sets
warten müssen. UI-seitig via MudBlazor `MudTreeView<T>` mit `ServerData` (Lazy-Load pro Knoten,
MudBlazor 9.4.0 vorhanden).

## Heutiger Stand der API (verifiziert, PRE109)

Die vorhandenen Bausteine decken den Wunsch **fast**, aber nicht sauber ab:

1. **`ISecurityRepository.GetEligibleScopes(userLabels, authType)`**
   (`TreeShared/Security/DbSecurityRepository.cs:881`) liefert das **komplette** erreichbare Set des
   Users (direkte + über den Rollenbaum geerbte Leaf-Tenants), inkl. `ScopeInfo.AccessMode`
   (`Direct`/`Inherited`). Genau diese Query ist bei großem Baum der teure Teil — sie ist als
   „lade-alles"-Einstieg gedacht, nicht als Wurzel- oder Ebenen-Lieferant. Der heutige MLM-Switcher
   ruft sie einmal in `OnInitialized` und baut den ganzen Baum im Speicher.

2. **`IHierarchySecurityContext.ChildTenantsWith(labels|userId, viewpoint(name|id), requiredPermissions)`**
   (`TreeShared/IHierarchySecurityContext.cs:119-125`, Impl
   `CoreIdentityTree/AspNetTreeSecurityContext`1.cs:686/717/750/781`) liefert `HierarchyTenant[]`
   (inkl. `ParentTenantId`) **permission-gefiltert** und mit interner Trust-Elevation
   (`ShowAllTenants`). Aber: die zugrundeliegende Proc `GetChildTenantsWithPermsProc`
   (`…SqlServer/CoreIdentityTree/SyntaxHelper/SqlColumnsSyntaxHelper.cs:394-427`) baut über
   `GetDownwardsRoleTreeProc` den **gesamten Downwards-Baum** ab Viewpoint und filtert am Ende nur
   auf die Permission — **ohne `ChildLevel`-Einschränkung**. Ergebnis: **der ganze zugängliche
   Subtree** eines Knotens, nicht eine Ebene. Für Lazy-Load bedeutet das: ein Expand lädt den
   kompletten Ast; man kann daraus zwar host-seitig die direkten Kinder (`ParentTenantId==viewpoint`)
   und ein `hasChildren` ableiten, zahlt aber pro Ast-Expand dessen ganze Tiefe.

3. **`GetDownwardsTenantUserRoles(labels, viewpoint)` → `DownwardsUserRoleView`**
   (`TreeShared/IHierarchySecurityContext.cs:109`, View
   `TreeShared/Models/VirtualModels/DownwardsUserRoleView.cs`) hat zwar `ChildLevel`,
   `ViewPointTenantId`, `ChildTenantId/Name`, `ResultingChildRoleId` — aber **keinen
   Permission-Filter** und keine bequeme „hat-Kinder"-Info; die Proc rechnet ebenfalls den ganzen
   Downwards-Baum.

4. **Kein billiges „nur meine obersten Tenants"-Primitive** und **kein striktes „nur direkte Kinder
   von X" (ChildLevel==1)**-Primitive vorhanden.

### Fazit der Lücke

- Für die **Wurzeln** müsste der Konsument entweder `GetEligibleScopes` (teuer, lädt alles) nehmen
  oder die direkten Mitgliedschaften des Users host-seitig als Wurzeln ableiten — Letzteres birgt
  Edge-Cases (User direkt an `P` **und** an einem Nachfahren `D` → `D` erschiene doppelt; Dedup nach
  „topmost/min-ParentLevel" macht heute die `GetEligibleScopes`-Maschinerie). Das im Host
  nachzubauen heißt, ein Stück Eligibility-Logik zu replizieren → fragil, koppelt an Toolkit-Interna.
- Für die **Ebenen** liefert `ChildTenantsWith` zu viel (ganzer Subtree je Expand).

Deshalb soll die Ebenen-/Wurzel-Semantik **im Toolkit** entstehen, nicht im Konsumenten.

## Vorschlag (Toolkit-Seite)

Zwei permission-gegatete Primitive, konsistent zum bestehenden `ChildTenantsWith`
(gleiche Trust-Self-Elevation, gleiche `requiredPermissions`-Semantik, SqlServer + ggf. PostgreSql):

### 1. Direkte Kinder (strikt eine Ebene)

```csharp
// IHierarchySecurityContext<…>
IEnumerable<TenantTreeNode> DirectChildTenantsWith(string[] userLabels, string viewpointTenant,   string[] requiredPermissions);
IEnumerable<TenantTreeNode> DirectChildTenantsWith(string[] userLabels, int?   viewpointTenantId, string[] requiredPermissions);
// + userId-Overloads analog zu ChildTenantsWith
```

Semantik: genau die **direkten** Kinder (`ChildLevel == 1` relativ zum Viewpoint) des Viewpoints, für
die der User mindestens eine der `requiredPermissions` besitzt. Umsetzung minimal-invasiv: dieselbe
`GetChildTenantsWithPermsProc`, nur der finale Select mit `WHERE ChildLevel = 1` (das `@rtQuery`
enthält `ChildLevel` bereits) — plus, damit die UI den Aufklapp-Pfeil korrekt zeigt, **pro Kind ein
`HasAccessibleChildren`-Flag** (existiert unter dem Kind irgendein permission-zugänglicher Nachfahre?
lässt sich aus demselben `@rtQuery` als `EXISTS(ChildLevel > 1 && Ahn == Kind)` berechnen).

### 2. Oberste zugängliche Tenants (Wurzeln der User-Sicht)

```csharp
IEnumerable<TenantTreeNode> TopmostTenantsWith(string[] userLabels, string[] requiredPermissions);
// + userId-Overload
```

Semantik: die **topmost** zugänglichen Tenants des Users (Einstiegspunkte/Wurzeln seines
Zugriffs-Forsts, dedupliziert nach min-ParentLevel — genau die „direkt zugewiesenen, nicht unter
einem anderen zugewiesenen liegenden" Knoten), jeweils mit `HasAccessibleChildren`. Damit kann der
Switcher den Baum initial **ohne** das volle `GetEligibleScopes`-Set seeden.

### DTO

```csharp
public sealed class TenantTreeNode
{
    public int    TenantId { get; set; }
    public int?   ParentTenantId { get; set; }
    public string TenantName { get; set; }        // = ScopeName
    public string DisplayName { get; set; }        // = ScopeDisplayName
    public bool   HasAccessibleChildren { get; set; }
    public ScopeAccessMode AccessMode { get; set; } // Direct/Inherited, wie ScopeInfo (optional, nice-to-have)
}
```

`HasAccessibleChildren` ist der eigentliche Mehrwert fürs Lazy-UI: ohne dieses Flag müsste der
Konsument pro Knoten eine Extra-Query fahren, nur um zu wissen, ob ein Aufklapp-Pfeil zu zeigen ist.

### Optional (nice-to-have): server-seitige Suche

Für die Suche im Lazy-Tree (heute filtert der Switcher flach über das ganze, vorab geladene Set)
wäre ein `SearchAccessibleTenants(userLabels, requiredPermissions, text, maxRows)` hilfreich, das
serverseitig über den zugänglichen Baum sucht (Name/DisplayName, `LIKE`), sodass die Suche nicht das
komplette Set in den Client zieht. Ohne dieses Primitive bleibt Suche = „volles Set laden" und
konterkariert den Lazy-Gedanken, sobald der Baum groß ist.

## Erwartete Nutzung im Konsumenten (MLM), nach Umsetzung

- `OnInitialized` / erstes Öffnen: `TopmostTenantsWith(labels, ["SwitchTenant"])` → Wurzeln sofort,
  Aufklapp-Pfeil aus `HasAccessibleChildren`.
- MudTreeView `ServerData(node)`: `DirectChildTenantsWith(labels, node.TenantName, ["SwitchTenant"])`
  → genau eine Ebene, ein billiger Roundtrip je Expand (auch bei riesigen Ästen).
- Suche: optional `SearchAccessibleTenants(...)` statt Voll-Laden.

`requiredPermissions` = `["SwitchTenant"]` entspricht der Permission, mit der der Switcher heute
überhaupt erst erscheint (`Services.VerifyUserPermissions(new[] { "SwitchTenant" })` in
`TenantSwitcher.razor`). Falls Umschalt-Eligibility toolkit-seitig anders definiert ist (z.B. „jede
Rolle am Tenant genügt"), bitte in der Umsetzung festlegen und hier vermerken.

## Hinweise / Constraints

- Konsistenz mit `ChildTenantsWith`: gleiche interne Trust-Elevation (`ShowAllTenants=true`,
  `IncludeChildTree=true`), damit der ParentTenant-/Namens-Join unabhängig vom aktiven Tenant-Filter
  funktioniert (analog `AspNetTreeSecurityContext`1.cs:717ff`).
- Per-Operation-Context beachten (§8): der Switcher läuft im Blazor-Circuit; die neuen Methoden
  sollten wie `GetEligibleScopes` kollisionsfrei auf einer geleasten Context-Instanz laufen
  (`LeaseContext()`), nicht auf dem geteilten Circuit-Context.
- SqlServer-Proc-Änderung ist gering (nur finaler Select + `HasAccessibleChildren`-Subquery); die
  Wurzel-Variante braucht die „topmost/min-ParentLevel"-Dedup, die es im Upwards-Pfad schon gibt.
- Migration: neue/geänderte Procs = View/Proc-(Re)Create in einer Toolkit-Migration; **keine**
  Schemaänderung an Tabellen erwartet → für den Konsumenten voraussichtlich nur Paket-Bump + `database
  update` (Proc-Redeploy), kein Host-Code außer der Switcher-Umstellung.

## Konsumentenseitige Folgearbeit (MLM), nach Bump

`TenantSwitcher.razor` von der heutigen `MudAutocomplete`-Flachliste (ganzer Baum vorab via
`GetEligibleScopes`, client-seitig eingerückt + gefiltert) umbauen auf `MudTreeView` + `ServerData`
gegen die neuen Primitive. Static-SSR-Fallback (Identity-Pages ohne Circuit) und die
`forceLoad`-`?tenant=`-Switch-Navigation bleiben unverändert.

---

## Antwort/Analyse aus der Toolkit-Session (2026-07-06)

**Entscheidung zur Eligibility-Semantik (vom Toolkit-Verantwortlichen festgelegt):** Für den Switcher
gilt **„any access"** — ein Tenant ist sichtbar/umschaltbar, sobald der User dort **irgendeine Rolle
(⇒ mindestens 1 Permission)** hat, direkt oder über den Rollenbaum geerbt. **Kein** Filter auf eine
spezifische Permission (`SwitchTenant` o.ä.). **Konsequenz:** Der im §-Vorschlag zentrale
Permission-Join entfällt — die beiden dort skizzierten neuen Procs (`DirectChildTenantsWith` /
`TopmostTenantsWith` mit `requiredPermissions`) werden **nicht** gebaut.

### Kernbefund: Unter „any access" ist die Struktur bereits vorhanden

Die role-basierte Erreichbarkeit ist schon materialisiert und deckt „any access" exakt ab:

- **`TenantAccessTreeDown`** (View, `SqlColumnsSyntaxHelper.cs:232`) = pro User über
  `TenantUsers → TenantUserRoles` + `RoleRoles`-Vererbung nach unten der **komplette zugängliche
  Forst** mit `ChildTenantId`, `TopmostTenantId`, `level`.
- Die kombinierte View **`TenantAccessTree`** ist bereits als **`context.UserAccessTree`**
  (`DbSet<UserAccessTree<string>>` auf `IHierarchySecurityContext`) exponiert und trägt `ChildTenantId`,
  `ParentTenantId`, `TopmostTenantId`, `UserId`, `ParentLevel` **und `DirectAssign`** (= das optional
  gewünschte `AccessMode` gratis). **Kein** Global-Filter → direkt per `UserId` abfragbar.

Damit ist alles host-seitig aus **einer** Menge ableitbar
(`accessibleIds(U) = DISTINCT ChildTenantId WHERE UserId==U`), reines LINQ, **ohne Proc-/Schemaänderung**:

```csharp
// Wurzeln: zugängliche Tenants, deren Parent NICHT zugänglich ist
//          (löst das P-und-D-Doppel-Dedup sauber, ohne Eligibility-Logik nachzubauen)
roots          = accessible.Where(t => t.ParentTenantId == null || !accessibleIds.Contains(t.ParentTenantId.Value));
// Expand(V): direkte Kinder
directChildren = tenants.Where(t => t.ParentTenantId == V && accessibleIds.Contains(t.TenantId));
// hasChildren(X): Aufklapp-Pfeil
hasChildren    = tenants.Any(c => c.ParentTenantId == X && accessibleIds.Contains(c.TenantId));
```

### Der einzige echte Vorbehalt → BITTE MLM-SEITIG ABKLÄREN

`TenantAccessTree`/`…Down` ist eine **DB-weite rekursive CTE-View**. Ob das `WHERE UserId=@u`-Prädikat
in die Rekursion **gepusht** wird, entscheidet über die Lazy-Tauglichkeit: pusht SQL Server nicht,
rechnet die View die Forst **für alle User**, bevor gefiltert wird → konterkariert „lazy" bei großem
Mandantenbaum.

**→ Aufgabe MLM-Session:** Query-Plan von
`SELECT DISTINCT ChildTenantId FROM TenantAccessTree WHERE UserId=@u` auf einer realistisch großen DB
prüfen (Pushdown ja/nein?).

Für den Fall „kein Pushdown / zu teuer" existiert das passende Primitive **bereits**:
**`GetDownwardsTenantUserRoles(labels, viewpoint)`** (Proc `GetDownwardsRoleTreeProc`,
`IHierarchySecurityContext.cs:109`) ist **user- UND viewpoint-scoped und OHNE Permission-Filter** — also
je Expand nur der Subtree unter dem Knoten. Die Issue-Kritik #3 daran („kein Permission-Filter") ist
unter „any access" **gegenstandslos** — der fehlende Filter ist hier genau erwünscht. `ChildLevel == 1`
+ `hasChildren` host-seitig ableiten.

### Handlungsoptionen (Ergebnis hängt am Plan-Check)

| Variante | Toolkit-Änderung | Wann |
|---|---|---|
| **0 — nichts im Toolkit** | keine | Pushdown ok. Switcher rein konsumentenseitig: Wurzeln + Baum via `context.UserAccessTree` (LINQ) bzw. lazy via `GetDownwardsTenantUserRoles(labels, viewpoint)` je Expand. Funktioniert heute. |
| **1 — dünner Repo-Wrapper** | klein, keine Procs/Schema | Pushdown NICHT ok **oder** Circuit-Safety/DTO zentral gewünscht: `LeaseContext()`-Wrapper auf `ISecurityRepository`, der `UserAccessTree` bzw. den viewpoint-Proc kapselt und `TenantTreeNode` liefert. |

**Circuit-Safety-Hinweis (gilt für beide Varianten):** Der Switcher läuft im Blazor-Circuit. Direkt auf
`context.UserAccessTree` zu queryen ist das bekannte „second operation on this context"-Risiko
(vgl. PRE079 / `feedback_dbcontext_reentry`). Entweder MLM nutzt einen **per-Operation-Context**
(`IDbContextFactory`/eigener Scope) für die Switcher-Reads, **oder** wir liefern Variante 1 (Wrapper mit
`LeaseContext()`). Die reine „nichts im Toolkit"-Variante 0 verlagert diese Pflicht an den Konsumenten.

**Nächster Schritt:** MLM macht den Plan-Check und entscheidet 0 vs. 1; erst danach ggf. Toolkit-Arbeit
(dann nur der kleine Wrapper, **keine** neuen Permission-Procs).

---

## Ergebnis Plan-Check aus der MLM-Session (2026-07-06)

**Fazit: KEIN effektiver Pushdown → Variante 0 fällt weg.** `… FROM TenantAccessTree WHERE UserId=@u`
ist bei großem Baum nicht lazy-tauglich — schon der **einmalige** Init-Load trägt einen großen
user-**un**abhängigen Kostenboden.

### Methode

Da die reale MLM-DB winzig ist (3 Tenants → kein aussagekräftiger Plan), habe ich eine synthetische
Scratch-DB (`MLM_PlanCheck` auf `(localdb)\mssqllocaldb`) gebaut: die **4 Views verbatim** aus
`SqlColumnsSyntaxHelper.cs` (`UpwardsTenantTree`, `TenantAccessTreeDown`, `TenantAccessTreeUp`,
`TenantAccessTree`), dazu ein **9-ärer Tenant-Baum mit 5000 Tenants**, pro Tenant ein **direkt
zugewiesener User** (5000 `TenantUsers`/`SecurityRoles`/`TenantUserRoles`), und `RoleRoles`, die den
Baum spiegeln (Parent-Rolle → Kind-Rolle) — d.h. Abwärts-Vererbung greift über die ganze Tiefe.
Indizes gesetzt (PKs, `Tenants.ParentTenantId`, `TenantUsers.UserId/TenantId`, `SecurityRoles.TenantId`,
`RoleRoles.PermissiveRoleId`). Gemessen mit `SET STATISTICS IO, TIME` + `OPTION (RECOMPILE)`.

### Zahlen (5000 Tenants)

| Query | zugängliches Set | CPU / elapsed | Logical Reads (Σ) |
|---|---|---|---|
| `WHERE UserId='U4999'` (**Leaf**) | **1 Tenant** | 297 / **294 ms** | **~460 000** |
| `WHERE UserId='U1'` (**Root**) | ganzer Baum | 5250 / **5542 ms** | ~615 000 |
| kein Filter (ganzer View) | alle User | 2360 / 2690 ms | ~720 000 |

### Interpretation

- **Der Leaf-User (Set-Größe 1) kostet ~294 ms und ~460k Logical Reads** — dieselbe Größenordnung wie
  der komplette ungefilterte View. Bei echtem Pushdown müsste er nahe null sein. Er ist es **nicht**.
- Der `WHERE UserId`-Filter prunt nur den `rDown`-Anker von `TenantAccessTreeDown` (Root 5,5 s vs. Leaf
  0,29 s ≈ 19× — die Abwärts-Rekursion wird also user-scoped). **Aber** `UpwardsTenantTree` /
  `TenantAccessTreeUp` sind rein **strukturell über den ganzen Tenant-Baum** und user-unabhängig:
  schon für den Leaf-User `Tenants` = 114k Reads, `Worktable` = 270k. Dieser Boden bleibt, egal wie
  klein das User-Set ist, und wächst super-linear mit der Tenant-Zahl (Join-Blow-up:
  `RoleRoles` Scan count 19 078 beim Leaf, 43 156 beim Full).
- Damit ist auch die im Vorschlag skizzierte Variante-0-Idee („accessibleIds(U) **einmal** laden, dann
  reines LINQ") am großen Baum die Anti-Lösung: der Root-User zahlt bei nur 5000 Tenants 5,5 s beim
  ersten Öffnen — genau das, was der Feature-Wunsch vermeiden soll. `GetEligibleScopes` (leaf-basiert,
  ohne den Up/Down-Ranking-Join) dürfte sogar günstiger sein als dieser View.

### Nachtrag: Viewpoint-Proc `GetDownwardsRoleTreeProc` ebenfalls gemessen (2026-07-06)

Die komplette Proc-Kette (`GetDownwardsRoleTreeProc` + `GetUpwardsRoleTreeForId` + View
`DownwardsTenantTree` + `Users`) verbatim aus `SqlColumnsSyntaxHelper.cs` in `MLM_PlanCheck` nachgebaut
und pro Viewpoint gemessen (je 5 Aufrufe, Ø Wall-Clock, 5000-Tenant-Baum):

| Viewpoint | Proc-Rows | Ø ms/Aufruf |
|---|---|---|
| Leaf `T4999`  | **1** | **601** |
| Deep `T4000`  | 1 | 615 |
| Mid `T50`     | 91 | 609 |
| Top `T2`      | 820 | 656 |
| Root `T1`     | 5000 | 710 |

**Der Fallback trägt denselben Ganzbaum-Boden.** Die Kosten sind **flach ~600–710 ms, unabhängig von der
Subtree-Größe** — ein Leaf-Viewpoint (1 Ergebniszeile) kostet 601 ms, praktisch gleich viel wie der Root
(710 ms für 5000). Ursache: `GetDownwardsRoleTreeProc` materialisiert `UpwardsTenantTree` **und**
`DownwardsTenantTree` (beide **Ganzbaum**-Rekursionen), plus `GetUpwardsRoleTreeForId`, und filtert erst
danach; der `where outermostleaftenantname=@viewPoint`-Filter wird **nicht** in die rekursive CTE gepusht.
`GetDownwardsTenantUserRoles(labels, viewpoint)` je Expand ist damit **auch nicht** lazy-tauglich.

### Kernbefund: keine der bestehenden Primitive ist lazy-tauglich — der billige Weg ist non-rekursiv

Beide rekursiven Wege (`TenantAccessTree WHERE UserId`; `GetDownwardsRoleTreeProc`) materialisieren die
Ganzbaum-CTEs (`UpwardsTenantTree`/`DownwardsTenantTree`/`TenantAccessTree*`) und filtern erst danach →
fixer Boden, super-linear wachsend mit der Tenant-Zahl. **Prototyp einer non-rekursiven Ein-Ebenen-Query**
(gegeben Knoten X + die **schon bekannte** Resulting-Role, die der User an X hält — beim Expandieren
mitgeführt: direkte Kinder = Kinder mit RoleRoles-Vererbungskante aus dieser Rolle **oder** direkter
Membership) gemessen, 200 Aufrufe, Ø:

| Expand | Kinder | Ø **Mikrosekunden**/Aufruf |
|---|---|---|
| Leaf `X=4999` | 0 | **15 µs** |
| Root `X=1`    | 9 | 37 µs |
| Top  `X=2`    | 9 | 39 µs |
| Mid  `X=50`   | 9 | 65 µs |

**~15–65 µs statt ~600 ms — Faktor ~10 000×,** und skaliert mit der **Kinderzahl**, nicht mit dem Baum.
Das ist der einzige Weg, der echtes Lazy-Load liefert.

### Empfehlung (revidiert)

- **Variante 0 verwerfen** und **den Fallback `GetDownwardsTenantUserRoles` pro Expand verwerfen** —
  beide haben den Ganzbaum-Boden (~300 ms bzw. ~600 ms schon bei 5000 Tenants, wachsend).
- Genuines Lazy-Load braucht eine **neue, non-rekursive Ein-Ebenen-Primitive** im Toolkit, die die
  Resulting-Role(s) des Users am aktuellen Knoten **mitführt** und daraus die direkten Kinder in **einem
  Join** (RoleRoles-Kante + direkte Membership, „any access") ableitet — plus `HasAccessibleChildren` als
  `EXISTS` derselben Ein-Ebenen-Logik. Kosten ~O(Kinder × Rollen-an-X), im µs-Bereich.
- **Wurzeln** analog non-rekursiv: `TenantUsers WHERE UserId=@u` (Index-Seek) = die direkten
  Memberships des Users als Einstiegsknoten, jeweils mit ihrer Rolle als Startwert fürs Expandieren;
  P-und-D-Dedup betrifft nur die wenigen Membership-Zeilen.
- Diese Primitive gehört ins **Toolkit** (nicht in den Host): sie muss die echten Modell-Feinheiten
  korrekt abbilden, die mein synthetisches Modell vereinfacht — **mehrere** SecurityRoles/PermissionSets
  pro Tenant, RoleRoles-Fan-out, GlobalRoles über `GlobalToLocalRoles`/`GlobalRolePermissions`. Mein
  Prototyp beweist die **Größenordnung** (µs, O(Kinder)), ist aber kein Drop-in.
- Der ursprüngliche `ChildTenantsWith`/`DirectChildTenantsWith`-Gedanke war richtig — **aber** er darf
  NICHT über `GetDownwardsRoleTreeProc` + `WHERE ChildLevel=1` gebaut werden (das ist exakt der gemessene
  600-ms-Boden). Er muss als **eigenständige non-rekursive** Proc/Query entstehen.

### Bonus-Befund: die Rekursionen sind *nicht* per se langsam — nur der Anker ist falsch parametrisiert

(Auf Nachfrage aus MLM: allgemeine Downward-from-Viewpoint-Performance, unabhängig vom Dropdown.)

Nachgemessen (5000-Tenant-Baum, Ø je 20 Aufrufe), wo der Ganzbaum-Boden **wirklich** sitzt:

| Query | Viewpoint | Rows | Ø |
|---|---|---|---|
| `DownwardsTenantTree WHERE TopmostTenantId=@vp` (strukturell down, gefiltert) | Leaf→Root | 1→5000 | 0 µs → 37 ms (skaliert mit Subtree) |
| Anker-parametrisierte Down-Rekursion (strukturell / rollen-bewusst) | Leaf | 1 | 50 µs / 49 µs |
| `UpwardsTenantTree WHERE OutermostLeafTenantId=@vp` (strukturell up, gefiltert) | alle | 1–5 | **0–50 µs** |
| **`GetUpwardsRoleTreeForId(@u,@vp)`** (Rollen-Up-Auflösung) | **alle** | **1** | **~170–189 ms, flach** |

**Diagnose:** Die **strukturellen** Tenant-Rekursionen (up & down) pushen den Viewpoint-Filter sauber in den
Anker → O(Subtree) bzw. O(Tiefe), µs-Bereich. Der **einzige** Ganzbaum-Boden liegt in der
**Rollen-Vererbungs-Rekursion**: das `r`-CTE in `GetUpwardsRoleTreeForId` (= dieselbe Form wie
`TenantAccessTreeUp`) ankert auf `FROM SecurityRoles` (**alle** Rollen) und der `@user`/`@viewPoint`-Filter
steht **über** der Rekursion → volle Materialisierung, dann Filter (~170 ms flach, obwohl 1 Zeile
rauskommt). `GetDownwardsRoleTreeProc` erbt diesen Boden (ruft die Funktion), `TenantAccessTree` ebenso
(joint `…Up`).

**Genereller Fix (toolkit-weit, nicht nur Dropdown):** rekursive Rollen-Auflösung **anker-parametrisieren** —
Rekursion beim konkreten Viewpoint/User starten und die Resulting-Role **mitführen**, statt eine
allgemeine Ganzbaum-Rekursion oben zu filtern. Konkret: `GetUpwardsRoleTreeForId` / `TenantAccessTreeUp`
so umbauen, dass der Anker `WHERE TenantId=@vp` (bzw. die User-Membership am Viewpoint) trägt und aufwärts
läuft (O(Tiefe)); analog die downward-Rollenauflösung ab Viewpoint. Die strukturellen Views zeigen, dass
der Optimizer den Anker-Push kann, sobald das Prädikat auf der Anker-Schlüsselspalte sitzt und nicht erst
hinter Zusatz-Joins. Nutzen: schnellere Rechte-/Scope-Auflösung überall, wo „von einem Viewpoint nach
unten/oben" selektiert wird.

### Scratch-Harness

`…/scratchpad/plancheck_setup.sql` (Baum+4 Views), `plancheck_measure.sql` (View-Pfad),
`plancheck_proc_setup.sql` (Proc-Kette), `plancheck_proc_clean.sql` (Viewpoint-Messung),
`plancheck_onelevel.sql` (non-rekursiver Prototyp), `plancheck_anchored.sql` (View-Filter vs.
Anker-parametrisierte Down-Rekursion), `plancheck_upward.sql` (Up-View vs. Rollen-Up-Funktion) — alle
reproduzierbar. DB `MLM_PlanCheck` wird nach dem Review gedroppt.

---

## Finaler Design-Stand (Toolkit-Session, 2026-07-06) — verbindlich

Konsolidiert nach zwei Runden Klärung mit dem Toolkit-Verantwortlichen. Dieser Abschnitt **ersetzt** die
frühere „Antwort/Analyse"-Sektion (Variante 0/1) — die ist durch die MLM-Messung überholt.

### 1. Zugriffs-Gate: ≥1 **Permission**, nicht „irgendeine Rolle"

Ein Tenant ist sichtbar/umschaltbar, sobald der User dort **mindestens ein Recht** hat. Das bloße
Vorhandensein einer (auch durchgereichten) Rolle genügt **nicht** — MLM baut Rollenbäume, um User nach
unten durchzureichen, aber der Ziel-Admin entscheidet über die tatsächlichen Rechte. Präzise:

> `zugänglich(T)` ⇔ ∃ Rolle `r`, die der User an `T` hält (direkt via `TenantUserRole` **oder** vererbt via
> `RoleRoles` von einem Vorfahren), mit **≥1 Permission**, wobei
> `perms(r) = RolePermissions(r) ∪ GlobalRolePermissions( GlobalToLocalRoles(r) )`.

- **GlobalRole** ist reines Permission-Set an einer SecurityRole (spart, bei 10 000 Tenants nicht ~100
  Permissions einzeln zu vergeben). Sie gewährt **keinen eigenen Zugriffspfad** — sie wirkt nur, wenn die
  **konkrete lokale Ziel-Rolle** die GlobalRole via `GlobalToLocalRoles.LocalRoleId = r` **anzieht**
  (Beispiel: GlobalRole `TenantAdmin` an der ADM-Rolle, aber am Ebene-3-Child hält der User nur
  `DefaultUser` → dort sieht er was, darf aber nicht alles, was `TenantAdmin` dürfte).
- Es ist **„≥1 Permission" (COUNT≥1)**, **kein** Filter auf eine bestimmte Permission (kein `SwitchTenant`).
- Das ist exakt `GetChildTenantsWithPermsProc` **ohne** den `@permRaw`-Filter (der `@perm2T`-Aufbau via
  `RolePermissions` **und** `GlobalToLocalRoles→GlobalRolePermissions` bleibt, nur die Einschränkung auf
  konkrete Permission-Namen fällt weg).

### 2. Genau EIN Zugriffsmodus — kein Sysadmin-„See-all"

Auch der Sysadmin kommt nur per direkter Membership oder Rollenvererbung auf einen Tenant. Es gibt **keinen**
strukturellen „alle Tenants"-Zweig. Das `ShowAllTenants=true` (wie in `GetEligibleScopes`) ist **nur
EF-Filter-Bypass** während der Auflösung (damit die Parent-/Namens-Joins nicht vom aktiven Tenant-Filter
geclippt werden), **nicht** Zugriffs-Erweiterung.

### 3. Perf-Hebel = Anker-Parametrisierung (bestätigt durch den Bonus-Befund oben)

Nicht „Rekursion" ist teuer, sondern der **falsch geankerte** Rollen-CTE (`GetUpwardsRoleTreeForId` ankert
`FROM SecurityRoles` = alle Rollen, filtert `@user/@viewPoint` über der Rekursion → ~170 ms flach). Das
Lazy-Ein-Ebenen-Primitive ist die **Extremform** der Anker-Parametrisierung: Rekursion beim konkreten
Viewpoint/User starten, die **Resulting-Role mitführen** → Kosten O(erreichbarer-Teilbaum), nicht
O(Gesamtbaum). Der Prototyp `plancheck_onelevel.sql` belegt die Größenordnung (~15–65 µs, skaliert mit
Kinderzahl).

### 4. Pass-Through-Darstellung: **Option B (flach/gelupft)** — ENTSCHIEDEN

Weil das Gate permission-basiert ist, gibt es **Pass-Through-Tenants** (Rolle vorhanden, aber 0 Permission —
reine Durchreiche zu einem tieferen berechtigten Tenant). Entscheidung: **Option B.**

> Der Baum zeigt **nur zugängliche Tenants** (≥1 Permission). Jeder erscheint unter seinem **nächsten
> zugänglichen Vorfahren**. Pass-Through-Tenants sind **unsichtbar** (übersprungen/kollabiert).

Semantik daraus:
- **Wurzeln** = topmost zugängliche Tenants (kein zugänglicher Vorfahre).
- **Expand(X)** = von `X` **durch die Pass-Through-Spanne nach unten laufen** bis zur nächsten
  zugänglichen „Frontier" — diese Frontier-Knoten sind die Kinder (können mehrere strukturelle Ebenen tief
  liegen). Unter einem gefundenen zugänglichen Knoten wird **nicht** weiter gelaufen (dessen eigenes Expand
  übernimmt das).
- **`HasAccessibleChildren(X)`** = ∃ zugänglicher Knoten strikt unter `X` — als **kurzschließender**
  Abwärts-Walk (stoppt beim ersten Treffer).
- Kosten: O(Spanne bis zur Frontier), geankert/rollen-tragend, **nicht** Gesamtbaum. Tiefe Pass-Through-
  Spannen kosten proportional zu ihrer Länge, nicht zur DB-Größe.

### 5. API-Skizze (revidiert, verbindlich)

```csharp
// ISecurityRepository — circuit-safe via LeaseContext(); non-rekursiv/geankert; SqlServer
IReadOnlyList<TenantTreeNode> GetRootTenants(string[] userLabels, string authType);
IReadOnlyList<TenantTreeNode> GetChildTenants(string[] userLabels, string authType,
                                              int parentTenantId, int[] carriedRoleIds);

public sealed class TenantTreeNode {
    public int    TenantId;      public int? ParentTenantId;
    public string TenantName;    public string DisplayName;
    public bool   HasAccessibleChildren;
    public ScopeAccessMode AccessMode;   // Direct/Inherited, aus dem Join
    internal int[] CarriedRoleIds;       // opak: bleibt im Blazor-Circuit, nie an den Browser
}
```

- **Gate je Knoten** = `EXISTS(RolePermissions WHERE RoleId ∈ carriedRoles) OR EXISTS(GlobalToLocalRoles g
  WHERE g.LocalRoleId ∈ carriedRoles AND EXISTS(GlobalRolePermissions WHERE GlobalRoleId=g.GlobalRoleId))`.
- `carriedRoleIds` = **alle** an `X` gehaltenen Rollen (rechtelose Rollen müssen vererben dürfen); das
  Sichtbar-Gate ist separat (≥1 Permission).
- Umsetzung als **eigenständige, non-rekursive/anker-parametrisierte** Proc/`FromSql` — **NICHT** über
  `GetDownwardsRoleTreeProc` + `WHERE ChildLevel=1` (das ist der gemessene ~600-ms-Boden).
- Platzierung `ISecurityRepository` + `LeaseContext()` (Blazor-Circuit-Sicherheit, wie `GetEligibleScopes`).

### 6. Zwei Arbeitspakete

- **AP1 (Switcher, jetzt):** obige Primitive (`GetRootTenants` / `GetChildTenants` / `HasAccessibleChildren`,
  Option-B-Semantik, Permission-Gate inkl. GlobalRole-Anziehung), SqlServer, Repo-Wrapper. Danach MLM: Umbau
  `TenantSwitcher.razor` auf `MudTreeView` + `ServerData`; `carriedRoleIds` als opaker Node-State;
  Static-SSR-Fallback + `?tenant=`-Switch unverändert.
- **AP2 (separat, toolkit-weit):** MLMs **genereller Anker-Fix** von `GetUpwardsRoleTreeForId` /
  `TenantAccessTreeUp` (Anker an Viewpoint/User statt Filter über der Rekursion). Beschleunigt
  `GetEligibleScopes` und jede „von-Viewpoint-Scope-/Rechte-Auflösung. **Großer Blast-Radius** (Migrationen,
  viele Konsumenten) → bewusst getrennt von AP1.

**Status:** Design abgeschlossen/verbindlich. Nächster Schritt = AP1 im Toolkit implementieren (SqlServer,
Migration = neue Proc(s) + keyless `TenantTreeNode`-Mapping; keine Tabellen-Schemaänderung).

### 7. AP1 umgesetzt (Toolkit-Session, 2026-07-06) — Build grün, Host-Test offen

Implementiert — **abweichend vom Design-Text als LINQ-to-Entities statt SqlServer-Proc** (das Ein-Ebenen-
Primitive ist non-rekursiv → in EF ausdrückbar). Vorteile: **keine Proc, keine Migration, kein keyless-
Mapping, kein Schema-/Proc-Deploy** beim Konsumenten → nur Paket-Bump. Type-safe, provider-neutral (nur die
Tree-Repo implementiert es, Flat/Decorators liefern leere Liste).

**Geänderte Dateien:**
- `ITVComponents.WebCoreToolkit/Models/TenantTreeNode.cs` — neues DTO (`TenantId`, `ParentTenantId`,
  `TenantName`, `DisplayName`, `HasAccessibleChildren`, `AccessMode`, opakes `CarriedRoleIds`).
- `ITVComponents.WebCoreToolkit/Security/ISecurityRepository.cs` — zwei **DIM** (Default = leere Liste):
  `GetRootTenants(labels, authType)` und `GetChildTenants(labels, authType, parentTenantId, carriedRoleIds)`.
- Decorator-Passthrough (sonst schluckt der DIM-No-op den Call): `SecurityRepository`, `CookiePermissionRepo`
  (→ `parentRepo`), `AssetSecurityRepository` (→ `decoratedRepo`).
- `…TenantSecurity/TreeShared/Security/DbSecurityRepository.cs` — Impl: `GetRootTenants`/`GetChildTenants`
  über `ReadDetached`/`LeaseContext()` (circuit-safe) + `CreateForCaller(ShowAllTenants=true)` (Filter-Bypass);
  Ein-Ebenen-LINQ (`ChildLevel`: direkte Kinder mit inherited [`RoleRoles`-Kante aus `carried`] ∪ direkter
  Membership, je Rolle `HasPermission` = `RolePermissions` ∪ `GlobalToLocalRoles→GlobalRolePermissions`);
  Option-B-Logik gekapselt in privater `TenantTreeWalker` (Aggregat je Tenant, Frontier-Collapse durch
  Pass-Through, strukturelle Wurzel-Ableitung ohne Cross-Nesting, `HasAccessibleChildren` = kurzschließender
  Abwärts-Walk).

**Konsumenten-API (MLM):**
```csharp
// roots (einmal beim Öffnen):
IReadOnlyList<TenantTreeNode> roots = securityRepository.GetRootTenants(userLabels, authType);
// expand (MudTreeView ServerData je Knoten):
IReadOnlyList<TenantTreeNode> kids = securityRepository.GetChildTenants(userLabels, authType, node.TenantId, node.CarriedRoleIds);
```
`node.CarriedRoleIds` opak weiterreichen (bleibt im Server-Circuit). `HasAccessibleChildren` steuert den
Aufklapp-Pfeil.

**Build:** `WebCoreToolkit`, `…TenantSecurity`, `…TenantSecurity.SqlServer` grün (0 Fehler).

**OFFEN / Host-Test nötig (kann die Toolkit-Session nicht lokal, keine Tree-DB):**
1. **EF-SQL-Übersetzung** von `ChildLevel` (verschachtelte `Any` + `array.Contains(nullable.Value)` im `where`
   und in der Projektion) — muss auf realer SQL-Server-Tree-DB als gültiges SQL rauskommen; sonst Query
   umbauen (z. B. explizite Joins statt `Contains`).
2. **Semantik** gegen echte MLM-Daten: Permission-Gate inkl. GlobalRole-Anziehung, Pass-Through-Collapse,
   Wurzel-Dedup (P-und-D), `HasAccessibleChildren`.
3. **Perf** am großen Baum: bestätigen, dass die Ein-Ebenen-Queries wirklich anker-parametrisiert (Index auf
   `Tenants.ParentTenantId`) laufen und nicht in EF zu einem teuren Plan werden.

Danach: Paket-Bump + MLM baut `TenantSwitcher.razor` auf `MudTreeView`+`ServerData` um. **AP2** (genereller
Anker-Fix `GetUpwardsRoleTreeForId`) bleibt separat/offen.

### 8. AP2 — Anker-Fix der Upward-Rollen-Funktionen: UMGESETZT (Toolkit-Session, 2026-07-06), Build grün

**Implementiert.** Die vier `GetUpwardsRoleTree*`-Funktionen in `SqlColumnsSyntaxHelper.cs` haben jetzt am
Anker der rekursiven `r`-CTE den Leaf-Filter; neue Redeploy-Migration
`20260706120000_RedeployAnchoredUpwardsRoleTree` (ruft `ConfigureViews` → dropt+legt alle Objekte neu an,
idempotent). SqlServer-Projekt baut grün. **Semantik-Verifikation + Messung bleiben MLM** (reine SQL-Strings →
`dotnet build` prüft sie nicht; `dotnet ef` ist hier durch einen net10-Design-Time-Fehler blockiert, s. u.).

**Die Änderung (genau eine `WHERE`-Zeile pro Funktion, an den Anker der rekursiven `r`-CTE):**

| Funktion (`SqlColumnsSyntaxHelper.cs`) | Anker `... inner join Tenants st on st.TenantId = s.TenantId` **ergänzen um** |
|---|---|
| `GetUpwardsRoleTreeForId` (`urtIdFunc`) | ` where (@FromLeaf is null or st.TenantName = @FromLeaf)` |
| `GetUpwardsRoleTreeForLabels` (`urtLblFunc`) | ` where (@FromLeaf is null or st.TenantName = @FromLeaf)` |
| `GetUpwardsRoleTreeForIdByLeafId` (`urtIdFuncTi`) | ` where (@FromLeafTenantId is null or s.TenantId = @FromLeafTenantId)` |
| `GetUpwardsRoleTreeForLabelsByLeafId` (`urtLblFuncTi`) | ` where (@FromLeafTenantId is null or s.TenantId = @FromLeafTenantId)` |

Der äußere `where …OutermostLeafTenantName/…LeafTenantId…` bleibt **unverändert** stehen (Redundanz bei
gesetztem Leaf, nötig für den `null`-Fall).

**Äquivalenz-Beweis:** In der Rekursion ist `TenantId` **schleifen-invariant** — der rekursive `SELECT` trägt
`r_2.RoleId … r_2.TenantId` unverändert weiter; nur `ParentRoleId`/`ParentTenantId` wandern hoch. Also gilt für
jede Kette: `r.TenantId` = Tenant des Anker-Rollen. Der äußere Query pinnt `r.TenantId = t.OutermostLeafTenantId`
und `t.OutermostLeafTenantName/…Id = @FromLeaf/@FromLeafTenantId` → nur Ketten mit `r.TenantId` = Leaf-Tenant
überleben ohnehin. Den Anker vorab auf genau diesen Tenant zu filtern **entfernt keine Ergebniszeile**, sondern
nur die Rekursions-Äste, die der äußere Filter später eh verwirft. → **Ergebnis identisch, Rekursion von
„alle Rollen" auf „Rollen am Viewpoint" reduziert** (µs statt ~170 ms flach, MLM-Messung).

**SCOPE-GUARD — NICHT anfassen (kein Drop-in!):**
- Das **inline `r`-CTE in `GetDownwardsRoleTreeProc` / `GetDownwardsRoleTreeByVpIdProc`** (gleicher Anker-Text!)
  **nicht** mit demselben Filter belegen: dort rangiert `r.TenantId` über die **Kind**-Tenants (aus
  `@rawtree`/`@resultingUpTree`), nicht über den Viewpoint. Ein einzelner Viewpoint-Filter am Anker wäre dort
  **falsch**. (Optimierung dieses Ankers = eigene Analyse, Anker auf die Kind-Tenant-Menge — offen.)
- Die **View `TenantAccessTreeUp`** ist parameterlos → nicht anker-parametrisierbar ohne sie in eine Funktion
  umzubauen (größerer Umbau; `TenantAccessTree`/`UserAccessTree` ist eine `DbSet`-View). AP1 lenkt den Switcher
  ohnehin daran vorbei. Separat lassen.
- **`null`-Leaf-Fall** (z. B. `GetEligibleScopes` → `GetUpwardsTenantUserRoles(labels, null)`) bleibt
  Ganzbaum („lade-alles") — bewusst außerhalb AP2.

**Deploy (umgesetzt):** Migration `20260706120000_RedeployAnchoredUpwardsRoleTree` — `Up()` ruft
`SqlColumnsSyntaxHelper.ConfigureViews(migrationBuilder);` (analog `20241125163308_SetViewCode`, dropt-if-exists
+ legt alle Views/Procs/Funktionen neu an). Kein Tabellen-Schema-Change. Designer wurde vom letzten Migration-
Designer 1:1 übernommen (Modell **unverändert**, nur SQL-Bodies in `ConfigureViews`).

**Caveat Designer/Snapshot:** `dotnet ef` ließ sich hier nicht laufen (Design-Time-Fehler
`IdentityPasskeyData requires a primary key` — net10-Identity-Lücke, **unabhängig** von AP2). Der Designer der
neuen Migration ist daher eine verbatim-Kopie des `RolePermissionTypeFix`-Designers (`ProductVersion 8.0.11`,
wie der aktuelle `ModelSnapshot`) — korrekt, solange das Modell unverändert ist. Falls MLM den Snapshot bereits
auf net10 gehoben hat (inkl. `IdentityPasskeyData`), stattdessen die Migration in MLMs Umgebung via
`dotnet ef migrations add` regenerieren (der `Up()`-Einzeiler bleibt gleich).

**MLM-Verifikation:** (1) Semantik-Spot-Check gegen echte Daten (identische Zeilen vor/nach für ein paar
User×Viewpoint), (2) Messung `GetDownwardsRoleTreeProc`/`GetChildTenantsWithPermsProc` vor/nach (erwartet:
weg vom ~600-ms-Boden für den Upward-Anteil).

---

## Perf-Verifikation PRE109 → PRE111 (MLM-Session, 2026-07-06)

PRE111 enthält den Anker-Fix: die vier Upward-Role-Funktionen (`GetUpwardsRoleTreeForId` / `…ForLabels` /
`…ByLeafId`) filtern jetzt **im `r`-CTE-Anker** (`where (@FromLeaf is null or st.TenantName=@FromLeaf)`
bzw. `s.TenantId=@FromLeafTenantId`). Konsumentenseitig via Migration `RecreateToolkitViews`
(`SqlColumnsSyntaxHelper.ConfigureViews(migrationBuilder)`) redeployed — Zahlen im selben Scratch-Harness
(5000-Tenant-Baum), Scratch-Funktion 1:1 auf die PRE111-DDL gepatcht.

### 1. Isolierte Funktion `GetUpwardsRoleTreeForId(@u,@vp)` — **großer Gewinn, robust**

| Viewpoint | PRE109 | PRE111 |
|---|---|---|
| Root `T1`  | 189 ms | **2,8 ms** |
| Top `T2`   | 172 ms | **2,7 ms** |
| Mid `T50`  | 170 ms | **2,8 ms** |
| Leaf `T4999` | 170 ms | **7,8 ms** |

**~25–60× schneller, Boden weg.** Der Anker-Fix wirkt exakt wie vorhergesagt: der user-unabhängige
Ganzbaum-Floor der Rollen-Up-Rekursion ist eliminiert.

### 2. End-to-end `GetDownwardsRoleTreeProc` (via `INSERT … EXEC`) — **gemischt**

| Viewpoint | Subtree | PRE109 | PRE111 |
|---|---|---|---|
| Leaf `T4999` | 1 | 601 ms | **~440 ms** ✓ |
| Mid `T50`    | 91 | 609 ms | **~535 ms** ✓ |
| Top `T2`     | 820 | 656 ms | **~1340 ms** ✗ |
| Root `T1`    | 5000 | 710 ms | **~5950 ms** ✗ (stabil über 2 Läufe) |

Für flache Viewpoints besser, für große Subtrees **schlechter**. Per-Statement-Breakdown für Root
(standalone, nicht via INSERT EXEC): Funktion-INSERT 149 ms · rawtree-INSERT 154 ms · finaler SELECT
557 ms = **~860 ms** — also **nicht** intrinsisch 8× langsamer. Die ~5950 ms entstehen erst im
`INSERT … EXEC`-Pfad: der durch den Anker geänderte Funktions-**Estimate** kippt dort den Plan des
**finalen SELECT** des Procs, dessen `r`-CTE (Proc-Body, ~Zeile 373) PRE111 **nicht** geankert hat und der
weiterhin über **alle** SecurityRoles läuft — kombiniert mit den Table-Variable-1-Row-Schätzungen ⇒
plan-instabil bei großem Subtree.

### Fazit / Restarbeit Toolkit

- **Kernbottleneck behoben:** die Rollen-Up-Auflösung ist ~60× schneller. Überall, wo „von einem
  Viewpoint aufwärts die effektive Rolle" gebraucht wird, zahlt sich das direkt aus. 👍
- **Offen:** `GetDownwardsRoleTreeProc` trägt noch ein **un-geankertes** Ganzbaum-`r`-CTE im finalen SELECT
  (Proc-Body) + Table-Variable-Estimates → große Subtrees plan-instabil (im INSERT-EXEC-Pfad Regression).
  Empfehlung: denselben Anker-Ansatz auf das interne `r`-CTE anwenden und/oder Table-Variables durch
  `#temp` (mit Statistik) oder `OPTION (RECOMPILE)` ersetzen.
- **Für den Lazy-Tree bleibt die non-rekursive Ein-Ebenen-Primitive der richtige Weg** (µs, s.o.) — der
  Proc liefert weiterhin den ganzen Subtree je Aufruf, unabhängig vom Anker-Fix.

Zusätzliche Scratch-Skripte: `plancheck_proc_setup_pre111.sql` (Funktion auf PRE111-DDL),
`plancheck_proc_breakdown.sql` (Per-Statement). MLM-Migration: `RecreateToolkitViews`
(`ConfigureViews`, plus vorgeschaltetes `DROP VIEW IF EXISTS [dbo].[TenantAccessTree]` — **Hinweis an
Toolkit:** `ConfigureViews` droppt `TenantAccessTree` nicht, nur `…Up`/`…Down`, daher SQL 2714 beim
Re-Run auf bestehender DB; ggf. den Drop in `ConfigureViews` ergänzen).

---

## Reaktion Toolkit-Session (2026-07-06) — ConfigureViews-Fix + AP3-Plan

**(a) `ConfigureViews`-Bug behoben.** `Drop View if exists [dbo].[TenantAccessTree]` als **erster** Drop in
`ConfigureViews` ergänzt (es hängt an `…Up`/`…Down`/`UpwardsTenantTree`). Redeploy ist damit idempotent — das
vorgeschaltete Hand-`DROP` in `RecreateToolkitViews` ist ab nächstem Bump nicht mehr nötig. Build grün.

**(b) AP3 = Regression von `GetDownwardsRoleTreeProc` bei großen Subtrees beseitigen.** Der Anker-Fix (AP2) ist
für die isolierte Upward-Auflösung ein klarer Gewinn, **regressiert aber** die Downward-Proc bei großen Subtrees
im `INSERT…EXEC`-Pfad (Plan-Kaskade). AP3 ist damit **nicht optional** — sonst ist AP2 netto gemischt. Zwei
korrektheits-erhaltende Hebel (beide brauchen MLM-Messung, wie AP2):

1. **Proc-Body-`r`-CTE ankern — UMGESETZT (Build grün).** In `GetDownwardsRoleTreeProc` (`SqlColumnsSyntaxHelper.cs`
   Z. 377) **und** `GetDownwardsRoleTreeByVpIdProc` (Z. 531) der Anker
   `... inner join Tenants st on st.TenantId = s.TenantId` um `where s.TenantId in (select ChildTenantId from @rawtree)`
   ergänzt. **Gleicher Beweis wie AP2**: `r.TenantId` schleifen-invariant, der finale SELECT joint
   `r.TenantId = t.OutermostLeafTenantId` und `@rawtree d.ChildTenantId = OutermostLeafTenantId` →
   `r.TenantId ∈ @rawtree.ChildTenantId` überlebt ohnehin. **Ehrliche Einschränkung:** `@rawtree.ChildTenantId` ist
   die **Nachfahren-Menge** des Viewpoints — für kleine/mittlere Subtrees prunt das stark, für den **Root**
   (≈ ganzer Baum, genau der regressierte Fall) ist die Menge ≈ alle Tenants → hier hilft #1 wenig. Es ist
   korrektheits-sicher und gibt dem Optimizer die Chance zu prunen (via 1-Row-Estimate von `@rawtree` evtl. sogar
   günstig), aber die **Root-Regression schließt es voraussichtlich nicht allein** — das ist #2.
2. **Table-Variable-Estimate-Kaskade stabilisieren — OFFEN (MLM-Mess-Entscheidung, Option A).** `@rawtree`/
   `@resultingUpTree`/`@completeUpTree` → `#temp` (bekommen Statistik) **oder** `OPTION (RECOMPILE)` am finalen
   SELECT. Korrektheits-neutral; behebt die eigentliche Plan-Instabilität (1-Row-Schätzung der Table-Vars). Welche
   Variante (bzw. ob nach #1 überhaupt nötig) → MLMs Harness.

**Deploy:** kein neuer Migrations-Eintrag nötig — dieselbe Migration `20260706120000_RedeployAnchoredUpwardsRoleTree`
ruft `ConfigureViews` und legt damit **alle** Objekte (AP2 + AP3-#1 + `TenantAccessTree`-Drop-Fix) neu an.

**Vorbehalt (aus AP2 gelernt):** #1 ist korrektheits-sicher, aber **plan-sensitiv** — ein „beweisbar äquivalenter"
Change kann den Plan kippen. Ob #1 die Regression mildert/schließt oder ob #2 nötig ist, entscheidet nur MLMs
Messung (Vorher/Nachher `GetDownwardsRoleTreeProc` je Viewpoint-Größe).

---

## Perf-Verifikation PRE112 (MLM-Session, 2026-07-06) — Regression war ein Messartefakt

Auf PRE112 gebumpt, Views/Procs via Migration `RecreateToolkitViewsPre112` (`ConfigureViews`) redeployed
(der `TenantAccessTree`-Drop-Fix in `ConfigureViews` ist da — `database update` lief ohne SQL 2714). PRE112
enthält den Anker aufs interne Proc-`r`-CTE (`where s.TenantId in (select ChildTenantId from @rawtree)`).

**Wichtigste Korrektur zuerst:** Die in der PRE111-Verifikation gemeldete „Regression bei großen Subtrees"
(Root 710→5950 ms) **war ein Cross-Session-Messartefakt.** Ein sauberer **apples-to-apples**-Vergleich
(PRE109-Proc vs. PRE112-Proc in **derselben** DB/Session, gleicher Harness, 5000-Tenant-Baum) zeigt:

| Viewpoint | Subtree | PRE109-Proc | PRE112-Proc |
|---|---|---|---|
| Leaf `T4999` | 1 | 612 ms | **253 ms** (2,4×) |
| Deep `T4000` | 1 | 611 ms | **247 ms** |
| Mid `T50`    | 91 | 710 ms | **359 ms** (2,0×) |
| Top `T2`     | 820 | 1524 ms | **1183 ms** |
| Root `T1`    | 5000 | 6213 ms | **5969 ms** |

**PRE112 ist durchgehend schneller — keine Regression.** Die frühere PRE109-Zahl „Root 710 ms" war ein
**Plan-Glücksfall** aus einer separaten Session; im fairen Vergleich liegt PRE109-Root ebenfalls bei ~6,2 s.
Ursache-Klärung dazu (finaler SELECT isoliert via `SELECT … INTO`, nicht `INSERT…EXEC`): für Root sind
PRE109- und PRE112-`r`-CTE praktisch gleich teuer (5688 vs 5684 ms), weil `@rawtree` beim Root **alle**
Tenants enthält → der `IN (@rawtree)`-Anker ist dort ein **No-op**. Der ~6-s-Root-Kostenpunkt ist also
**inhärent** (das Auflösen der Rollen für den ganzen Baum), nicht durch AP2/AP3 verursacht — und AP3-#1 kann
ihn per Konstruktion nicht senken. Für alle **flacheren** Viewpoints (der reale Lazy-Expand-Fall) greift der
Anker und halbiert die Zeit.

**Fazit:**
- **AP2 (Upward-Funktionen anker-parametrisiert): großer, robuster Gewinn** — isolierte
  `GetUpwardsRoleTreeForId` unverändert ~60× schneller (170→2,8 ms). ✓
- **AP3-#1 (internes Proc-`r`-CTE an `@rawtree`): Netto-Gewinn ohne Regression** — flache/mittlere Viewpoints
  ~2× schneller; Root unverändert inhärent teuer (Anker dort No-op). **#2 (Table-Variables → `#temp`/RECOMPILE)
  ist NICHT nötig**, um eine Regression zu schließen (es gab keine) — bliebe aber eine Option, falls der
  Root-Fall (ganzer Subtree auf einmal) je real gebraucht wird und man die Plan-Varianz dämpfen will.
- **`TenantAccessTree`-Drop-Fix in `ConfigureViews` bestätigt** — Re-Run auf bestehender DB ohne SQL 2714.

Lehre für die Messmethodik (für künftige Perf-Checks): PRE109/PRE111/PRE112 **immer in derselben Session
gegeneinander** messen — diese rekursiven-CTE-+-Table-Variable-Queries haben plan-instabile Absolutzeiten,
Cross-Session-Vergleiche täuschen Regressionen/Gewinne vor. Neue Skripte: `plancheck_proc_setup_pre112.sql`,
`plancheck_fair.sql` (finaler SELECT via `SELECT … INTO`).

---

## Abschluss-Bewertung Toolkit-Session (2026-07-06): Perf-Arbeit fertig — nicht weitergraben

Bewertung nach der PRE112-Klärung: **Wir haben das lohnende Maximum rausgeholt. Kein weiteres Perf-Graben.**

- **Gebankt:** AP2 (~60× auf die isolierte Upward-Auflösung) + AP3-#1 (~2× auf flache/mittlere Viewpoints =
  der reale Lazy-Expand-Fall) + ConfigureViews-Drop-Fix. **Keine Regression** (die PRE111-Zahl war ein
  Cross-Session-Artefakt).
- **Verbleibender teurer Fall = Root-Viewpoint (~6 s) ist INHÄRENT** (Rollen-Auflösung für den ganzen Baum,
  keine Viewpoint-Einschränkung → Anker per Konstruktion No-op). Kein Hebel senkt die Zeit; **#2 (Table-Vars →
  `#temp`/`OPTION(RECOMPILE)`) dämpft nur Plan-Varianz, nicht die Kosten → bewusst NICHT umgesetzt.**
- **Out-of-path:** Der Lazy-Switcher (AP1, non-rekursiv, µs) ruft die Downward-Proc mit Root-Subtree gar nicht.
  Der ~6-s-Fall trifft nur „ganzer Subtree ab Wurzel auf einmal" — genau das „lade-alles"-Muster, das die Lazy-
  Übung ablöst. Ihn strukturell zu beschleunigen = Bulk-Auflösung algorithmisch neu bauen → großer Umbau, geringer
  ROI. **Nur ziehen, falls je ein realer Konsument Bulk-Whole-Tree schnell braucht.**

**Nächste Schritte (nicht mehr Perf):** commit + Paket-Bump; MLM: AP1-Host-Test (EF-Übersetzung/Semantik/Plan der
Ein-Ebenen-Primitive) + Switcher-Umbau auf `MudTreeView`+`ServerData`. Offene, unabhängige Baustelle:
net10-Design-Time (`IdentityPasskeyData requires PK`), die `dotnet ef` blockiert.
