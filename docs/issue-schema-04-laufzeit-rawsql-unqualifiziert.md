# Schema-Support 04: Unqualifiziertes Raw SQL zur Laufzeit (`exec …Proc`, `Sequences`, `Systemlog`)

**Typ:** Bug (bricht bei Nicht-`dbo`-Schema, sofern der DB-User nicht `DEFAULT_SCHEMA` = Ziel-Schema hat)
**Betrifft:**

| Datei | Zeile | Statement |
|---|---|---|
| `...AspNetCoreTreeTenants.SqlServer\SyntaxHelper\SqlColumnsSyntaxHelper.cs` | 153, 163 | `exec GetChildTenantsWithPermsProc …` |
| dto. | 175, 185 | `exec GetChildTenantsWithPermsByVpIProc …` (siehe auch Issue 05) |
| dto. | 113–118 | `update Sequences set …` |
| `...AspNetCoreTenants.SqlServer\SyntaxHelper\SqlColumnsSyntaxHelper.cs` | 35–40 | `update Sequences set …` |
| `...TenantSecurityContext.SqlServer\SyntaxHelper\SqlColumnsSyntaxHelper.cs` | 39–44 | `update Sequences set …` |
| `...AspNetCoreTreeTenants\AspNetTreeSecurityContext`1.cs` | 865 | `EXEC [GetDownwardsRoleTreeProc] …` |
| `...TenantSecurityShared\Extensions\ContextExtensions.cs` | 319 | `delete from Systemlog where EventTime < …` |

## Problem

Diese Statements werden vom Client abgesetzt. Unqualifizierte Namen werden über das `DEFAULT_SCHEMA` des
DB-Users aufgelöst, danach über `dbo`. Folgen:
- Ohne passendes `DEFAULT_SCHEMA` (oder bei einem sysadmin-Login, der immer `dbo` verwendet) werden die
  Objekte nicht gefunden.
- In einer geteilten DB mit einer Kundentabelle `dbo.Sequences` oder `dbo.Systemlog` wird **die falsche
  Tabelle verändert** – beim `Systemlog`-Cleanup mit einem `delete`.

## Vorschlag
Objektnamen über `context.GetDbSchema()` (Issue 01) qualifizieren. Da Bezeichner nicht parametrisierbar sind,
den Namen vorab als Literal einsetzen und die Werte weiterhin als Parameter übergeben
(`FromSqlRaw` / `ExecuteSqlRaw` bzw. `FormattableStringFactory.Create`).

`ConfigureMethod`-Lambdas erhalten den `DbContext` bereits als Parameter – das Schema ist dort verfügbar.

## Akzeptanzkriterien
- Alle aufgeführten Statements verwenden `[schema].[Objekt]`.
- Funktionieren mit einem DB-User ohne `DEFAULT_SCHEMA` auf das Ziel-Schema.
