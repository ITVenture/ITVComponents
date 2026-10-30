# Schema-Support 03: `ConfigureViews` – Objekt-Referenzen in Views/Functions/Procs schema-qualifizieren

**Typ:** Bug / Risiko in geteilter Datenbank
**Betrifft:** `ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.SqlServer\SyntaxHelper\SqlColumnsSyntaxHelper.cs`
(`ConfigureViews`, ab Zeile 190) sowie die mitgelieferte Migration
`...AspNetCoreTreeTenants.SqlServer\Migrations\20241125163308_SetViewCode.cs:14`

## Problem

Die **angelegten** Objekte sind korrekt mit `[{schema}]` qualifiziert (DROP/CREATE). In den **Bodies** ist
die Qualifizierung aber uneinheitlich:

- qualifiziert (`{schema}.Tenants`): nur `UpwardsTenantTree`, `DownwardsTenantTree`,
  `GetDownwardsTenantTreeByTopmostId`.
- unqualifiziert: `TenantAccessTreeDown/Up`, `TenantAccessTree` und alle übrigen Functions/Procs
  (ca. Zeilen 268–309, 382–727). Betroffen sind u.a. `Tenants`, `TenantUsers`, `SecurityRoles`,
  `TenantUserRoles`, `RoleRoles`, `Users`, `RolePermissions`, `Permissions`, `GlobalToLocalRoles`,
  `GlobalRolePermissions`, `UpwardsTenantTree`, verschachtelte Aufrufe wie `GetUpwardsRoleTreeForLabels(...)`
  und `exec GetDownwardsRoleTreeProc`.

SQL Server löst unqualifizierte Namen innerhalb eines Moduls zuerst im Schema des Moduls, dann in `dbo` auf.
Solange alle Objekte im Ziel-Schema existieren, funktioniert das. In einer geteilten Kunden-DB mit generischen
Namen (`Users`, `Tenants`, `Permissions`, …) bindet aber jedes fehlende oder umbenannte Objekt still an eine
Kundentabelle in `dbo`.

Zusätzlich:
- Default-Parameter `schema = "dbo"`: ein vergessenes Schema legt alles in `dbo` an und die
  `DROP … IF EXISTS`-Statements können **gleichnamige Kundenobjekte in `dbo` löschen**.
- `SetViewCode` ruft `ConfigureViews(migrationBuilder)` ohne Schema auf.

## Vorschlag
- Alle Tabellen-, View-, Function- und Proc-Referenzen in den Bodies mit `[{schema}].` qualifizieren –
  nur so greift der Platzhalter aus Issue 01 überall.
- Den stillen Default `"dbo"` entfernen; neue Überladung `ConfigureViews(mb)` arbeitet mit
  `MigrationSchema.Placeholder` (Issue 01), der beim Anwenden durch den `SchemaAwareMigrationsSqlGenerator`
  (Issue 10) ersetzt wird.
- `SetViewCode` auf die neue Überladung umstellen.

## Akzeptanzkriterien
- Kein unqualifizierter Objektname mehr in den von `ConfigureViews` erzeugten Modulen.
- Test: DB mit Schema `partner` plus gleichnamigen Dummy-Tabellen in `dbo` → Views/Procs lesen
  ausschliesslich aus `partner`.
- `dotnet ef migrations script` mit konfiguriertem Schema enthält keinen Platzhalter und kein `[dbo]` mehr.
