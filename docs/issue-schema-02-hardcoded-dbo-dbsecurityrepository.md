# Schema-Support 02: Hart codiertes `[dbo]` in `DbSecurityRepository`

**Typ:** Bug (bricht bei Nicht-`dbo`-Schema)
**Betrifft:** `ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared\Security\DbSecurityRepository.cs:1227`

## Problem

```csharp
var closest = ((DbContext)securityContext).Set<UpwardsRoleUserView<TUserId>>().FromSqlInterpolated(
    $@"SELECT ...
    FROM [dbo].[GetUpwardsRoleTreeForLabels]({labelsJson}, {forTenant})
    ...");
```

Liegt die Funktion in einem anderen Schema (z.B. `partner`), schlägt die Abfrage fehl. Existiert zusätzlich
eine gleichnamige Funktion in `dbo` (geteilte Kunden-DB), wird still die falsche verwendet.

## Vorschlag

Schema aus dem Modell ableiten (Issue 01). Da Bezeichner in `FromSqlInterpolated` nicht parametrisiert werden
können, den Objektnamen vorab bauen und als Literal einsetzen, z.B. mit `FormattableStringFactory.Create`
oder über `FromSqlRaw` mit Parametern:

```csharp
var fn = $"[{((DbContext)securityContext).GetDbSchema()}].[GetUpwardsRoleTreeForLabels]";
... .FromSqlRaw($@"SELECT ... FROM {fn}({{0}}, {{1}}) WHERE [OutermostLeafTenantId] = {{2}} ...",
    labelsJson, forTenant, currentTenant);
```

Alternative: die Abfrage über die bereits registrierte DbFunction `GetUpwardsRoleTreeForLabels` in LINQ
formulieren; dann setzt EF das Schema selbst (setzt Issue 06 voraus).

## Akzeptanzkriterien
- Kein `[dbo]`-Literal mehr in Laufzeit-SQL.
- Abfrage liefert mit `HasDefaultSchema("partner")` und ohne Default-Schema dieselben Ergebnisse wie bisher.
