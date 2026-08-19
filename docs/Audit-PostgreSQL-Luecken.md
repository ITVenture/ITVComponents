# Audit: was einem Umzug von MLM auf PostgreSQL im Weg steht

Stand 2026-08-19, erhoben am Code (nicht am Gedächtnis). Anlass: MLM muss möglicherweise auf PostgreSQL
umziehen; MLM fährt **hierarchische** Mandanten (`CoreIdentityTree`).

## Kurzfassung

Die Provider-Pakete sind paarweise vorhanden (EFRepo, TenantSecurity, Workflow — je SqlServer und
PostgreSql). Das täuscht. **Die hierarchische Ausprägung gibt es für PostgreSQL überhaupt nicht**, und
genau die benutzt MLM. Dazu kommen zwei Stellen, an denen T-SQL fest im providerneutralen Code steht.

| Baustein | SQL Server | PostgreSQL | Bewertung |
|---|---|---|---|
| `EFRepo` | ✔ | ✔ | in Ordnung |
| `Workflow` (Context + Migrationen) | ✔ | ✔ | in Ordnung, gerade erst nachgezogen |
| `TenantSecurity` **Basic** (flach) | ✔ | ✔ | in Ordnung |
| `TenantSecurity` **CoreIdentity** (flach + Identity) | ✔ | ✔ | in Ordnung |
| `TenantSecurity` **CoreIdentityTree** (hierarchisch) | ✔ | **fehlt vollständig** | **der Blocker** |
| Roh-SQL im neutralen Projekt | — | — | **2 harte Stellen** |
| `Plugins.DatabaseDrivenConfiguration` | T-SQL | — | Altpfad, gering |

Leere Ordner ohne `csproj` und ohne Eintrag in der Solution (`AspNetCoreTenants.*`,
`AspNetCoreTreeTenants.SqlServer`, `TenantSecurityContext.*`) sind **keine** Lücken — das sind
`bin`/`obj`-Reste abgeräumter Projekte. Sie sehen im Verzeichnisbaum nur so aus.

---

## 1. Der Blocker: die Baum-Logik ist reines T-SQL

`…TenantSecurity.SqlServer/CoreIdentityTree/SyntaxHelper/SqlColumnsSyntaxHelper.cs` hat **715 Zeilen**.
Zum Vergleich: die flachen Gegenstücke haben 44–50 Zeilen und legen nur berechnete Spalten und eine
Sequenz-Methode an. Für PostgreSQL gibt es im ganzen Repo **kein einziges** Beispiel einer View,
Funktion oder Prozedur — es gibt also auch nichts zum Abschauen.

`ConfigureViews(MigrationBuilder, schema = "dbo")` deployt **17 Datenbank-Objekte**:

**7 Views** — `UpwardsTenantTree`, `DownwardsTenantTree`, `TenantAccessTreeDown`, `TenantAccessTreeUp`,
`TenantAccessTree`, `UpwardsRoleTree`, `DownwardsRoleTree`

**6 Funktionen** (5 davon `RETURNS TABLE`, also Inline-TVFs) — `GetUpwardsRoleTreeForId`,
`GetUpwardsRoleTreeForLabels`, `GetUpwardsRoleTreeForIdByLeafId`, `GetUpwardsRoleTreeForLabelsByLeafId`,
`GetUpwardsRoleTree`, `GetEffectiveTenantUserRoles`

**4 Prozeduren** — `GetDownwardsRoleTreeProc`, `GetChildTenantsWithPermsProc`,
`GetDownwardsRoleTreeByVpIdProc`, `GetChildTenantsWithPermsByVpIdProc`

Davon werden 13 ausserhalb des Helpers aus C# heraus benutzt. Die übrigen vier
(`TenantAccessTreeDown`/`Up`, `GetChildTenantsWithPerms*ByVpIdProc`) haben keinen Aufrufer im Toolkit,
sind aber **bewusst als Schnittstelle für Konsumenten** angelegt und werden in bestimmten Szenarien
benutzt. Sie gehören mitportiert — „kein Treffer im Repo" heisst hier nicht „ungenutzt".

