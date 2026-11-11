# Interceptor 01: `SecurityModificationInterceptor` löscht bereits gelöschte Datensätze erneut → `DbUpdateConcurrencyException`

**Typ:** Bug
**Gefunden in:** ESAG PartnerPortal, 4.0.0-PRE313 (Analyse gegen Branch `DEV`, Stand 3af48ffa6)
**Status:** DEV umgesetzt (Vorschlag 1, 3, 5; 4 nur als Leeren der Liste; 2 bewusst nicht, siehe unten). Future_10 folgt mit Tests.
**Betrifft:**
- `ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared\Interceptors\SecurityModificationInterceptor.cs`

## Symptom

Beim Login (externer Login, ESAG `ExternalLoginHandler.SyncUser`) bricht `SaveChanges` mit folgender Exception ab:

```
System.AggregateException: AsyncHelpers.Run method threw an exception.
 ---> Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException: The database operation was expected to affect 1 row(s),
      but actually affected 0 row(s); ...
   at Microsoft.EntityFrameworkCore.DbContext.SaveChanges(Boolean acceptAllChangesOnSuccess)
   at ...SecurityModificationInterceptor`12.SavedChangesAsync(SaveChangesCompletedEventData eventData, Int32 result, ...)
   at ...SecurityModificationInterceptor`12.<>c__DisplayClass6_0.<b__0>d.MoveNext()
   at ITVComponents.Threading.AsyncHelpers...
   at Microsoft.EntityFrameworkCore.Diagnostics.CoreLoggerExtensions.SaveChangesCompleted(...)
   at Microsoft.EntityFrameworkCore.DbContext.SaveChanges(Boolean acceptAllChangesOnSuccess)
   at ESAG_PartnerPortal.Handlers.ExternalLoginHandler.SyncUser(...)
```

Das eigentliche (erste) `SaveChanges` ist bereits committet. Es scheitert nur das Nach-Speichern des Interceptors,
der Aufrufer bekommt aber trotzdem eine Exception (hier: der Login schlägt mit HTTP 500 fehl).

## Ursache

Der Interceptor arbeitet in zwei Phasen:

1. `SavingChangesAsync`: Für gelöschte `TRoleRole`/`TRolePermission`/`TRole`/`TTenantUser`/`TGRoleLRole` lädt
   `LoadCascadeProxies(...)` die abhängigen Datensätze **über den Context** und registriert
   `modifyActions.Add(db => db.Set<T>().RemoveRange(tmp))`.
2. `SavedChangesAsync`: führt die Aktionen aus und ruft `eventData.Context.SaveChanges()` erneut auf (Zeile ~77).

Dabei gibt es zwei Wege, auf denen ein bereits gelöschter Datensatz erneut gelöscht wird:

### a) Aufrufer löscht die Abhängigen selbst im selben `SaveChanges` (konkreter Fall)

Aufrufer-Code (ESAG `ExternalLoginHandler.SyncUser`):

```csharp
securityDb.TenantUserRoles.RemoveRange(tu.Roles);
securityDb.TenantUsers.Remove(tu);
securityDb.SaveChanges();
```

- `SavingChanges` sieht den gelöschten `TTenantUser` → `LoadCascadeProxies(context, tu, ...)` lädt
  `Set<TUserRole>().Where(n => n.TenantUserId == tu.TenantUserId)`. Durch Identity Resolution sind das **dieselben
  Instanzen**, die bereits im Zustand `Deleted` getrackt sind. Registriert wird `RemoveRange(tmp)`.
- Erstes `SaveChanges`: löscht UserRoles + TenantUser. Nach `AcceptAllChanges` sind die UserRoles `Detached`.
- `SavedChanges`: `RemoveRange(tmp)` hängt die Detached-Instanzen als `Deleted` wieder an → zweites `SaveChanges`
  erzeugt `DELETE` auf nicht mehr existierende Zeilen → 0 rows → `DbUpdateConcurrencyException`.

Gleiches gilt für alle anderen `LoadCascadeProxies`-Varianten, wenn der Aufrufer Abhängige bereits selbst entfernt
(oder ein DB-Cascade sie im ersten Save mitlöscht, z.B. `GRoleLRole.LocalRole` ist `OnDelete(Cascade)`).

### b) Re-Entrancy: das Nach-Speichern löst erneut die Kaskaden-Erfassung aus (mehrstufige Vererbung)

Das `SaveChanges()` in `SavedChangesAsync` läuft wieder durch **denselben** Interceptor. `SavingChangesAsync` sieht
jetzt die vom Interceptor selbst gelöschten Datensätze und sammelt deren Kaskaden **nochmals**, obwohl die rekursive
Erfassung aus Phase 1 sie bereits enthält.

Beispiel: A → B → C (Rollen-Vererbung), `p0` in A, `p1` in B (`OriginId = p0`), `p2` in C (`OriginId = p1`).
Löschen von RoleRole A→B:
1. Save 1: RoleRole gelöscht; registriert „lösche p1“, rekursiv „lösche p2“.
2. Save 2 (Interceptor): `SavingChanges` sieht `p1`, `p2` als `Deleted`, sucht deren Kinder (`OriginId == p1` → `p2`,
   noch in der DB) und registriert erneut „lösche p2“. Save 2 löscht `p1`, `p2`.
3. Save 3 (Interceptor): `p2` ist `Detached` → `RemoveRange` → `DELETE` → 0 rows → Exception.

