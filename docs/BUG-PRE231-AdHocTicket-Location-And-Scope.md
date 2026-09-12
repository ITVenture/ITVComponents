> **BEHOBEN in PRE232.** Alle drei Befunde bestätigt und umgesetzt, Leitfaden §55.10.
> Umgesetzt wurde Vorschlag **1** (Unterscheidung Anmeldung/Riegel) **zusammen mit** der Alternative
> (Ortsprüfung gegen `AssetTicket.RootPath`) — nicht statt ihrer:
>
> * **Anmeldung** prüft das Ticket: entschlüsseln, Mandant, Fristen, Widerruf, Vorlage samt
>   `AllowAdHoc`, und den `RootPath` gegen das Pfadmuster. Kein laufender Pfad, keine Gültigkeitsregel.
> * **Riegel** prüft zusätzlich den laufenden Pfad und die Gültigkeitsregel, einmal je Vorgang.
>
> Der Grund, warum es beides braucht, ist ein **vierter Befund, der hier fehlt**: alles, was am
> `AssetKey` hängt, kannte Tickets nicht. `VerifyRequestLocation` schlägt über `AssetIsAccessible` in
> `SharedAssets` nach — ein Ticket hat dort keine Zeile —, und `IsLegitSharedAssetPath` baute die
> Asset-Sicht über `GetAssetInfo(assetKey)`, für ein Ticket also `null`. Der Riegel *konnte* den Ort
> für ein Ticket bisher gar nicht prüfen. Vorschlag 1 allein hätte die Ortsbindung damit ersatzlos
> entfernt. Beide Stellen unterscheiden jetzt nach `SegmentKind`.
>
> **Breaking am Vertrag:** `ISharedAssetAdapter.GetTicketInfo(..., bool forAuthentication = false)` und
> neu `ISharedAssetContext.AuthenticationAsset`. Wer selbst authentifiziert, nimmt `AuthenticationAsset`
> statt `CurrentAsset` — bei gespeicherten Freigaben identisch, bei einem Ticket der Unterschied
> zwischen einer Seite und einer weissen Seite.
>
> Regressionstests in `ITVComponents.WebCoreToolkit.Tests/AdHocTicketAuthenticationTests.cs`: dass die
> Anmeldung die Anmeldefrage stellt, dass der Riegel weiterhin die volle stellt, und dass eine
> Unterressource (`/ADM/_framework/blazor.web.js`) sich anmelden kann.
>
> Danke für die Reproduktionstabelle mit `^(/ADM)?/checkout/\d+$` — die mittlere Zeile war der
> Beweis, dass mehr als der Mandant am Pfad hing, und hat die Diagnose abgekürzt.

# BUG (PRE231): Die Ticket-Anmeldung prüft zu viel — Ortsprüfung je Datei, Gültigkeitsregel ohne Mandanten-Geltungsbereich

> Gemeldet aus **MiniStore** (Blazor Web App, .NET 10, `TenantSource.PathSegment`, TreeTenants,
> PostgreSQL, Toolkit `5.0.0-PRE231`), unmittelbar nach dem Fix aus
> `BUG-PRE230-AdHocTicket-Anonymous-Authentication.md`.
>
> **Der Fix aus PRE230 wirkt** — der Besucher wird jetzt angemeldet, und die stillen Stellen
> sprechen. Genau dadurch wurde sichtbar, dass der neue Aufrufweg zwei Prüfungen mitbringt, die an
> dieser Stelle nicht stehen können.

## Übersicht

| | |
|---|---|
| **Betroffen** | `SharedAssetInfoProvider.cs:844` (Ortsprüfung) und `:853` (Gültigkeitsregel), gerufen aus `AnonymousAssetAuthenticationHandler.cs:138` (`CurrentAsset` → `GetTicketInfo`) und `AssetDrivenClaimsTransformation.cs:83` |
| **Kern** | `GetTicketInfo` war bisher der Weg *einer Seite*, die ihre Argumente bestätigt — mit abgetrenntem Pfad und gesetztem Geltungsbereich. Seit PRE231 ist es zusätzlich der Weg der **Anmeldung**, und dort gilt beides nicht. |
| **Folge A** | `Canonical(RequestPath)` behält den Freigabe-Abschnitt. Das Pfadmuster der Vorlage passt nie. |
| **Folge B** | Die Ortsprüfung trifft **jede** Anfrage — auch `blazor.web.js`, CSS und Bilder. Die Seite meldet sich an, ihre Bestandteile nicht: **weisse Seite**. |
| **Folge C** | `IsStillValid` ruft die Gültigkeitsregel ohne Mandanten-Geltungsbereich. Jede Regel, die mandantengebundene Daten liest, bekommt eine **leere Verbindungszeichenfolge**. |
| **Nicht betroffen** | Gespeicherte Freigaben. Dort authentifiziert der Handler über `AssetKey` + `AccessToken` (`:88`) — **ohne** Ortsprüfung. Die macht später der Riegel, einmal je Vorgang. Genau diese Trennung fehlt dem Ticket-Zweig. |

