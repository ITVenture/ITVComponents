# Schema-Support 06: DbFunctions ohne `HasSchema`

**Typ:** Risiko (muss verifiziert werden)
**Betrifft:**
- `ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.SqlServer\SyntaxHelper\SqlColumnsSyntaxHelper.cs:77-84`
  (`GetUpwardsRoleTreeForId`, `GetUpwardsRoleTreeForLabels`, `…ByLeafId`)
- `ITVComponents.EFRepo\Options\DbContextModelBuilderOptions.cs:91-96`, `ITVComponents.EFRepo\DbContextConfig\Impl\DbFunctionConfigurator.cs`

## Problem

```csharp
builderOptions.ConfigureDbFunction("GetUpwardsRoleTreeForLabels", m => m.HasName("GetUpwardsRoleTreeForLabels"));
```

Es wird kein Schema gesetzt. Die SQL-Server-Konvention von EF Core verwendet für Funktionen ohne Schema
`dbo` als Fallback.

Das Default-Schema wird jetzt über den `SchemaRedirect`-Konfigurator gesetzt
(`ActivationOptions.CustomSchema` bzw. `CustomSchemaOptionsLoader`). Konfiguratoren laufen in der
Reihenfolge ihrer Registrierung (`DbContextModelBuilderOptions.ConfigureModelBuilder`); die
`DbFunctionConfigurator`s können also **vor** dem `SchemaRedirect` laufen. Für Tabellen/Views ist das egal
(EF löst `GetSchema()`/`GetViewSchema()` erst beim Lesen gegen das Model-Default-Schema auf). Ob das für
DbFunctions ebenso gilt oder die Konvention beim Hinzufügen fest `dbo` setzt, ist **zu verifizieren**.

## Vorschlag
- Verifizieren: Kontext mit `CustomSchema = "partner"` erzeugen,
  `context.Model.FindDbFunction(...)!.Schema` prüfen.
- Falls `dbo`: `SchemaRedirect.ConfigureEntity` setzt zusätzlich auf allen DbFunctions ohne explizit
  konfiguriertes Schema das Ziel-Schema, z.B.
  `foreach (var f in modelBuilder.Model.GetDbFunctions()) if (f.GetConfiguredSchema... == null) f.SetSchema(targetSchema);`
  – damit ist die Reihenfolge der Konfiguratoren irrelevant.

## Akzeptanzkriterien
- Test: mit `CustomSchema = "partner"` referenziert das generierte SQL für die DbFunctions `[partner].[…]`,
  unabhängig von der Registrierungsreihenfolge der Konfiguratoren.
