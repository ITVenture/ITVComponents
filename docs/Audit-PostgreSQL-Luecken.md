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

> **Stand 19.08.2026: 2.1–2.6 sind gebaut und gegen eine echte PostgreSQL-Instanz belegt.** Was unten
> als Schätzung steht, ist damit erledigt; die Abweichungen zur Schätzung stehen im Abschnitt
> „Was der Lauf ergeben hat" weiter unten. Offen ist allein **2.7**, und zwar aus einem Grund, der
> nichts mit der Übersetzung zu tun hat — siehe dort.

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

### Was der Lauf ergeben hat

**Es sind 5 Views, nicht 7.** `UpwardsRoleTree` und `DownwardsRoleTree` stehen in der T-SQL-Fassung in
einem `if (false)`-Block, und die zugehörigen Entitäten hängen per `ToView(null)` an gar nichts. Toter
Code, der nicht mitübersetzt wurde — mitzuportieren hiesse, ihn auf einem zweiten Provider am Leben zu
erhalten.

**Die Zyklensicherung (2.6) ist eine Wächter-Funktion, nicht die `CYCLE`-Klausel.** Das war die
inhaltlich wichtigste Entscheidung der Phase. `CYCLE` (ab PG 14) bricht die Rekursion **still** ab und
liefert ein Teilergebnis. Bei einer Rechte-Abfrage ist das die schlechtere Sorte Fehler: jemand
arbeitet mit zu wenig Rechten weiter, ohne dass es auffällt. SQL Server bricht bei 100 Ebenen mit
Fehler ab, und genau diese Zusage wurde nachgebaut — die Funktion wirft (`ERRCODE 54001`) und nennt
zusätzlich das Objekt, in dem der Zyklus auftrat. Nachgewiesen für einen Zyklus in der
Mandanten-Hierarchie *und* für einen Ring aus Rollen-Rollen innerhalb eines Mandanten.

**Die Spaltennamen richten sich nach dem Modell, nicht nach der Vorlage.** Zwei Stellen, an denen die
T-SQL-Fassung anders schreibt als die Modellklasse: `DownwardsUserRoleView.ViewPointTenantId` (grosses
P; die Vorlage schreibt `ViewpointTenantId`) und `UserAccessTree.DirectAssign` (Vorlage:
`directAssign`). Auf SQL Server gleichgültig, auf PostgreSQL der Unterschied zwischen „EF findet die
Spalte" und „EF findet sie nicht".

**Belegt statt behauptet.** Wegwerf-Container `postgres:17`, ein Schema mit den zehn berührten
Tabellen, Hierarchie T1→T2→T3, und ein kleines Programm, das das SQL aus `ConfigureViews` **selbst**
herauszieht — also kein Test gegen eine Abschrift. Ergebnis: alle 30 Anweisungen deployen fehlerfrei,
32 inhaltliche Prüfungen grün (Erwartungswerte von Hand aus der T-SQL-Vorlage hergeleitet), 48
Spaltennamen stimmen exakt. Damit ist ein guter Teil von 3.1 vorweggenommen; es fehlen die
Gleichheitstests gegen SQL Server (3.2).

**2.7 hing an etwas, das gar nichts mit der Übersetzung zu tun hatte.** Für *keinen* Identity-Kontext
der Bibliothek liess sich eine Migration erzeugen — `ModelValidator.ValidateNonNullPrimaryKeys` brach
mit „The entity type 'IdentityPasskeyData' requires a primary key" ab, auf SQL Server genauso. Ursache
war `TableNamesFromProperties` in ITVComponents.EFRepo: die Konvention lief mit `FlattenHierarchy` über
alle DbSet-Eigenschaften und holte damit einen von .NET 10 **ausdrücklich ausgeschlossenen** Typ
zurück ins Modell. Behoben; Einzelheiten und was der Host dazu tun muss stehen in
`Migration-Future_10-MLM-Passkeys.md`.

