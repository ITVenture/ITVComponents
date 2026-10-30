# Schema-Support 07: `SqlDynamicDataAdapter` / `SqlQuerySyntaxProvider` kennen kein Schema

**Typ:** Bug / Risiko in geteilter Datenbank
**Betrifft:**
- `ITVComponents.EFRepo.SqlServer\SqlQuerySyntaxProvider.cs:399-433` (`FullQualifyColumn`, `FormatTableName`, `ResolveTableName`)
- `ITVComponents.EFRepo.SqlServer\SqlDynamicDataAdapter.cs` – alle Statements (select 53, update 135, insert 214,
  delete 252, copy 303, DDL 458–596), `TableExists` 715, `DescribeTable` 718–751
- `ITVComponents.WebCoreToolkit.EntityFramework\DataSources\Impl\WrappedDynamicDataAdapter.cs:76,131,137,191,196`
- analog `ITVComponents.EFRepo.PostgreSql\PostgreSqlDynamicDataAdapter.cs:711, 733-758`

## Problem
- `FormatTableName` erzeugt `[name]` ohne Schema. Ein übergebenes `partner.X` wird zu `[partner.X]`.
  DML/DDL und FK-`references` landen damit im Default-Schema des DB-Users.
- `TableExists`: `object_ID(@p0)` mit unqualifiziertem Namen → Auflösung über das Default-Schema, sonst `dbo`.
- `DescribeTable`: `INFORMATION_SCHEMA.COLUMNS`/`KEY_COLUMN_USAGE` werden nur nach `TABLE_NAME` gefiltert,
  Joins über `object_name(...)`/`object_id(c.table_name)`. Gleichnamige Tabellen in anderen Schemas werden
  vermischt bzw. doppelt geliefert, FK-Ziele nur über den Namen gematcht.

## Vorschlag
- Schema-Eigenschaft am `SqlQuerySyntaxProvider` (Default: Default-Schema des Kontexts bzw. `dbo`);
  `FormatTableName` liefert `[schema].[name]`.
- Metadaten-Abfragen zusätzlich nach `TABLE_SCHEMA` filtern, `object_id` mit qualifiziertem Namen aufrufen,
  FK-Ziele über Schema + Name matchen.
- PostgreSQL analog (`table_schema`).

## Akzeptanzkriterien
- Dynamische Tabellen werden im konfigurierten Schema angelegt, gelesen und beschrieben.
- Gleichnamige Tabelle in `dbo` beeinflusst `TableExists`/`DescribeTable` nicht.
