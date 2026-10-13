# BUG PRE253: `DevicePairings` verliert beim EF-Migrieren das `ON DELETE CASCADE`

**Status:** behoben (nach PRE253, siehe „Behebung“) · **Datum:** 2026-10-13 · **Quelle:** MiniStore, Bump PRE252 → PRE253 (Leitfaden §73)

## Der Fall

§73 macht `DevicePairing.TenantId` und `.ClientAppId` nullbar. Das ist richtig so. Eine Konfiguration
der Löschregel gibt es aber weder als Attribut noch in einem Modell-Builder. EF leitet sie deshalb
selbst ab:

- **bisher (Pflichtfeld):** `Cascade`
- **jetzt (nullbar):** `ClientSetNull`, in der Datenbank also ohne Löschwirkung

`dotnet ef migrations add` erzeugt daraus für jeden Host, der per EF migriert, Folgendes
(MiniStore, PostgreSQL, unverändert):

```csharp
migrationBuilder.DropForeignKey(name: "FK_DevicePairings_ClientApps_ClientAppId", table: "DevicePairings");
migrationBuilder.DropForeignKey(name: "FK_DevicePairings_Tenants_TenantId", table: "DevicePairings");
// … AlterColumn nullable …
migrationBuilder.AddForeignKey(name: "FK_DevicePairings_ClientApps_ClientAppId", table: "DevicePairings",
    column: "ClientAppId", principalTable: "ClientApps", principalColumn: "ClientAppId");   // kein onDelete
migrationBuilder.AddForeignKey(name: "FK_DevicePairings_Tenants_TenantId", table: "DevicePairings",
    column: "TenantId", principalTable: "Tenants", principalColumn: "TenantId");           // kein onDelete
```

Danach steht in der Datenbank:

```
 FK_DevicePairings_ClientApps_ClientAppId  | confdeltype = a   (NO ACTION, vorher c)
 FK_DevicePairings_Tenants_TenantId        | confdeltype = a   (NO ACTION, vorher c)
```

## Warum das ein Fehler ist

| Stelle | Erwartung |
|---|---|
| Leitfaden §73.1, SQL Server | baut beide Fremdschlüssel **mit `ON DELETE CASCADE`** wieder auf, also bleibt die Löschregel |
| Leitfaden §73.1, PostgreSQL | fasst die Fremdschlüssel nicht an, die Löschregel bleibt `CASCADE` |
| EF-Modell (`Shared/Models/Base/DevicePairing.cs:117-121`) | nur `[ForeignKey]`, kein `OnDelete`, die Löschregel wird `ClientSetNull` |

Ein Host, der nach dem Leitfaden per SQL migriert, und einer, der per EF migriert, haben damit
verschiedene Schemata. Beim nächsten `migrations add` nach einem SQL-Update „repariert“ EF den
Fremdschlüssel ausserdem auf `NO ACTION`.

**Die Folge:** Kopplungsvorgänge werden nirgends gelöscht. Im Toolkit gibt es kein `Remove` und kein
`ExecuteDelete` auf `DevicePairings`; abgelaufene Vorgänge bleiben stehen. Nach einer EF-Migration
scheitert deshalb jedes Löschen einer **Client-Anwendung** oder eines **Mandanten**, an der oder dem
je ein Gerät gekoppelt wurde, mit einer Fremdschlüssel-Verletzung. Die Ausnahme sind Vorgänge, die
zufällig im selben `DbContext` geladen sind.

Im Test fällt das nicht auf, weil InMemory keine Fremdschlüssel kennt (siehe die Checkliste in §73.6).

## Vorschlag

Die Löschregel ausdrücklich festlegen, im `OnModelCreating` des Sicherheitskontexts (bzw. dort, wo
die übrigen `DevicePairing`-Beziehungen konfiguriert werden):

```csharp
modelBuilder.Entity<TDevicePairing>().HasOne(p => p.ClientApp).WithMany()
    .HasForeignKey(p => p.ClientAppId).OnDelete(DeleteBehavior.Cascade);
modelBuilder.Entity<TDevicePairing>().HasOne(p => p.Tenant).WithMany()
    .HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Cascade);
```

Damit stimmen EF-Modell und Leitfaden-SQL wieder überein. Ein Host, der PRE253 schon per EF migriert
hat, bekommt den Fremdschlüssel mit der nächsten Migration zurück. Das gehört als Hinweis in den
Leitfaden.

## Umgehung

Bis dahin ist keine nötig, solange keine Client-Anwendung und kein Mandant gelöscht wird. Wer
löschen muss, löscht vorher die zugehörigen Zeilen in `DevicePairings` von Hand. Die generierte
Migration in MiniStore (`20261013102402_ToolkitPRE253PairingByTypeKey`) bleibt unverändert, weil
eine Handkorrektur dort beim nächsten `migrations add` gegen den Snapshot wieder zurückgedreht würde.

## Behebung

Anders als vorgeschlagen bekommt **nur `ClientAppId` die Kaskade**. Cascade auch auf `TenantId` ergäbe
zwei Löschpfade von `Tenants` nach `DevicePairings`: direkt und über `ClientApps`
(`FK_ClientApps_Tenants_TenantId` ist Cascade). SQL Server weist das mit Fehler 1785 ab. Nötig ist es
auch nicht: Mandant und Anwendung werden immer zusammen gesetzt, aus derselben Anwendung. Ein
gelöschter Mandant räumt seine Vorgänge also über `ClientApps` ab.

- `DevicePairing.cs`:
  - `ClientApp` ist `[DeleteBehavior(DeleteBehavior.Cascade)]`.
  - `Tenant` ist ausdrücklich `[DeleteBehavior(DeleteBehavior.ClientSetNull)]` (NO ACTION) und
    begründet.
  - Beides steht als Attribut am Modell und wirkt deshalb auch zur Design-Time.
- Leitfaden:
  - §65.3 (SQL Server und PostgreSQL): Der Mandanten-Fremdschlüssel hat keine Kaskade mehr. Die alte
    Fassung scheiterte auf SQL Server an `FK_ClientApps_Tenants_TenantId`.
  - §73.1 SQL Server: Der Mandanten-Fremdschlüssel wird ohne Kaskade aufgebaut.
  - §73.1 PostgreSQL: Beide Fremdschlüssel werden ausdrücklich gesetzt.
  - §73.1 enthält einen Hinweis für Hosts, die per EF migriert haben, und §73.6 einen neuen
    Checklistenpunkt.
- Test: `DevicePairingChoiceTest.DeleteRules_CascadeOverTheApplicationOnly` prüft die Regeln am Modell.

MiniStore: Die nächste `migrations add` stellt `FK_DevicePairings_ClientApps_ClientAppId` auf
`CASCADE`. Der Mandanten-Fremdschlüssel bleibt `NO ACTION`. Von Hand ist nichts zu tun.
