# BUG (PRE234): Eine Vorlagen-Rolle mit `Permissions` **und** `RoleGrants` verletzt beim Anwenden `IX_UniqueRolePermission`

> Gemeldet aus MiniStore (`5.0.0-PRE234`). Hat dort eine Mandanten-Anlage über das Onboarding
> vollständig blockiert; umgangen wird er zurzeit dadurch, dass die Vorlage **keine** `RoleGrants`
> mehr benutzt und jede Rolle ihre Rechte selbst trägt.

## Übersicht

| | |
|---|---|
| **Betroffen** | `ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity/Shared/Interceptors/SecurityModificationInterceptor.cs`, `ProcessPermissionInheritanceChanges` (ab Zeile 323) und `ProcessRoleInheritanceChanges` (ab Zeile 358), zusammen mit `Shared/Helpers/TenantTemplateHelperBase.cs`, `ApplyTemplatePrivate` (Zeile 508-518) |
| **Kern** | Entstehen in **einem** `SaveChanges` sowohl die `RolePermission`-Zeilen einer Rolle als auch eine `RoleRole`, die diese Rolle als `PermissiveRole` führt, erzeugen **beide** Nachbearbeitungen dieselbe abgeleitete Zeile: gleiche `RoleId`, `PermissionId`, `TenantId` **und** `OriginId`. |
| **Folge** | `23505: duplicate key value violates unique constraint "IX_UniqueRolePermission"`. Beim Onboarding rollt damit die gesamte Transaktion zurück — kein Mandant, kein Rechnungsprofil, der Pending-Datensatz bleibt offen. |
| **Auslösende Bedingung** | Eine Rolle im Vorlagen-Markup hat ein nicht-leeres `Permissions` **und** ein nicht-leeres `RoleGrants`. |
| **Warum das bisher niemandem auffiel** | Im MLM-Bestand trägt jede Vorlagen-Rolle `"Permissions": []` und bezieht ihre Rechte ausschliesslich über `GlobalRoleGrants`. Damit ist `ropes` beim Anlegen der `RoleRole` leer und nur ein Pfad läuft. |

## Messung

MiniStore, Vorlage `MiniStoreBasicTenant`, Rolle `TenantOwner` mit 16 Rechten und
`"RoleGrants": ["Employees"]`. Aus `SystemLog` (UTC), gekürzt:

```
2026-09-14 08:58:07.549  Microsoft.EntityFrameworkCore.Update
  Npgsql.PostgresException 23505: duplicate key value violates unique constraint "IX_UniqueRolePermission"

2026-09-14 08:58:07.736  …OnboardingViews.Components.Onboarding.MyTenants
  Completing the pending onboarding for laden@ecke.ch failed; the tenant was not created.
    at SecurityModificationInterceptor`12.SavedChangesAsync(...)
    at SecurityModificationInterceptor`12.<>c__DisplayClass6_0.<<SavedChanges>b__0>d.MoveNext()
    at TenantTemplateHelperBase`47.ApplyTemplatePrivate(TTenant, TenantTemplateMarkup, Boolean, TemplateApplyMode)
    at TenantTemplateHelperBase`47.ApplyTemplate(DbContext, TTenant, TenantTemplateMarkup, Action`1)
    at HierarchyOnboardingHandler`1.ApplyTenantTemplateAsync(...)
    at HierarchyOnboardingHandler`1.CreateOrResumeTenantAsync(...)
    at OnboardingPendingHelper.CompleteAsync[TCtx,TUser](...)
```

Der Index ist `UNIQUE btree ("RoleId", "PermissionId", "TenantId", "OriginId")`. Die Kollision
entsteht also nicht zwischen direkt vergebenem und geerbtem Recht — die unterscheiden sich in
`OriginId` — sondern zwischen **zwei identischen Ableitungen derselben Quellzeile**.

## Root Cause

`ApplyTemplatePrivate` legt Rollen, Rechte und Grants in einem Zug an (Zeile 508-518): erst alle Rollen
speichern, dann je Rolle `ApplyPermissions` und `ApplyRoleGrants`, und das gemeinsame `SaveChanges`
folgt danach. Im `ChangeTracker` dieses einen Speichervorgangs stehen damit gleichzeitig:

* die neuen `RolePermission`-Zeilen der permissiven Rolle → gesammelt in `ropes` (Zeile 130),
* die neue `RoleRole`-Zeile → gesammelt in `roros` (Zeile 126).

Anschliessend laufen beide Nachbearbeitungen:

* `ProcessPermissionInheritanceChanges` geht von den neuen Rechten aus und verteilt sie auf
  `n.Role.PermittedRoles` — erzeugt `{RoleId = PermittedRole, OriginId = n.RolePermissionId}`.
* `ProcessRoleInheritanceChanges` geht von der neuen `RoleRole` aus und verteilt
  `n.PermissiveRole.RolePermissions` — erzeugt `{RoleId = PermittedRole, OriginId = np.RolePermissionId}`.

Für dieselbe Quellzeile ist das zweimal exakt derselbe Datensatz. Jeder der beiden Pfade ist für sich
richtig und nötig — der eine deckt „Recht kommt zu einer Rolle hinzu, die bereits vererbt", der andere
„Vererbung entsteht für eine Rolle, die bereits Rechte hat". Nur der gleichzeitige Fall ist nicht
abgefangen.

## Lösungsvorschlag

Die beiden Ableitungen gegeneinander entdoppeln. Am wenigsten invasiv in
`ProcessRoleInheritanceChanges`: die Quellzeilen ausklammern, die im selben Durchlauf bereits über
`ropes` propagiert werden — deren Ids sind dort bekannt, bevor `ropes.Clear()` läuft. Alternativ die
fertige Menge beider Pfade vor dem `AddRange` über `(RoleId, PermissionId, TenantId, OriginId)`
eindeutig machen; das deckt zusätzlich den Fall ab, dass eine Rolle über mehrere Wege dieselbe
Ableitung bekommt.

Dasselbe gilt sinngemäss für `ProcessGlobalRoleInheritanceChanges` und `TGRoleLRole`, auch wenn dort
kein Fehler beobachtet wurde: der Aufbau ist derselbe.

## Nebenbefund: die Richtung von `RoleGrants` ist aus dem Markup nicht erkennbar

`RoleGrants` auf Rolle X erzeugt `RoleRole { PermissiveRole = X, PermittedRole = die genannte Rolle }`,
und propagiert werden die Rechte **von X zur genannten Rolle**. Gelesen wird der Eintrag naheliegend
umgekehrt — „X bekommt die Rechte der genannten Rolle". Wer sich vertut, gibt der schwächeren Rolle
die Rechte der stärkeren, und zwar lautlos: es entsteht kein Fehler, nur zu viel Recht.

Eine Zeile am `RoleTemplateMarkup.RoleGrants` („die Rollen, an die diese Rolle ihre Rechte weitergibt")
würde das ausräumen. Im bestehenden MLM-Markup ist die Richtung am Beispiel
`PARENT##Systemadministrator` gut zu sehen — der Kind-Mandant reicht seine Rechte an den
Eltern-Sysadmin hoch —, aber genau dieses Beispiel liest sich ohne den Interceptor-Code auch andersherum
plausibel.