**Berechnete Spalten brauchen auf PostgreSQL `stored: true`.** Ohne den Schalter baut Npgsql daraus
nicht still etwas anderes, sondern verweigert schon das Erzeugen der Migration: „Virtual (non-stored)
generated columns are only supported on PostgreSQL 18 and up". Auf SQL Server steht das Gegenstück
(`persisted`) im Ausdruck selbst — die Spalten sind dort ebenfalls persistiert, und das ist so
gewollt, weil die Eindeutigkeits-Indizes darauf sitzen. `ConfigureComputedColumn` hat dafür jetzt eine
Überladung mit `stored`. In der Datenbank angekommen sind alle sechs als `GENERATED ALWAYS … STORED`
(`is_generated = ALWAYS`) nachgeprüft.

**2.7 ist damit fertig:** Initialmigration erzeugt (75 Tabellen), als Skript ausgegeben, gegen eine
frische PostgreSQL-Datenbank eingespielt — und die Baum-Objekte anschliessend fehlerfrei auf dieses
echte Schema gelegt, nicht nur auf den Test-Aufbau.

### Phase 3 — Absicherung

| # | Was | Tage |
|---|---|---|
| 3.1 | Testprojekt gegen eine **echte** PostgreSQL-Instanz (SQLite/In-Memory beweist hier nichts — die Provider-Unterschiede *sind* der Gegenstand) | 1.5 |
| 3.2 | Gleichheits-Tests: dieselbe Hierarchie auf beiden Providern, Ergebnisse Zeile für Zeile vergleichen | 2–3 |
| 3.3 | Leitfaden-Abschnitt + Deployment-Anleitung | 0.5 |
| | **Summe Phase 3** | **4–5** |

#### 3.1 und 3.2 sind gelaufen — Ergebnis: 95 Zeilen, kein Unterschied

Aufbau, falls es zu wiederholen ist:

1. Je ein Programm zieht die Anweisungen aus `ConfigureViews` — auf beiden Seiten aus dem **echten
   Code**, nicht aus einer Abschrift. Genau das ist der Punkt: sonst prüft man seine eigene Abschrift.
2. Dieselbe Hierarchie beidseitig, in wortgleichen Fixtures: `T1 → {T2 → {T3, T5}, T4}`, alice an der
   Wurzel, bob in der Mitte.
3. Beide Seiten geben zeilenweise dasselbe Textformat aus (Nullwerte als `-`, `bit`/`boolean` als
   `1`/`0`), dann ein reiner Textvergleich.

Die erste Fassung der Hierarchie war zu brav — sie prüfte die Verrohrung, nicht die Semantik. Erst die
Erweiterung berührt die Stellen, an denen eine Übersetzung tatsächlich abweichen kann:

| Fall | Warum er zählt |
|---|---|
| Rolle innerhalb desselben Mandanten weitergegeben | Erst dadurch läuft die Rekursion in `GetEffectiveTenantUserRoles` überhaupt; vorher lieferte sie eine einzige Zeile und war damit nie geprüft. |
| Berechtigung nur über diese interne Weitergabe erreichbar | Prüft die „diskrete Weitergabe" (siehe `ISSUE-MLM-PermissionSet-CrossTenant-Propagation.md`) — der Mandant erscheint nur, wenn die Kette vollständig trägt. |
| Zwei Wege zu demselben Blatt auf derselben Ebene | Hier steht `RANK` und nicht `ROW_NUMBER`. Mit `ROW_NUMBER` wäre eine der beiden Zeilen verschwunden — und genau das zeigt der Vergleich. |
| Benutzer in der Mitte statt an der Wurzel | Der Baum muss von dort aus rechnen, nicht von oben. |
| Verzweigung statt Kette | Nachbarzweige dürfen sich nicht gegenseitig einsammeln. |

**Eine Beobachtung am Rande, die kein Produktfehler ist:** `GetChildTenantsWithPermsProc` lässt sich
auf SQL Server nicht per `INSERT … EXEC` abgreifen — die Prozedur macht intern selbst ein
`INSERT … EXEC`, und T-SQL verbietet die Schachtelung. Das trifft nur einen Testaufbau, der das
Ergebnis in eine Tabelle schreiben will; EF ruft die Prozedur über `FromSql`, dort tritt es nie auf.
Auf PostgreSQL ist es eine Funktion und damit frei zusammensetzbar — einer der wenigen Punkte, an
denen die PostgreSQL-Fassung mehr kann als das Original.

#### 3.3 — erledigt