## Reproduktion

Aufbau wie im Vorbericht: `pos-start` (gespeichert, anonym) führt auf einen Bootstrapper, der ein
Ad-hoc-Ticket auf `pos-checkout` ausstellt und dorthin weiterleitet.

```
pos-checkout   AllowAdHoc true, ArgumentEnforcement 2 (Strict), MaxAdHoc 60
               ValidityRuleKey  order-open
               PathFilter       ^/checkout/\d+$
               Argument         orderId (Int, Required)
```

Die drei Folgen lassen sich einzeln freischalten, indem man die jeweilige Prüfung entschärft. Das
haben wir am laufenden System durchgespielt:

| Pfadmuster | Gültigkeitsregel | Ergebnis |
|---|---|---|
| `^/checkout/\d+$` | `order-open` | 404 — „gilt an dieser Stelle nicht" |
| `^(/ADM)?/checkout/\d+$` | `order-open` | 404 — **unverändert**, obwohl beide plausiblen Formen abgedeckt sind |
| `^.*checkout/\d+$` | `order-open` | Seite kommt durch; jetzt scheitert die **Gültigkeitsregel** |
| `^.*checkout/\d+$` | — (abgehängt) | Seite kommt durch; **weisse Seite**, weil alle Unterressourcen 404 sind |

Die mittlere Zeile ist der Beweis für **A**: hätte `Canonical` nur den Mandanten stehen lassen,
wäre `^(/ADM)?/checkout/\d+$` durchgegangen. Es passt erst, wenn ein **beliebiger** Präfix erlaubt
ist — also trägt der geprüfte Pfad noch den Freigabe-Abschnitt.

## Die drei Befunde

### A — `Canonical(RequestPath)` kennt den Abschnitt zum Anmeldezeitpunkt nicht

```csharp
// SharedAssetInfoProvider.cs:844
if (!IsTemplateValidForPath(assetTmp, Canonical(services.GetService<IContextUserProvider>()?.RequestPath)))
```

`Canonical` (`:1055`) holt den abzuschneidenden Abschnitt aus `ISharedAssetContext.Segment` und den
Mandanten aus `IPermissionScope.PermissionPrefix`. Im Anmelde-Handler steht davon noch nichts fest
— der Prinzipal entsteht ja gerade erst. Das Muster einer Vorlage ist aber gegen den **kanonischen**
Pfad geschrieben (`^/checkout/\d+$`, so steht es auch im Leitfaden). Es kann dort nicht passen.

### B — die Ortsprüfung trifft jede Datei, nicht jeden Vorgang

Das ist der schwerwiegendere Punkt, und er ist eine **Asymmetrie zum bestehenden Weg**:

* **Gespeicherte Freigabe:** `HandleAuthenticateAsync` → `getAnonymousAssetQuery.Execute(AssetKey,
  AccessToken)` (`:88`). Schlüssel und Token, sonst nichts. Der Ort wird später geprüft, im Riegel,
  **einmal je Vorgang** — dieselbe Begründung, die §56.2 für das Zugriffsprotokoll gibt („bei einer
  Zeile pro Anfrage wäre die Tabelle nach einer Woche unbenutzbar").
* **Ticket:** `HandleAuthenticateAsync` → `CurrentAsset` (`:138`) → `GetTicketInfo` → **vollständige**
  Validierung inklusive Ort.

Eine Seite lädt aber Dateien nach, und die tragen denselben Freigabe-Abschnitt:

```
no principal for /ADM/_framework/blazor.web.9hsif5t8mt.js   (The ad-hoc ticket is not valid.)
no principal for /ADM/_content/MudBlazor/MudBlazor.min.js   (The ad-hoc ticket is not valid.)
no principal for /ADM/MiniStore.Web.0opedqpr5n.styles.css   (The ad-hoc ticket is not valid.)
no principal for /ADM/favicon.png                           (The ad-hoc ticket is not valid.)
```

Jede davon wird gegen das Pfadmuster **der Seite** geprüft und fällt durch. Selbst mit einem
Muster, das die Seite hereinlässt, bleibt die Seite damit funktionslos: ohne `blazor.web.js` kein
Circuit, und bei `prerender: false` ist das eine vollständig **weisse Seite** ohne Fehlermeldung im
Browser.

Der Ort gehört unseres Erachtens dorthin, wo er bei gespeicherten Freigaben schon steht: in den
Riegel (`VerifyRequestLocation` / `AssetScope.Require`). Die Anmeldung sollte das **Ticket** prüfen
— entschlüsseln, Mandant, `NotBefore`/`NotAfter`, Widerruf, Vorlage samt `AllowAdHoc` — und nicht
die Stelle, an der es gerade benutzt wird.

### C — die Gültigkeitsregel läuft ohne Mandanten-Geltungsbereich

```csharp
// SharedAssetInfoProvider.cs:853
if (!IsStillValid(assetTmp.ValidityRuleKey, values, ticket.Nonce))
```

Unsere Regel `order-open` beantwortet „gilt die Freigabe noch?" mit „ist der Auftrag offen?" und
liest dafür den Fachkontext. Aus dem Anmelde-Handler heraus:

```
fail: MiniStore.Web.Services.OrderOpenValidityRule[0]
      Asset validity rule 'order-open' could not check order 20.
      System.ArgumentException: Format of the initialization string does not conform to
      specification starting at index 0.
         at Npgsql.NpgsqlConnectionStringBuilder..ctor(String connectionString)
         at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlRelationalConnection.CreateDbConnection()
```

Die Verbindungszeichenfolge ist **leer**. Der Kontext kommt über die Plugin-Fabrik, und deren
Namens- und Konstantenauflösung wendet den Mandanten-Präfix an; ohne Geltungsbereich bleibt die
Konstante unaufgelöst.

**Der Gegenbeweis steht im selben Log:** derselbe Kontext, dieselbe Lease-Art, wenige Zeilen früher —
`OrderCartService.CreateAsync` legt den Auftrag an und funktioniert. Dort ist die Anfrage bereits
über die `pos-start`-Freigabe authentifiziert, ein Geltungsbereich existiert. Der Unterschied ist
nicht der Kontext, sondern der Zeitpunkt.

Eine Regel, die *nichts* aus der Datenbank liest, wäre selten: „gilt das noch?" ist fast immer eine
Frage an Fachdaten — so ist das Merkmal im Leitfaden auch motiviert („‚Bis der Auftrag abgeschlossen
ist' ist kein Ablauf").

`GetTicketInfo` kennt `ticket.TenantName` und prüft ihn seit PRE231 ohnehin gegen den Namen aus der
URL. Damit liesse sich der Geltungsbereich setzen, bevor die Regel gefragt wird.

## Fix-Vorschlag

**Die Anmeldung prüft das Ticket, der Riegel prüft den Ort.**

1. `GetTicketInfo` bekommt eine Unterscheidung, ob es für die **Anmeldung** oder für den **Riegel**
   gerufen wird (Parameter oder eine getrennte Methode wie `GetTicketInfoForAuthentication`). Im
   Anmeldefall entfallen Ortsprüfung (`:844`) und Gültigkeitsregel (`:853`).
2. Der Riegel prüft beides weiterhin — dort ist der Pfad kanonisch, der Geltungsbereich steht, und
   es passiert **einmal je Vorgang** statt je Datei.
3. Falls die Gültigkeitsregel schon bei der Anmeldung greifen soll: vorher den Geltungsbereich aus
   `ticket.TenantName` setzen. Dann gilt sie auch für Unterressourcen — was für eine zurückgezogene
   Freigabe wünschenswert sein kann, aber die Kosten je Datei mit sich bringt.

Alternativ zu 1.: die Ortsprüfung im Anmeldefall gegen den **`RootPath` des Tickets** führen statt
gegen den aktuellen Pfad. Das Ticket trägt ihn (`AssetTicket.RootPath`), er ist beim Ausstellen
kanonisch gespeichert, und er ist von der laufenden Anfrage unabhängig. Damit bliebe eine Prüfung
erhalten, ohne über Unterressourcen zu stolpern.

## Abgrenzung / was ausdrücklich funktioniert

* **Der Fix aus PRE230 wirkt.** Der Besucher wird angemeldet, der Prinzipal heisst `#ANONYMOUS#`,
  und die Meldungen sind da. Ohne sie hätten wir keinen dieser drei Punkte gefunden.
* **Der Mandant in der Nutzlast** funktioniert. Ein Ticket von *vor* dem Update meldete sauber
  `gehoert dem Mandanten '(none)', wurde aber unter 'ADM' aufgerufen` — genau wie angekündigt.
* **Entschlüsseln, Fristen, Widerruf, Vorlagenauflösung**: alle durchlaufen ohne Beanstandung.
* **Rechte und Features** der Vorlage, beide Hälften (`RequiredPermission`/`AssetTemplateGrants`,
  `FeatureId`/`AssetTemplateFeatures`) — gesetzt und wirksam.
* **`SharedAssetInfoProvider`** steht in `TrustedFullAccessComponents` mit `ShowAllTenants`.
* **Gespeicherte anonyme Freigaben** (`pos-start`) laufen unverändert, samt ihrer Unterressourcen.
  Das ist der Vergleichsfall, der die Asymmetrie in Befund B trägt.

## Umgebung

| | |
|---|---|
| Toolkit | `5.0.0-PRE231` |
| Host | .NET 10, Blazor Web App (Server + WASM Auto), `prerender: false`, MudBlazor 9 |
| Mandanten | `TenantSource.PathSegment`, `app.UseTenantPathPrefix()`, TreeTenants |
| Sicherheitskontext | `AspNetTreeSecurityContext` (CoreIdentityTree) |
| Datenbank | PostgreSQL 17 (Npgsql), EF Core 10 |
| Fachkontext | über die Plugin-Fabrik (`IFreshInjectablePlugin<T>`), Verbindung als globale `WebPluginConstants`-Zeile |