### Was beim Übersetzen konkret weh tut

- **Rekursive CTEs.** 6× `with r as`, dazu `rUp`, `rDown`, `effroles`, `ranked`, `z`. PostgreSQL
  verlangt `WITH RECURSIVE` — T-SQL kommt ohne das Schlüsselwort aus. Das ist mechanisch, aber es
  betrifft jede einzelne Baum-Abfrage.
- **Rekursionsgrenze.** T-SQL bremst per `OPTION (MAXRECURSION n)`. Das gibt es in PostgreSQL nicht;
  eine Zyklensicherung muss in die CTE selbst (Pfad-Array plus `NOT path @> ARRAY[id]`, oder
  `CYCLE ... SET ... USING ...` ab PG 14). **Ohne Ersatz läuft ein Zyklus in der Mandanten-Hierarchie
  endlos**, statt mit einem Fehler abzubrechen — das ist keine Übersetzungs-, sondern eine
  Verhaltensfrage.
- **`CROSS APPLY`** (7×) → `LATERAL`. Bei den Baum-TVFs ist genau das der Kern der Abfrage.
- **Prozeduren, die Ergebnismengen liefern.** `EXEC [GetDownwardsRoleTreeProc] …` funktioniert so nicht:
  PostgreSQL kennt `CALL`, aber eine Prozedur gibt keine Ergebnismenge zurück wie in T-SQL. Die vier
  Prozeduren müssen zu `RETURNS TABLE`-Funktionen werden, und die Aufrufer von `EXEC …` auf
  `SELECT * FROM …(…)`.
- **Bezeichner und Schema.** `[dbo].[X]` → `public."X"`. PostgreSQL faltet unquotierte Namen auf
  Kleinschreibung; EF legt sie in Anführungszeichen und damit gemischt an. Jeder unquotierte Name in
  handgeschriebenem SQL ist ein Fund.
- **Kleinkram mit Breitenwirkung:** `isnull()` → `coalesce()`, `nvarchar` → `text`, JSON-Parameter
  (`labelsJson`) — T-SQL `OPENJSON` vs. PG `jsonb_array_elements`.

Die **berechneten Spalten** (6 Stück) sind dagegen die gute Nachricht: die flache PostgreSQL-Fassung hat
bereits sechs gleichartige, die sich fast unverändert übernehmen lassen.

### Was noch dazugehört

- `CoreIdentityTree` hat **keine PostgreSQL-Migrationen** — das Projekt existiert nicht. Neu anzulegen
  samt Design-Time-Helper (`AspNetTreeSecurityContextDesignTimeHelper`), analog zu den beiden flachen.
- In `CoreIdentityTree/Migrations` liegen zwei Migrationen, die **ausschliesslich** Objekte deployen:
  `SetViewCode` und `RedeployAnchoredUpwardsRoleTree`. Die zweite ist der Anker-Fix mit der
  Performance-Wirkung — sie muss inhaltlich mit übersetzt werden, nicht nur strukturell.

---

## 2. T-SQL im providerneutralen Projekt — zwei harte Stellen

Beide liegen in `ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity`, also dort, wo kein
Provider bekannt ist. Sie brechen auf PostgreSQL **unabhängig davon**, wie gut der Rest portiert ist.

**a) `TreeShared/Security/DbSecurityRepository.cs:1860`**

```csharp
FROM [dbo].[GetUpwardsRoleTreeForLabels]({labelsJson}, {forTenant})
```

Eckige Klammern, `dbo`, dazu `RANK() OVER (…)` (das kann PG). Das ist der Ein-Durchlauf-Pfad aus der
`RESOURCE_SEMAPHORE`-Optimierung, also **heisser Pfad**, nicht Randfall.

**b) `CoreIdentityTree/AspNetTreeSecurityContext\`1.cs:939`**

```csharp
$"EXEC [GetDownwardsRoleTreeProc] {userId}, {userIdIsLabels}, {viewpointTenant}"
```

