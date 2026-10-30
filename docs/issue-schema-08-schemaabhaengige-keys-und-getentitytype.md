# Schema-Support 08: Schema-abhängige Daten-Keys und `GetEntityType`-NullReference

**Typ:** Bug / Risiko
**Betrifft:**
- `ITVComponents.EFRepo\AutoValues\AutoValueHelper.cs:45`
- `ITVComponents.Decisions.Entities\Helpers\DecisionResultAttribute.cs:76-78`
- `ITVComponents.EFRepo\Extensions\EntityContextExtensions.cs:23`

## Problem

### Keys mit Schema
```csharp
string propertyName = $"[{schema}].[{tableName}].[{prop.Proprety.Name}]";
```
- Ohne Default-Schema ist `schema` leer → Key `[].[Tabelle].[Prop]`.
- Wechselt das Schema (z.B. auf `partner`), ändern sich alle Keys; bestehende Sequence-/`TableDecider`-
  Konfigurationen in der DB passen nicht mehr.

### `GetEntityType`
```csharp
n.GetTableName().Equals(tableName, ...) && n.GetSchema().Equals(schema, ...)
```
wirft eine `NullReferenceException` für view-gemappte oder keyless Entities (`GetTableName()` ist `null`)
und für Modelle ohne Default-Schema (`GetSchema()` ist `null`). `GetViewName`/`GetViewSchema` werden nicht
berücksichtigt.

## Vorschlag
- Für fehlendes Schema einheitlich `dbo` einsetzen (`GetSchema() ?? "dbo"`), damit `[].[…]` nicht entsteht.
- Beim Schemawechsel: Migrationshilfe bzw. Doku, wie bestehende Keys umgeschrieben werden.
- `GetEntityType`: `string.Equals(a, b, StringComparison.OrdinalIgnoreCase)` statt `a.Equals(b)`,
  Schema-Vergleich mit `?? "dbo"` normalisieren, Views über `GetViewName()`/`GetViewSchema()` einbeziehen.

## Akzeptanzkriterien
- Keine NRE bei Views/keyless Entities oder fehlendem Schema.
- Keys sind mit und ohne explizites Default-Schema stabil, solange das effektive Schema gleich bleibt.
