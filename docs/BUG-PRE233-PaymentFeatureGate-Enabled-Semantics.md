# BUG (PRE233): `PaymentFeatureGate` liest `Feature.Enabled` anders als die Feature-Auflösung — ein je Mandant verkauftes Zahlungs-Feature lässt sich nicht einschalten

> Gemeldet aus **MiniStore** (Blazor Web App, .NET 10, TreeTenants, PostgreSQL, Toolkit
> `5.0.0-PRE233`, Achse B / Stripe Connect gerade in Betrieb genommen).

## Übersicht

| | |
|---|---|
| **Betroffen** | `ITVComponents.WebCoreToolkit.EntityFramework.Billing.TenantSecurity/PaymentFeatureGate.cs`, Zeilen 52–58 |
| **Kern** | Das Tor verlangt `Feature.Enabled == true` **UND** eine `TenantFeatureActivation`. Die reguläre Feature-Auflösung (`DbSecurityRepository.GetFeatures`) rechnet dagegen `Enabled = Zeile.Enabled ODER es gibt eine Aktivierung`. Dieselbe Spalte, zwei entgegengesetzte Bedeutungen. |
| **Folge** | Genau die Konfiguration, die für ein **je Mandant verkauftes** Feature richtig ist (`Enabled = false` + Aktivierung je Mandant), lässt das Tor jeden Verkauf ablehnen — und zwar stumm, denn fail-closed ist hier die Absicht. |
| **Nicht betroffen** | Hosts, die `StripePayments` global anschalten. Die haben dafür kein verkaufbares Modul mehr. |

## Beobachtung

MiniStore verkauft die Kartenzahlung als Zusatzmodul. Das Muster dafür ist im Toolkit vorgegeben und
wird auch von unserem Basis-Feature `MiniStore.Core` so benutzt:

```sql
-- Feature-Zeile: NICHT global an
INSERT INTO "Features" ("FeatureName", "Enabled") VALUES ('StripePayments', false);

-- ... sondern je Mandant aktiviert
INSERT INTO "TenantFeatureActivations" ("FeatureId", "TenantId", "ActivationStart") VALUES (…, 1, …);
```

Damit sagt `SecureView RequiredFeatures="StripePayments"` für Mandant 1 korrekt „ja" und für jeden
anderen „nein" — das ist die Rechnung aus `DbSecurityRepository`:

```csharp
// ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity/TreeShared/Security/DbSecurityRepository.cs:1308
Enabled = n.T.Enabled || !string.IsNullOrEmpty(n.A)   // n.A = Mandant mit gültiger Aktivierung
```

`Enabled` auf der Zeile heisst dort also: **„gilt für alle"**. Eine Aktivierung ist der zweite,
mandantenbezogene Weg zum selben Ja.

`PaymentFeatureGate` liest dieselbe Spalte als **Hauptschalter**:

```csharp
// PaymentFeatureGate.cs:52-58
var feature = await db.Set<Feature>().IgnoreQueryFilters()
    .FirstOrDefaultAsync(f => f.FeatureName == FeatureKey, cancellationToken);
if (feature is not { Enabled: true })
{
    return false;
}

return await db.Set<TActivation>().IgnoreQueryFilters().AnyAsync(a => a.TenantId == tenantId && …);
```

Beides zugleich erfüllen kann man nur mit `Enabled = true` — und dann ist das Feature laut der
regulären Auflösung für **alle** Mandanten an.

## Die Klemme, konkret

| `Features.Enabled` | Aktivierung für Mandant X | `SecureView` / `HasFeature` | `PaymentFeatureGate` | Brauchbar? |
|---|---|---|---|---|
| `false` | ja | X: ja, andere: nein ✔ | **nein** ✘ | Nein — Masken gehen auf, jeder Verkauf wird abgelehnt |
| `false` | nein | nein ✔ | nein ✔ | Ja, aber das ist „Modul aus" |
| `true` | ja | **alle: ja** ✘ | X: ja, andere: nein ✔ | Nur mit Beigeschmack |
| `true` | nein | alle: ja ✘ | nein ✔ | Nein — Masken auf, nichts geht |

Keine Zeile ist richtig. Wir fahren heute Zeile 3 und haben es in der Migration als Kröte
dokumentiert: der Verkauf bleibt korrekt je Mandant verschlossen (`TenantPaymentAccountService` und
`TenantSaleService` fragen dasselbe Tor), aber die Maske „Auszahlungskonto" öffnet sich auch einem
Laden ohne das Modul und gibt dort nichts her.

## Unser Vorschlag

`PaymentFeatureGate` auf dieselbe Rechnung bringen wie die reguläre Auflösung:

```csharp
var feature = await db.Set<Feature>().IgnoreQueryFilters()
    .FirstOrDefaultAsync(f => f.FeatureName == FeatureKey, cancellationToken);
if (feature == null)
{
    return false;   // das Feature gibt es gar nicht
}

if (feature.Enabled)
{
    return true;    // global an — wie in DbSecurityRepository
}

return await db.Set<TActivation>().IgnoreQueryFilters().AnyAsync(…);
```

Das ist weiterhin fail-closed (kein Feature, keine Aktivierung → nein) und macht die
Standard-Konfiguration eines verkauften Moduls (`Enabled = false` + Aktivierung) zur richtigen.

**Was wir NICHT vorschlagen:** die Bedeutung von `Enabled` in `DbSecurityRepository` zu ändern. Dort
hängt zu viel dran, und „gilt für alle" ist die Lesart, die der Feature-Maske im AdminViews-Paket
entspricht.

**Falls ihr den Hauptschalter absichtlich wollt** — es gibt gute Gründe, eine Geld-Funktion
zusätzlich zentral abwürgen zu können —, dann bitte nicht über `Features.Enabled`, sondern über
`StripePaymentsOptions.Enabled`. Den Schalter gibt es dort bereits (`PaymentsRuntime.EnsureEnabled`),
er ist genau dafür beschrieben („the deployment always wins over the entitlement"), und er kollidiert
mit nichts. Dann bräuchte das Tor die Spalte gar nicht mehr zu lesen.

## Nebenbefund: dieselbe Stelle im Log

Fällt das Tor wegen `Enabled = false` durch, ist das von aussen nicht von „dieser Mandant hat das
Modul nicht" zu unterscheiden — in beiden Fällen kommt `false` ohne Eintrag. Für die Fehlersuche hat
uns das eine Weile gekostet. Ein `LogDebug` mit dem Grund (Feature fehlt / Feature aus / keine
Aktivierung) wäre an dieser Stelle viel wert, gerade weil die Wirkung („jeder Verkauf wird
abgelehnt") so weit vom Auslöser entfernt sichtbar wird.

## Umgebung

| | |
|---|---|
| Toolkit | `5.0.0-PRE233` |
| Host | .NET 10, Blazor Web App (Server), MudBlazor 9, PostgreSQL 17 |
| Sicherheit | `AspNetTreeSecurityContext`, `HierarchyTenant` / `HierarchyTenantFeatureActivation` |
| Registrierung | `AddBillingFeatureProvisioner<…>()` + `AddPaymentFeatureGate<…>()` |
| Features | `MiniStore.Core` (Basis), `StripePayments` (Zusatzmodul), beide je Mandant aktiviert |