`EXEC` gibt es in PostgreSQL nicht (siehe oben).

**Empfehlung:** beide Stellen nicht einzeln flicken, sondern hinter den bestehenden
Syntax-Helper-Mechanismus ziehen — dieselbe Trennung, die für berechnete Spalten und Methoden schon
existiert (`ConfigureMethods`, `ConfigureChildTenantPermissionMethod`). Sonst wandert dieselbe Frage beim
nächsten Provider wieder in den neutralen Code.

**Kleiner, aber echter Nebenfund** — `Shared/Extensions/ContextExtensions.cs:329`:

```csharp
context.Database.ExecuteSqlInterpolated($"delete from Systemlog where EventTime < {minLogTime}");
```

Der Bezeichner ist **unquotiert und falsch geschrieben** (`Systemlog` statt `SystemLog`). Auf SQL Server
egal, auf PostgreSQL faltet er zu `systemlog` und die Tabelle wird nicht gefunden. Ein Einzeiler, aber
er schlägt erst zur Laufzeit im Aufräumlauf zu.

---

## 3. Nachrangig

- **`ITVComponents.Plugins.DatabaseDrivenConfiguration`** benutzt `isnull(disabled,0)` und unquotierte
  Tabellennamen über den nativen `DataAccess`-Weg. Referenziert wird das Projekt nur noch von
  `Plugins.EntityFrameworkDrivenConfiguration`, sieht also nach Altpfad aus — vor dem Aufwand klären, ob
  MLM es überhaupt lädt.
- **`ITVComponents.DataAccess`** hat `SqlServer` und `SqLite`, aber kein PostgreSQL. Kein `csproj` im
  Repo referenziert `DataAccess.SqlServer` — nur relevant, wenn MLM den nativen Weg selbst benutzt.

---

## 4. Was die Migrations-Stände NICHT aussagen

Alle Ausprägungen enden bei Dezember 2024 (`RolePermissionTypeFix`), die Zahlen gehen auseinander
(Basic 31/9, CoreIdentity 19/10). **Das ist kein PostgreSQL-Rückstand**, sondern der bekannte Drift: MLM
pflegt seine Migrationen selbst, die Repo-Snapshots des Security-Kontexts laufen mit. Für die Frage
„was fehlt für PostgreSQL" zählen deshalb die **Laufzeit-Objekte** (Abschnitt 1) und der neutrale Code
(Abschnitt 2), nicht die Migrationszahlen.

Einzige Ausnahme: `CoreIdentityTree` hat mit `RedeployAnchoredUpwardsRoleTree` (Juli 2026) eine echte,
junge Migration — weil dort die Objekte selbst gepflegt werden.

---

## 5. Vorschlag für die Reihenfolge

1. **Erst entscheiden, was wegfällt.** Die vier ungenutzten Objekte prüfen. Vier weniger von siebzehn
   ist ein Fünftel weniger Übersetzungsarbeit an der schwierigsten Stelle.
2. **Die zwei neutralen Stellen hinter den Syntax-Helper ziehen** — unabhängig vom Umzug sinnvoll und
   ohne PostgreSQL testbar.
3. `SystemLog`-Einzeiler korrigieren (unabhängig, minimal).
4. **Projekt `TenantSecurity.PostgreSql/CoreIdentityTree` anlegen**, berechnete Spalten aus der flachen
   PG-Fassung übernehmen, dann die Views, dann die Funktionen, zuletzt die Prozeduren-Ersatz-Funktionen.
5. **Zyklensicherung ausdrücklich entwerfen**, nicht nebenbei — `MAXRECURSION` hat heute eine Zusage
   gemacht, die sonst ersatzlos verschwindet.
6. Tests: der Baum-Teil braucht eigene, gegen eine echte PostgreSQL-Instanz. Ein In-Memory- oder
   SQLite-Ersatz beweist hier nichts, weil genau die Provider-Unterschiede der Gegenstand sind.

---

## 6. Aufwandsschätzung

