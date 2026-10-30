# Schema-Support 10: `SchemaAwareMigrationsSqlGenerator` – Schema beim Anwenden der Migrationen einsetzen

**Typ:** Enhancement
**Betrifft:** neu in `ITVComponents.EFRepo.SqlServer` (z.B. `Migrations\SchemaAwareMigrationsSqlGenerator.cs`
plus Options-Extension)

## Ausgangslage

Migrationen werden bewusst **ohne Schema** generiert (Issue 01), damit dieselbe Migration für `dbo` und für
ein pro Installation konfiguriertes Schema gilt. `dotnet ef database update` / `migrations script` laufen über
die Design-Time-Factory – nicht über WebPart- oder Plugin-Init. Ohne weitere Massnahme
- erzeugt EF unqualifiziertes DDL, das im `DEFAULT_SCHEMA` des Migrations-Logins landet (bei sysadmin immer `dbo`),
- landet `__EFMigrationsHistory` im Default-Schema des Logins,
- bleibt der Platzhalter aus `ConfigureViews` (Issue 01/03) unaufgelöst.

## Vorschlag

### API für die Design-Time-Factory

```csharp
optionsBuilder.UseSqlServer(connectionString)
              .UseSchemaAwareMigrations(configuredSchema);   // null/leer => dbo, Verhalten wie bisher
```

`UseSchemaAwareMigrations`:
- registriert eine `IDbContextOptionsExtension`, die das Schema trägt
  (in `GetServiceProviderHashCode`/`ShouldUseSameServiceProvider` berücksichtigen),
- ersetzt `IMigrationsSqlGenerator` durch `SchemaAwareMigrationsSqlGenerator`,
- setzt `MigrationsHistoryTable("__EFMigrationsHistory", schema)`.
- Unterstützt optional Verweise `->Pfad` analog `CustomSchemaOptionsLoader`; fehlender Wert → Log-Warnung.

### Generator

`SchemaAwareMigrationsSqlGenerator : SqlServerMigrationsSqlGenerator`, Override von
`Generate(IReadOnlyList<MigrationOperation> operations, IModel? model, MigrationsSqlGenerationOptions options)`:

1. Für jede Operation mit `Schema == null` das konfigurierte Schema setzen (u.a. `CreateTableOperation`,
   `DropTableOperation`, `AddColumnOperation`, `AlterColumnOperation`, `CreateIndexOperation`,
   `AddForeignKeyOperation`, `AddPrimaryKeyOperation`, `RenameTable/Column/Index`, Sequence-Operationen …).
   Am einfachsten generisch über `ITableMigrationOperation` bzw. die `Schema`-Property.
2. `PrincipalSchema == null` bei `AddForeignKeyOperation` und bei den `ForeignKeys` innerhalb von
   `CreateTableOperation` ebenfalls setzen.
3. `RenameTableOperation.NewSchema` **nicht** anfassen (null = Schema bleibt).
4. `SqlOperation.Sql`: `MigrationSchema.Placeholder` (Issue 01) durch das Schema ersetzen.
5. Ohne konfiguriertes Schema: Platzhalter durch `dbo` ersetzen, sonst nichts ändern.
6. Hat eine Operation bereits ein **anderes** Schema als das konfigurierte (Literal in einer Migration),
   Warnung loggen – deutet auf eine versehentlich mit Schema generierte Migration hin.

Das Schema wird **nicht** angelegt (`EnsureSchema`); das übernimmt der DBA (der Migrations-User hat
typischerweise kein `CREATE SCHEMA`). Optional per Flag `ensureSchema: true` ein
`IF SCHEMA_ID(N'x') IS NULL EXEC(N'CREATE SCHEMA [x]')` voranstellen.

Hinweis: Der `model`-Parameter von `Generate` ist das Zielmodell der Migration (ohne Schema). Lookups des
Basis-Generators über `model.GetRelationalModel().FindTable(name, schema)` liefern mit gesetztem Schema
`null`; für Standard-Tabellen unkritisch, für Spezialfeatures (temporal, memory-optimized) zu prüfen.

## Akzeptanzkriterien
- `dotnet ef migrations script` mit `UseSchemaAwareMigrations("partner")` enthält ausschliesslich
  `[partner].`-qualifizierte Objekte, keinen Platzhalter, und die History-Tabelle `[partner].[__EFMigrationsHistory]`.
- Ohne Schema ist das generierte Skript identisch zum bisherigen (bis auf aufgelöste Platzhalter → `[dbo]`).
- `migrations add` bleibt unbeeinflusst (der Generator wirkt nur auf SQL-Generierung, nicht auf C#-Code).
