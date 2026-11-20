# Issue: `ChildTenantsWith` wirft im Baum-Kontext – die Prozeduren liefern `TenantNameLower` nicht

**Status:** offen
**Datum:** 2026-11-20
**Quelle:** ESAG-PartnerPortal (Konsument), Port von 4.0.0-PRE314 auf 5.0.0-PRE278, Laufzeittest im Schema `n10`.
**Toolkit-Stand:** `5.0.0-PRE278` (Branch `Future_10`). Eingeführt mit `5d528ac46` (2026-09-16).
**Betrifft:** CoreIdentityTree, SQL Server **und** PostgreSQL – jeder Aufruf von `ChildTenantsWith(...)`.

## Der Fall

Im ESAG-Portal ruft der Fachkontext `Egro360DataContext.GetSubTenantsWith(...)` auf dem Security-Kontext
`ChildTenantsWith(userId, currentTenant, requiredPermissions)` auf (z.B. beim Lesen der Rechnungsliste,
`SalesInvoiceController.Read`). Das wirft:

```
System.InvalidOperationException: The required column 'TenantNameLower' was not present in the results of a 'FromSql' operation.
   at Microsoft.EntityFrameworkCore.Query.Internal.FromSqlQueryingEnumerable`1.BuildIndexMap(...)
   ...
   at ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.SqlServer.SyntaxHelper.SqlColumnsSyntaxHelper.<>c.<ConfigureChildTenantPermissionMethod>b__9_0(DbContext c, String userId, String currentTenant, String[] requiredPermissions)
   at ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.AspNetTreeSecurityContext`1.ChildTenantsWith(String userId, String currentTenant, String[] requiredPermissions)
```

Die Datenbank ist mit der aktuellen Fassung aufgebaut (Migrationen inkl. `SqlColumnsSyntaxHelper.ConfigureViews`),
die Prozeduren sind also die von PRE278 – kein veralteter Stand in der Datenbank.

## Die Ursache

`5d528ac46` („Mandantenname klein: berechnete Spalte TenantNameLower statt lower() je Abfrage“) hat
`HierarchyTenant.TenantNameLower` als berechnete Spalte eingeführt:

```csharp
// ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.SqlServer/CoreIdentityTree/SyntaxHelper/SqlColumnsSyntaxHelper.cs (~42)
builderOptions.ConfigureComputedColumn<HierarchyTenant, string>(t => t.TenantNameLower,
    "lower(TenantName) persisted");
```

`ChildTenantsWith` liest das Ergebnis der Prozeduren per `FromSqlRaw` direkt als `HierarchyTenant`
(`ConfigureChildTenantPermissionMethod`, ~Zeilen 206–255). EF verlangt bei `FromSql` **jede** gemappte Spalte
im Resultat, auch berechnete. Die Prozeduren wurden beim Einführen der Spalte aber nicht nachgezogen – sie
liefern genau die Spalten von vorher (`Culture` ist mit Leitfaden §106 dazugekommen, `TenantNameLower` nicht):

**SQL Server** – `SqlColumnsSyntaxHelper.cs`, `GetChildTenantsWithPermsProc` (~521/550) und
`GetChildTenantsWithPermsByVpIdProc` (~678/707):

```sql
select t.TenantId, t.ParentTenantId, t.TenantName, t.DisplayName, null TenantPassword, t.TimeZone, t.Culture, t.TenantTypeId, t.TenantDirty
from @perm2T r
inner join [{{schema}}].[Tenants] t on t.TenantId = r.tenantId
group by t.TenantId, t.ParentTenantId, t.TenantName, t.DisplayName, t.TimeZone, t.Culture, t.TenantTypeId, t.TenantDirty
```

**PostgreSQL** – `PostgreSqlColumnsSyntaxHelper.cs`, `ChildTenantsWithPermsFunction` (~900–940): dieselbe
Spaltenliste in `RETURNS TABLE(...)`, im `SELECT` und im `GROUP BY`; der Aufruf (~Zeilen 256–280) liest per
`select * from ...` ebenfalls als `HierarchyTenant`.

Die Tests fallen nicht auf, weil die Test-Kontexte `TenantNameLower` mit `ValueGeneratedNever()` als normale
Spalte führen und die Prozeduren nicht ausführen.

## Die Wirkung

Jede Installation mit Baum-Mandanten (CoreIdentityTree), die `ChildTenantsWith` aufruft – direkt oder über
einen Fachkontext, der Unter-Mandanten mit Rechten bestimmt –, bricht dort mit der Exception ab. Betroffen sind
beide Provider und beide Überladungen (ViewPoint per Name und per Id, Benutzer per Id und per Labels).

## Vorschlag

1. In beiden SQL-Server-Prozeduren `t.TenantNameLower` in die Select-Liste **und** ins `group by` aufnehmen.
2. In der PostgreSQL-Funktion `"TenantNameLower" text` in `RETURNS TABLE(...)` aufnehmen und `t."TenantNameLower"`
   im `SELECT` und im `GROUP BY` ergänzen.