Basis: ein Entwickler, der diese Codebasis kennt. Personentage, Bandbreite statt Punktwert. Die
Annahmen stehen dabei — wo eine davon kippt, kippt die Zahl mit.

### Die gute Nachricht vorweg

**Der Grossteil ist bereits neutral.** Beim Nachsehen hat sich das Bild deutlich entspannt gegenüber
dem ersten Eindruck:

- Die 4 `[DbFunction]`-Deklarationen (`GetUpwardsRoleTreeFor*`) binden EF **über den Namen**. Steht
  in PostgreSQL eine Funktion gleichen Namens mit gleicher Signatur, funktioniert der Aufruf ohne
  **eine Zeile** C#-Änderung.
- Die 7 Views hängen als Entitäten am Namen (`GlobalDbObjectNaming`) — dito.
- Die 4 Prozeduren sind **schon** hinter `ConfigureMethod("ChildTenantsWith", …)` gezogen, mit
  passendem `GetMethod`-Aufruf und einem `InvalidOperationException`, wenn ein Provider sie nicht
  liefert. Das ist genau das Muster, das du für Phase 1 haben willst — es existiert bereits und ist
  erprobt.
- Berechnete Spalten und `SequenceNextVal` ebenso.

Phase 1 ist deshalb **kein Umbau, sondern das Nachziehen von Nachzüglern**.

### Phase 1 — Konsolidierung: alles Datenbankspezifische hinter den Helper

| # | Was | Tage |
|---|---|---|
| 1.1 | `AspNetTreeSecurityContext:939` (`EXEC GetDownwardsRoleTreeProc`) → `ConfigureMethod`. Spiegelt `ChildTenantsWith` eins zu eins. | 0.5 |
| 1.2 | `DbSecurityRepository:1860` (`[dbo].[GetUpwardsRoleTreeForLabels]` + `RANK()`) → `ConfigureMethod`. **Der einzige knifflige Punkt**, siehe unten. | 1.5–3 |
| 1.3 | `ContextExtensions:329` (`delete from Systemlog`) korrigieren bzw. mitziehen | 0.25 |
| 1.4 | `GlobalDbObjectNaming` vervollständigen (hat heute 4 Konstanten, zwei davon auskommentiert), Namen aus dem Helper dorthin ziehen | 0.5 |
| 1.5 | `ConfigureViews(…, schema = "dbo")` — Schema-Vorgabe provider-abhängig machen (`public`) | 0.25 |
| 1.6 | Durchsicht auf weitere Nachzügler + Build/Test grün halten | 0.5–1 |
| | **Summe Phase 1** | **3.5–5.5** |

**Zu 1.2, warum das der teure Punkt ist:** die Stelle liefert heute ein `IQueryable`, das der Aufrufer
anschliessend **weiterkomponiert** (Join auf `TenantUsers`). Ein Delegat, der ein Array zurückgibt,
würde die Komposition brechen und die Abfrage in zwei Datenbankrunden zerlegen — genau das, was die
`RESOURCE_SEMAPHORE`-Optimierung damals beseitigt hat. Der Delegat muss also `IQueryable` liefern, und
das will sauber getypt sein. Wenn sich das nicht elegant fassen lässt, wandert der Join mit in den
Provider-Teil — dann sind es eher 3 Tage.

Phase 1 ist **ohne PostgreSQL testbar**: danach muss alles auf SQL Server unverändert laufen. Das ist
der eigentliche Wert — der Umzug wird dadurch überhaupt erst abschätzbar.

### Phase 2 — PostgreSQL-Umsetzung

Der SQL-Block umfasst **526 Zeilen für 17 Objekte**, im Schnitt ~31 Zeilen; die beiden dicksten sind je
61 Zeilen (`GetDownwardsRoleTreeProc`, `GetDownwardsRoleTreeByVpIdProc`).