`Migration-Future_10-MLM.md` §39 beschreibt den PostgreSQL-Weg für den Mandanten-Baum: was zu
konfigurieren ist, in welcher Reihenfolge eingespielt wird, und die zwei Verhaltensunterschiede, die
eigenen Code betreffen können (Zyklen-Abbruch mit `SQLSTATE 54001` statt Fehler 530; die
Kind-Mandanten-Prozedur ist dort eine Funktion). Ausdrücklich als optional gekennzeichnet — wer bei
SQL Server bleibt, ist nicht betroffen. §38 daneben deckt den Passkey-Bruch ab, der bei der
Konventions-Korrektur mit herausgefallen ist.

**Damit sind Phase 1, 2 und 3 abgeschlossen.** Offen bleibt allein die Performance-Parität, die von
Anfang an nicht in der Schätzung stand.

### Gesamt

**19–27 Personentage** für „läuft auf PostgreSQL und liefert nachweislich dieselben Ergebnisse".

### Was die Zahl kippen kann

1. **Performance-Parität ist nicht enthalten.** *(Inzwischen gemessen — siehe „Phase 4" am Ende:
   die Befürchtung hat sich bestätigt, und zwar an einer anderen Stelle als vermutet.)* Die
   T-SQL-Fassungen sind *getunt* — der
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

## Phase 4 — Performance-Parität: gemessen (21.08.2026)

Der Punkt, der oben als „nach oben offen" stand, ist jetzt keine Schätzung mehr, sondern eine Zahl.

### Der Prüfstand

10 000 Mandanten auf **beiden** Providern, wortgleich aufgebaut und nachgezählt (Mandanten 10 000,
`UpwardsTenantTree` 509 950 Zeilen, 100 Rollen, je 5 000 Rausch-Zeilen bei Plugins, Diagnose-Abfragen
und Kacheln). Die Form ist bewusst zweiteilig: **1..100 eine Kette** — Tiefe 1 bis 100, also exakt an
der Rekursionsgrenze, nicht in ihrer Nähe — und **101..10000 ein breiter Baum**, dessen Eltern reihum
die Kettenglieder 1..99 sind. Damit gibt es Geschwister auf jeder Ebene und die tiefste Ebene ist 100.

Mit den echten Indizes aus der Initialmigration und mit frischen Statistiken auf beiden Seiten
(`ANALYZE` / `sp_updatestats`) — ohne beides wären Zeitmessungen wertlos. Die Datenbankobjekte kommen
wie schon in 3.1/3.2 aus `ConfigureViews` des jeweiligen Providers, also aus dem echten Code.

### Die Zahlen

| # | Messpunkt | Zeilen | PostgreSQL | SQL Server |
|---|-----------|--------|------------|------------|
| M0 | Baum vollständig materialisieren | 509 950 | **286 ms** | 1 950 ms |
| M1 | `CurrentTenantTree` für T100 (Kettenende) | 100 | 280 ms | **< 1 ms** |
| M2 | `CurrentTenantTree` für T199 (breiter Teil) | 100 | 272 ms | **< 1 ms** |
| M3 | Diagnose-Abfragen sichtbar von T199 | 102 | 275 ms | **39 ms** |
| M4 | Kacheln sichtbar von T199 | 102 | 275 ms | **38 ms** |
| M5 | Plugin-Auflösung `DataSource` von T199 | 1 | 786 ms | **2 ms** |
| M6 | Rollen-Baum nach **oben** (T100) | 1 | 296 ms | **3 ms** |
| M7 | Rollen-Baum nach **unten** von der Wurzel | 100 | **4 703 ms** | 5 007 ms |
| M8 | Kind-Mandanten mit Berechtigung, von der Wurzel | 5 | 5 087 ms | **4 839 ms** |
| M9 | dasselbe aus der Mitte (bob/T50) | 2 | **3 647 ms** | 4 818 ms |

Alle Zeilenzahlen stimmen auf beiden Seiten überein — gemessen wurde nachweislich dieselbe Arbeit.

Vorbehalte, damit die Zahlen nicht mehr behaupten als sie tragen: je ein Lauf; PostgreSQL im
Container, SQL Server auf LocalDB, also zwei verschiedene Umgebungen. Ein Faktor 2 ist damit Rauschen.
Ein Faktor 300 nicht.