Bei einstufiger Vererbung tritt das nicht auf (Save 3 hat dann nur leere `RemoveRange`-Aufrufe), allerdings wird
trotzdem ein überflüssiges drittes `SaveChanges` ausgeführt, weil `callSave = contextActions.Count != 0` auch für
Aktionen mit leeren Listen gilt.

## Weitere Befunde im selben Interceptor

- **Kein Aufräumen bei Fehlern:** `SaveChangesFailed`/`SaveChangesFailedAsync` sind nicht überschrieben.
  Scheitert ein Save, bleiben `contextActions`, `ropes`, `roros` in der Interceptor-Instanz liegen und werden beim
  nächsten (fachlich unabhängigen) `SaveChanges` desselben Scopes ausgeführt.
- **`gloros` wird nie verarbeitet oder geleert:** Neue `TGRoleLRole` werden in `SavingChangesAsync` gesammelt
  (`gloros.Add(gloro)`), aber von keiner Stelle gelesen oder geleert → Liste wächst über die Lebensdauer der Instanz.
- **Unnötiges Nach-Speichern:** `callSave` ist `true`, sobald irgendeine Aktion registriert ist, auch wenn alle
  `RemoveRange`-Listen leer sind.

## Vorschlag

1. **Bereits gelöschte Datensätze überspringen** in allen registrierten Lösch-Aktionen, z.B.:
   ```csharp
   modifyActions.Add(db => db.Set<TUserRole>().RemoveRange(
       tmp.Where(e => db.Entry(e).State is not (EntityState.Detached or EntityState.Deleted))));
   ```
   (`Detached` = im vorherigen Save bereits gelöscht, `Deleted` = vom Aufrufer/anderer Aktion schon markiert.)
   Alternativ beim Laden in `LoadCascadeProxies` Einträge ausfiltern, deren Entry bereits `Deleted` ist.
2. **Re-Entrancy-Guard:** Während der Interceptor selbst `SaveChanges` aufruft, in `SavingChangesAsync` keine
   neuen Kaskaden erfassen (Flag in der Instanz, im `finally` zurücksetzen). Die rekursive Erfassung in Phase 1
   deckt die gesamte Kette bereits ab.
3. **`SaveChangesFailed`/`SaveChangesFailedAsync` überschreiben** und `contextActions`, `ropes`, `roros`, `gloros`
   leeren.
4. **`gloros` verarbeiten** (analog `roros`/`ropes`) oder, falls nicht benötigt, gar nicht erst sammeln.
5. **Nach-Speichern nur bei tatsächlichen Änderungen:** `callSave` von `ChangeTracker.HasChanges()` nach Ausführung
   der Aktionen abhängig machen statt von `contextActions.Count`.

## Akzeptanzkriterien

- Test: TenantUser mit UserRoles; Aufrufer entfernt **UserRoles und TenantUser** im selben `SaveChanges` → kein
  Fehler, UserRoles und TenantUser sind gelöscht.
- Test: dreistufige Rollen-Vererbung A → B → C mit vererbter Permission; RoleRole A→B löschen → kein Fehler,
  `p1` und `p2` gelöscht, `p0` unverändert.
- Test: ein absichtlich fehlschlagendes `SaveChanges` (z.B. Constraint-Verletzung), danach ein unabhängiges
  `SaveChanges` auf demselben Context → es werden keine Aktionen aus dem fehlgeschlagenen Save ausgeführt.
- Sync- (`SaveChanges`) und Async-Pfad (`SaveChangesAsync`) verhalten sich gleich.
- Bestehende Tests in `ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Test`
  (z.B. `RoleInheritanceDuplicateTest`) bleiben grün.

## Workaround auf Konsumentenseite (bis zum Fix)

Abhängige nicht selbst löschen, sondern nur den Principal entfernen und die Kaskade dem Interceptor überlassen
(ESAG: in `SyncUser` die Zeile `securityDb.TenantUserRoles.RemoveRange(tu.Roles);` entfernen).

## Umsetzung (DEV)

- **Vorschlag 1:** Alle Lösch-Aktionen laufen über `RemoveStillTracked`, das `Detached`- und `Deleted`-Einträge
  überspringt. Das behebt a) und b).
- **Vorschlag 2 (Re-Entrancy-Guard) bewusst nicht:** Das verschachtelte `SaveChanges` wird gebraucht. Erst damit
  wandern neu abgeleitete Rechte über mehrere Stufen weiter, und beim Löschen einer RoleRole erfasst erst der zweite
  Durchlauf die Kinder der abgeleiteten `GRoleLRole` (die erste Erfassung läuft bei ihnen nicht rekursiv). Mit
  Vorschlag 1 ist die erneute Erfassung harmlos.
- **Vorschlag 3:** `SaveChangesFailed(Async)` und `SaveChangesCanceled(Async)` leeren alle Listen und schreiben eine
  `Warning`, wenn dabei etwas verworfen wurde.
- **Vorschlag 4:** Auf DEV werden neue `GRoleLRole` nicht weitergegeben; die Liste wird nach jedem Save geleert.
  Future_10 gibt sie seit PRE234 weiter (`ProcessGlobalRoleInheritanceChanges`); eine Übernahme nach DEV wäre eine
  eigene Verhaltensänderung.
- **Vorschlag 5:** Nach-Speichern nur bei `contextActions.Count != 0 && ChangeTracker.HasChanges()`. `HasChanges()`
  allein würde nach `SaveChanges(acceptAllChangesOnSuccess: false)` die Einträge des Aufrufers ein zweites Mal speichern.
- **Tests:** DEV hat kein TenantSecurity-Testprojekt; die Akzeptanztests kommen mit der Umsetzung auf Future_10.