| # | Was | Tage |
|---|---|---|
| 2.1 | Projekt `TenantSecurity.PostgreSql/CoreIdentityTree` + Design-Time-Helper + `WebPartInit`-Verdrahtung. Mechanisch, zwei Vorlagen vorhanden. | 1 |
| 2.2 | 6 berechnete Spalten — Beinahe-Kopie aus der flachen PG-Fassung | 0.5 |
| 2.3 | 7 Views (rekursive CTEs → `WITH RECURSIVE`) | 3–4 |
| 2.4 | 6 Funktionen (TVF → `RETURNS TABLE`, `CROSS APPLY` → `LATERAL`) | 3–5 |
| 2.5 | 4 Prozeduren → `RETURNS TABLE`-Funktionen, Delegate aus 1.1/1.2 daraufzeigen | 2–3 |
| 2.6 | **Zyklensicherung als Ersatz für `OPTION (MAXRECURSION)`** — Entwurf + Umsetzung in jeder rekursiven CTE | 1–2 |
| 2.7 | Migrationsprojekt + Initialmigration + Objekt-Deploy-Migrationen | 1 |
| | **Summe Phase 2** | **11.5–16.5** |

### Phase 3 — Absicherung

| # | Was | Tage |
|---|---|---|
| 3.1 | Testprojekt gegen eine **echte** PostgreSQL-Instanz (SQLite/In-Memory beweist hier nichts — die Provider-Unterschiede *sind* der Gegenstand) | 1.5 |
| 3.2 | Gleichheits-Tests: dieselbe Hierarchie auf beiden Providern, Ergebnisse Zeile für Zeile vergleichen | 2–3 |
| 3.3 | Leitfaden-Abschnitt + Deployment-Anleitung | 0.5 |
| | **Summe Phase 3** | **4–5** |

### Gesamt

**19–27 Personentage** für „läuft auf PostgreSQL und liefert nachweislich dieselben Ergebnisse".

### Was die Zahl kippen kann

1. **Performance-Parität ist nicht enthalten.** Die T-SQL-Fassungen sind *getunt* — der
   `RESOURCE_SEMAPHORE`-Fix, der Anker-Fix im Rollen-Baum (~60×/~2×), der Ein-Durchlauf-`RANK()`.
   Diese Arbeit ist auf PostgreSQL **nicht übertragbar**: anderer Planer, andere Statistiken, andere
   Indexstrategie. Eine korrekt übersetzte Abfrage kann dort um Grössenordnungen langsamer sein. Das
   ist keine Übersetzungs-, sondern eine neue Optimierungsaufgabe, und die ist nach oben offen —
   erfahrungsgemäss **+3 bis +10 Tage**, je nachdem wie tief MLMs Hierarchie wirklich ist.
2. ~~Die vier ungenutzten Objekte könnten wegfallen.~~ **Erledigt, negativ:** `TenantAccessTreeDown`/`Up`
   und die beiden `…ByVpIdProc` sind bewusst als Schnittstelle für Konsumenten angelegt und werden in
   bestimmten Szenarien benutzt — sie haben nur keinen Aufrufer *innerhalb* des Toolkits. Sie müssen
   also mitportiert werden. Die erhoffte Einsparung entfällt; die Zahlen oben enthalten sie ohnehin.
3. **Semantische Feinheiten in den CTEs.** Der Anker-Fix legt nahe, dass die Baum-Abfragen nicht
   naiv sind. Wenn beim Übersetzen auffällt, dass eine Zusicherung an T-SQL-Verhalten hängt (Sortier-
   stabilität, `NULL`-Behandlung im Vergleich, implizite Konvertierungen), kostet jeder solche Fund
   einen halben bis ganzen Tag.

### Empfehlung zur Reihenfolge

Phase 1 lohnt sich **unabhängig davon, ob der Umzug kommt**: sie räumt T-SQL aus dem geteilten Code,
ist auf SQL Server verifizierbar und macht Phase 2 erst schätzbar. Ich würde sie machen und danach
entscheiden — nach Phase 1 ist die Unsicherheit in Phase 2 deutlich kleiner, weil dann feststeht,
welche Objekte überhaupt noch gebraucht werden.