3. Robuster für künftige Spalten: die Prozeduren nicht mehr als `HierarchyTenant` mappen, sondern nur die
   `TenantId`s liefern lassen und die Mandanten danach per LINQ aus `Tenants` laden – dann zieht jede neue
   Spalte (berechnet oder nicht) automatisch mit, und eine vergessene Prozedur kann das Mapping nicht mehr
   brechen. Alternativ zumindest ein Test, der die Prozedur gegen eine echte Datenbank ausführt und das
   Ergebnis auf das aktuelle Mapping legt.
4. Leitfaden: Hinweis, dass Konsumenten nach dem Update eine Migration mit `ConfigureViews(migrationBuilder)`
   brauchen, damit die korrigierten Prozeduren ausgerollt werden (wie §14/§40/§106).

## Umgehung (ESAG, Schema `n10`)

Bis zum Fix die beiden Prozeduren in der Datenbank ergänzen (liest die bestehende Definition und schreibt sie
per `ALTER` zurück; wird beim nächsten `ConfigureViews` wieder durch die Library-Fassung ersetzt):

```sql
DECLARE @p sysname, @def nvarchar(max);
DECLARE c CURSOR LOCAL FOR SELECT name FROM sys.procedures
    WHERE schema_id = SCHEMA_ID(N'n10') AND name IN (N'GetChildTenantsWithPermsProc', N'GetChildTenantsWithPermsByVpIdProc');
OPEN c; FETCH NEXT FROM c INTO @p;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @def = OBJECT_DEFINITION(OBJECT_ID(N'n10.' + QUOTENAME(@p)));
    IF @def NOT LIKE N'%TenantNameLower%'
    BEGIN
        SET @def = REPLACE(@def, N't.TenantTypeId, t.TenantDirty', N't.TenantTypeId, t.TenantDirty, t.TenantNameLower');
        SET @def = STUFF(@def, PATINDEX(N'%CREATE%', @def), 6, N'ALTER');
        EXEC (@def);
    END
    FETCH NEXT FROM c INTO @p;
END
CLOSE c; DEALLOCATE c;
```

## Verwandte Befunde aus demselben Port (je eigenes Issue sinnvoll)

- `ConfigureViews` schreibt Views/Funktionen/Prozeduren als nacktes `migrationBuilder.Sql(...)`. Mit
  `dotnet ef database update` geht das; in **jedem** per `migrations script` erzeugten Skript (idempotent oder
  nicht) stehen `CREATE VIEW/FUNCTION/PROCEDURE` mitten im Batch und scheitern. Installationen, die per Skript
  ausrollen (DBA), können die Baum-Objekte so nicht anlegen.
- `ConfigureViews` emittiert immer den SQL-Stand der **referenzierten** Library-Version. Ruft eine ältere
  Migration (z.B. eine Baseline) es auf, scheitert ein frischer Aufbau, sobald die Objekte Spalten verwenden,
  die erst spätere Migrationen anlegen (hier `Tenants.Culture`). Hinweis im Leitfaden: nur die jeweils
  neueste Migration ruft `ConfigureViews` auf.
- Commit `8baa9b00d` („CallingPlugin: volle Aufrufkette“) ändert für Einträge einer Pre-/PostInit-Sequenz, wer
  `CallingPlugin` ist (vorher der Anforderer, jetzt der Sequenz-Besitzer). Bestehende Plugin-Konfigurationen mit
  `CallingPlugin.PluginType` / `[CallingPlugin.UniqueName]` in solchen Ketten brechen (falsches Typ-Argument,
  `~>`-Referenz „not found“). Der Leitfaden erwähnt das nicht; ESAG-Umstellung: Typ-Argumente auf
  `CallingPlugin["TContext"]`, `~>`-Referenzen in den Konstruktoren auf `[CallingPlugin.PrevPlugin(1).UniqueName]`.
- `DbPluginsSelector.GetAutoLoadPlugins()` (TreeShared) greift auf `securityContext.CurrentTenantId.Value` zu,
  sobald ein Benutzer angemeldet ist. Ist kein Mandant aufgelöst (z.B. weil `TrustedFullAccessComponents` noch
  4.x-Typnamen tragen), kommt nur „Nullable object must have a value“ statt eines Hinweises auf die Ursache.
- Paket-Konsolidierung: `TrustedFullAccessComponents`, `WebPlugins.Constructor` und `Localizations.Identifier`
  enthalten assembly-qualifizierte 4.x-Typnamen (Namespaces, Assemblies, `Version=4.0.0.0`, teils andere
  Generic-Stelligkeit, z.B. `DbSecurityRepository`43/46` → `45`, `DbPluginsSelector`8` → `11`). Der
  Packaging-Leitfaden nennt nur Code und WebPart-Config; ein Abschnitt (oder ein Prüf-Skript) für diese
  Datenbank-Inhalte fehlt. `DbPluginFormatter` nimmt ausserdem statt des Kontexts eine `IToolkitContextFactory`
  (Plugin-Konstruktor: eigene Abhängigkeit, z.B. `$sysFactory`).