### Der eine Befund, der alles erklärt

**PostgreSQL schiebt einen Filter nicht in eine rekursive Sicht.** Der Plan von M1 sagt es wörtlich:

```
CTE Scan on r  (actual rows=100)
  Filter: (("OutermostLeafTenantName")::text = 'T100'::text)
  Rows Removed by Filter: 509850     <-- der ganze Baum wird gebaut und dann weggeworfen
  temp written=2363                   <-- ~19 MB in temporäre Dateien, pro Aufruf
```

SQL Server zieht die Bedingung in den **Anker** der Rekursion und läuft eine einzige Kette von 100
Zeilen hoch. PostgreSQL materialisiert erst alle 509 950 Zeilen und filtert danach. Das ist keine
Frage der Definition — die beiden Sichten sind Zeile für Zeile dieselbe Abfrage. Eine rekursive CTE
ist für den PostgreSQL-Planer eine Optimierungsgrenze.

Drei Gegenproben, damit der Befund nicht auf die Umgebung geschoben wird:

- **Es liegt an der Form, nicht an der Maschine.** Dieselbe Frage mit von Hand gefiltertem Anker —
  also das, was eine Funktion mit Parameter tut — kostet auf demselben Container **0,2–1,3 ms** statt
  270 ms. Der Plan wechselt dabei von „Seq Scan + Recursive Union über 509 950 Zeilen" auf „Index Scan
  auf `IX_UniqueTenant` + 100 Nested-Loop-Schritte".
- **Konfiguration heilt es nicht.** Mit `work_mem = 256MB` verschwindet der Überlauf in temporäre
  Dateien, die Zeit bleibt bei 290 ms statt 303 ms. Die Kosten sind die 509 950 Zeilen selbst.
- **PostgreSQLs Rekursion ist nicht langsam — im Gegenteil.** Denselben Baum baut sie in 286 ms, wozu
  SQL Server 1 950 ms braucht. Der Rückstand entsteht ausschliesslich daraus, dass **jede** gefilterte
  Abfrage diesen vollen Aufbau bezahlt.

### Warum das die kritische Stelle ist

`CurrentTenantTree` läuft bei **jedem** gefilterten Zugriff in einem Kind-Mandanten
(`AspNetTreeSecurityContext`). Auf PostgreSQL kostet das bei 10 000 Mandanten 270 ms und rund
19 MB temporäre Dateien — pro Aufruf. Dasselbe trifft die Plugin-Auflösung (M5 berührt die Sicht
zweimal), Diagnose-Abfragen und Kacheln (M3/M4) und die Rollen-Funktionen, die die Sicht intern lesen
(M6: `GetUpwardsRoleTreeForIdByLeafId` liest `UpwardsTenantTree` und filtert erst danach).

Die Kosten hängen an der Grösse des **ganzen** Baums (Summe aller Tiefen), nicht an der Tiefe des
abgefragten Mandanten. Sie wachsen also mit jedem neuen Mandanten — auch für Mandanten, die selbst
flach sitzen.

### Was ausdrücklich *nicht* das Problem ist

M7/M8/M9 sind auf beiden Seiten gleich langsam (3,6–5,1 s). Der Rollen-Baum nach unten von der Wurzel
über 10 000 Mandanten und „Kind-Mandanten mit Berechtigung" kosten auf SQL Server heute genauso viel.
Das ist ein bestehender Zustand und kein Umzugsrisiko — aber einen eigenen Blick wert, falls MLM diese
Wege häufig geht. Der ursprüngliche Verdacht, die Abwärtsrichtung sei das Umzugsrisiko, war damit
falsch: das Risiko liegt in der Aufwärtsrichtung, die vorher unauffällig aussah.

### Der Umbau: die Rekursion bekommt das Blatt als Parameter

Behoben, und zwar **ohne eine einzige Zeile im geteilten Code** — die rund zwanzig Aufrufer der Sicht
bleiben unverändert, ebenso die gesamte SQL-Server-Seite. Die Sicht selbst ist es, die anders gebaut
wird:

- Neu: die Funktion `GetUpwardsTenantTreeByLeafId(p_leaf)` trägt die Rekursion und filtert **im Anker**.
- `UpwardsTenantTree` ist jetzt ein flacher `CROSS JOIN LATERAL` über `Tenants` auf diese Funktion.
  Flache Sichten zieht PostgreSQL in die umgebende Abfrage hinein, der Filter auf
  `OutermostLeafTenantName`/`-Id` landet dadurch direkt am Scan von `Tenants`, und die Funktion läuft
  nur noch für die getroffenen Zeilen.

Es ist derselbe Gedanke wie beim Anker-Fix der T-SQL-Rollenbaum-Funktionen (Migration
`RedeployAnchoredUpwardsRoleTree`), eine Ebene tiefer angesetzt.

| # | Messpunkt | PG vorher | **PG nachher** | SQL Server |
|---|-----------|-----------|----------------|------------|
| M0 | Baum vollständig materialisieren | 286 ms | 739 ms | 1 950 ms |
| M1 | `CurrentTenantTree` T100 | 280 ms | **1,0 ms** | < 1 ms |
| M2 | `CurrentTenantTree` T199 | 272 ms | **1,1 ms** | < 1 ms |
| M3 | Diagnose-Abfragen von T199 | 275 ms | **0,9 ms** | 39 ms |
| M4 | Kacheln von T199 | 275 ms | **0,9 ms** | 38 ms |
| M5 | Plugin-Auflösung `DataSource` | 786 ms | **1,4 ms** | 2 ms |
| M6 | Rollen-Baum nach oben | 296 ms | **4,8 ms** | 3 ms |
| M7 | Rollen-Baum nach unten, Wurzel | 4 703 ms | **1 308 ms** | 5 007 ms |
| M8 | Kind-Mandanten mit Berechtigung | 5 087 ms | **1 613 ms** | 4 839 ms |
| M9 | dasselbe aus der Mitte | 3 647 ms | **1 611 ms** | 4 818 ms |

Zwei Läufe, die Zahlen decken sich. Alle Zeilenzahlen unverändert.

**Der Preis, damit ihn niemand übersieht:** ein Durchlauf **ohne** Filter ruft die Funktion je Mandant
einmal und kostet dadurch mehr als vorher (M0: 739 statt 286 ms). Das ist eine bewusste Wahl — die
gefilterten Zugriffe sind der Normalfall und laufen bei jeder Anfrage in einem Kind-Mandanten, der
volle Durchlauf ist die Ausnahme. Im Toolkit gibt es ihn im aktiven Code nicht: alle Lesestellen
filtern auf `OutermostLeafTenantId`/`-Name` (die beiden ungefilterten Joins in `DbPluginsSelector`
stehen in einem auskommentierten Block). Und selbst mit dem Aufschlag bleibt PostgreSQL dort schneller
als SQL Server (739 ms gegen 1 950 ms).

**Mitgenommen, ohne dass es das Ziel war:** M7/M8/M9 — der Rollen-Baum nach unten und die
Kind-Mandanten mit Berechtigung — sind um den Faktor 3 schneller geworden, weil die dortigen Funktionen
die Sicht intern lesen. Damit liegt PostgreSQL auf diesen Wegen jetzt **vor** SQL Server.

**Nachgewiesen, dass sich sonst nichts geändert hat:**

- Der Gleichheitstest gegen SQL Server aus 3.1/3.2 läuft unverändert durch: 87 vergleichbare Zeilen
  Zeichen für Zeichen identisch, dazu die fünf Kind-Mandanten-Abfragen mit denselben Ergebnissen
  (`CTA` → T1/T3, `CTB` → T2, `CTC` → T2, `CTV` → T1/T3, `CTD` → T4).
- Der Zyklen-Wächter sitzt jetzt in der Funktion statt in der Sicht und meldet sich unverändert: Kette
  der Länge 101 trägt, 102 bricht ab — dieselbe Grenze wie SQL Server. Ein Mandanten-Ring wirft auf
  allen drei Wegen (über die Sicht, über die Funktion, über den gefilterten Zugriff).

**Für Konsumenten auf PostgreSQL:** eine Migration, die `ConfigureViews` erneut ausführt (idempotent,
`DROP … IF EXISTS` + `CREATE`) — sonst bleibt die alte Sicht stehen. Schema unverändert.
