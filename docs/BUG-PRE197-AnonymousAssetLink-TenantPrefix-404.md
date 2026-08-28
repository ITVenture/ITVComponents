# BUG (PRE197): Anonyme Freigabe-Links laufen auf Blazor-Hosts mit `UseTenantPathPrefix()` immer in einen 404 — und zwar lautlos

> Gemeldet aus **MLMManager** (Blazor Server/WASM-Auto, `TenantSource.PathSegment`, Toolkit
> `5.0.0-PRE197`), beim ersten echten Einsatz der SharedAssets aus Leitfaden §50–§56.
> Die Verdrahtung des Hosts folgt §50.2 wortwörtlich; die Reihenfolge in `Program.cs` ist korrekt.

## Übersicht

| | |
|---|---|
| **Betroffen** | `ITVComponents.WebCoreToolkit.Blazor/Security/TenantPathPrefixMiddleware.cs`, Zeile 88-94 (Durchlass für nicht angemeldete Anfragen) im Zusammenspiel mit `ITVComponents.WebCoreToolkit/WebPartInit.cs:29-39` (`Shared-Asset-Key` landet nur in der `DefaultPolicy`) |
| **Kern** | Der anonyme Asset-Prinzipal entsteht erst in `UseAuthorization`. `UseTenantPathPrefix()` läuft davor und sieht deshalb einen anonymen Benutzer — es lässt den Pfad **ungestrippt** durch. Das Mandanten-Segment bleibt in `Request.Path` stehen, das Routing findet keinen Endpunkt. |
| **Folge** | Jeder **anonyme** Freigabe-Link (`/~{key}.{token}/{tenant}/…`) endet in 404, bevor irgendeine Asset-Logik läuft. `SharedAssetAccesses` bleibt leer, es gibt keinen Audit-Eintrag und **keine einzige Logzeile**, die auf Assets hindeutet. |
| **Nicht betroffen** | Links für **angemeldete** Empfänger. Dort liefert das Identity-Cookie den Prinzipal, `AssetDrivenClaimsTransformation` hängt `FixedUserScope` an, `AssetSecurityRepository` macht den Asset-Mandanten eligible — die Middleware strippt und die Seite läuft. |
| **Nicht betroffen** | MVC-Hosts ohne `UseTenantPathPrefix()`. Dort ist der Mandant ein Routenwert und der Constraint fragt `PermissionScopeExists`, was `AssetSecurityRepository` zum Routing-Zeitpunkt beantwortet. |

## Reproduktion

Host-Konfiguration (alles nach Leitfaden):

```csharp
// Infrastructure/DependencyInjection.cs
services.AddAuthentication(options =>
{
    options.DefaultScheme       = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
}).AddIdentityCookies();

// Program.cs
var webPartAuthBuilder = builder.Services.AddAuthentication();
webPartManager.RegisterAuthenticationSchemes(webPartAuthBuilder);   // registriert Shared-Asset-Key
builder.Services.AddAuthorization(op => webPartManager.CustomObjectConfig(op));

app.UseSharedAssetPath();          // <- §50.2, ganz vorne
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseAuthentication();
app.UseTenantPathPrefix();
app.UseRouting();
app.UseAuthorization();
```

WebParts: `"UseSharedAssets": true`, WebPart `ITVComponents.WebCoreToolkit.Extras.dll`
(`AnonymousAssetShares`, `MaxAnonymousLinkAge: 365`).

Daten:

```
AssetTemplates:            Name "Details", SystemKey "UserDetail",
                           RequiredFeature MLM_Core, RequiredPermission Navigate
AssetTemplatePathFilters:  ^/CustomerCare/Customers/\d+$
SharedAssets:              AssetKey f69c…e06b, TenantId 1 (ADM),
                           RootPath /CustomerCare/Customers/3
```

Ablauf: `<ShareButton />` erscheint auf `/ADM/CustomerCare/Customers/3`, der Dialog legt die Freigabe
an, **Haken „anonym" gesetzt**, der Link-Dialog zeigt den Link. Aufruf in einem privaten Fenster:

```
https://<host>/~ZjY5YzQ1Y2Y2MDQwNDNjZmEwZGRjZmZkY2IxMmUwNmI.<token>/ADM/CustomerCare/Customers/3
      -> 404, ausgeliefert als die /not-found-Seite des Hosts
```

`SharedAssetAccesses`: **0 Zeilen**. Konsole: keine Zeile aus `SharedAssetPathMiddleware`, keine aus
`TenantPathPrefixMiddleware`, keine aus `AnonymousAssetAuthenticationHandler`.

## Root Cause

Die Kette, Schritt für Schritt:

1. **`SharedAssetPathMiddleware`** erkennt `~ZjY5…`, legt Schlüssel und Token in `HttpContext.Items`
   und verschiebt den Abschnitt nach `PathBase`. Danach:
   `PathBase = /~ZjY5…`, `Path = /ADM/CustomerCare/Customers/3`. **Korrekt.**

2. **`app.UseAuthentication()`** authentifiziert mit dem *DefaultAuthenticateScheme*, und das ist
   `IdentityConstants.ApplicationScheme`. Der Besucher hat kein Cookie → `context.User` bleibt
   anonym. `Shared-Asset-Key` wird hier **nicht** aufgerufen.

3. Das Schema ist zwar registriert (`Extras/AnonymousAssetAccess/WebPartInit.cs:38-46`,
   `auth.AddAnonymousAssetSupport(...)`), landet aber über den Shared-Heap-Eintrag `SignInSchemes`
   ausschliesslich in der **DefaultPolicy**:

   ```csharp
   // ITVComponents.WebCoreToolkit/WebPartInit.cs:29-39
   [CustomConfigurator(typeof(AuthorizationOptions))]
   public static void ConfigureAuthenticationTypes(AuthorizationOptions op, [SharedObjectHeap]ISharedObjHeap sharedObjects)
   {
       var l = sharedObjects.Property<List<string>>("SignInSchemes", true);
       if (l.Value.Count != 0)
       {
           var policy = new AuthorizationPolicyBuilder(op.DefaultPolicy);
           l.Value.ForEach(policy.AuthenticationSchemes.Add);
           op.DefaultPolicy = policy.Build();
       }
   }
   ```

   Eine Policy mit `AuthenticationSchemes` wird von der **`AuthorizationMiddleware`** ausgewertet —
   also in `app.UseAuthorization()`, **nach** `UseRouting`.

4. **`TenantPathPrefixMiddleware`** läuft dazwischen und nimmt für den anonymen Benutzer diesen Zweig:

   ```csharp
   // TenantPathPrefixMiddleware.cs:88-94
   var user = context.User;
   if (user?.Identity == null || !user.Identity.IsAuthenticated)
   {
       // Let downstream [Authorize] / authentication challenge decide; the segment will be
       // re-validated after sign-in when the user lands here again.
       await next(context);
       return;
   }
   ```

   Der Kommentar beschreibt die Absicht präzise — und sie stimmt für den Normalfall: ein nicht
   angemeldeter Besucher soll auf die Anmeldung geschickt werden, und danach kommt er wieder hier
   vorbei. Nur: **das Segment wird dabei nicht gestrippt.**

5. **`UseRouting`** sucht folglich nach `/ADM/CustomerCare/Customers/3`. Kein `@page` passt darauf
   (die Seite ist `@page "/CustomerCare/Customers/{CustomerId:int}"`). 404.

6. `UseStatusCodePagesWithReExecute("/not-found")` rendert die Fehlerseite. `UseAuthorization` —
   und damit die DefaultPolicy mit `Shared-Asset-Key` — wird nie erreicht, weil es keinen Endpunkt
   gibt, auf den eine Policy anzuwenden wäre.

Der anonyme Prinzipal entsteht also **eine Stufe zu spät für die Instanz, die ihn braucht**.

## Was den Fall besonders teuer macht: er ist lautlos

§50.2 verspricht ausdrücklich:

> **Vergesst ihr es, sagt es das Log.** Sieht das Anmeldeschema eine Anfrage mit unbearbeitetem
> Asset-Abschnitt, schreibt es einmal je Prozess ein `LogError` mit der richtigen Reihenfolge.
> Stilles Nichtstun gibt es hier nicht.

Diese Zusicherung greift hier nicht — und zwar aus einem systematischen Grund:
`WarnAboutPipelineOrder()` sitzt in `AnonymousAssetAuthenticationHandler.HandleAuthenticateAsync()`
(`AnonymousAssetAuthenticationHandler.cs:52-57`). Der Handler wird in dieser Konstellation **nie
aufgerufen**. Die Diagnose-Hilfe für „der Link tut einfach nichts" ist genau in dem Fall stumm, in
dem der Link einfach nichts tut.

Dazu kommt: `TenantPathPrefixMiddleware` protokolliert den Durchlass in Zeile 88-94 gar nicht, und
der spätere 404 kommt vom Routing, das über Assets nichts weiss. Es bleibt buchstäblich **keine
Spur** — nicht in der Konsole, nicht in `SharedAssetAccesses`. Wir haben den Fall nur gefunden, indem
wir die Pipeline von Hand nachgelesen haben.

## Fix-Vorschläge

Drei Wege, absteigend nach unserer Präferenz:

### A) `TenantPathPrefixMiddleware` holt den Asset-Prinzipal selbst (bevorzugt)

Wenn die Anfrage einen Asset-Abschnitt trägt (`SharedAssetPathMiddleware.HasRun(context)` — die
Methode existiert bereits und ist genau für solche Unterscheidungen dokumentiert), vor der
Mandantenprüfung explizit authentifizieren:

```csharp
var user = context.User;
if ((user?.Identity == null || !user.Identity.IsAuthenticated)
    && SharedAssetPathMiddleware.HasRun(context))
{
    var assetResult = await context.AuthenticateAsync(assetSchemeName);
    if (assetResult.Succeeded)
    {
        context.User = user = assetResult.Principal;
    }
}
```

Vorteile: eine Stelle, im selben Assembly wie das Problem; kein Host muss etwas wissen; der Weg
„Asset im Pfad" bleibt in sich geschlossen. Der Schema-Name muss konfigurierbar sein, damit ein Host
mit eigenem Namen (`WebPartOptions.AuthenticationType`) nicht ausfällt — die
`ScopedPermissionScopeOptions` wären der naheliegende Ort dafür.

Zu prüfen bei der Umsetzung: `Blazor` referenziert `Extras` heute nicht. Entweder wandert die
Schema-Konstante nach `WebCoreToolkit` (wo `SharedAssetPathMiddleware` ohnehin liegt), oder die
Middleware bekommt den Namen rein als konfigurierten String ohne Typbezug.

### B) Der Host baut ein Policy-Schema mit `ForwardDefaultSelector`

Technisch lösbar, aber:

- Es steht **nirgends im Leitfaden**. §50.2 nennt vier Reihenfolge-Bedingungen und den Satz „Die
  Dienste kommen weiterhin über `WebPartInitOptions.UseSharedAssets`" — von Schema-Komposition ist
  keine Rede.
- Es ist für jeden Blazor-Host mit Mandant-im-Pfad **dieselbe** Verdrahtung. Etwas, das in jedem Host
  gleich aussieht, gehört nicht in jeden Host.
- Es kollidiert mit `AddIdentityCookies()`: der Host müsste `DefaultScheme` auf ein eigenes
  Policy-Schema umbiegen und das Identity-Cookie zum Ziel des Selectors machen. Das ist genau die
  Sorte Startup-Umbau, bei der ein Nebeneffekt auf die Anmeldung wahrscheinlicher ist als der Nutzen.

Falls ihr euch dafür entscheidet, wäre ein fertiger Helfer (`AddAssetAwareDefaultScheme(<inner>)`)
plus ein Absatz in §50.2 das Minimum.

### C) Mindestens: den Fall hörbar machen

Unabhängig von A/B — und selbst wenn ihr das Verhalten als „so gedacht" einstuft — sollte
`TenantPathPrefixMiddleware` protokollieren, wenn sie eine **nicht angemeldete Anfrage mit
Asset-Abschnitt** durchlässt. Ein `LogWarning` mit Pfad, Asset-Abschnitt und dem Hinweis auf die
Schema-Komposition macht aus einem stummen 404 eine Ein-Zeilen-Diagnose. Das ist die billigste
Massnahme im ganzen Report und hätte uns hier schon gereicht.

## Abgrenzung / was ausdrücklich funktioniert

Damit die Suche nicht in die falsche Richtung läuft — das haben wir am laufenden System bzw. am Code
geprüft:

- **Reihenfolge in `Program.cs`**: korrekt nach §50.2. `SharedAssetPathMiddleware` läuft, strippt und
  legt Schlüssel und Token in `Items`. Nicht die Ursache.
- **`ISecurityRepository` über `GetAssetSecurityRepository`**: übernimmt das WebPart
  (`CoreIdentityTree/Extensions/DependencyExtensions.cs:50`). Nicht die Ursache.
- **Linkerzeugung**: `BuildLink` (`SharedAssetInfoProvider.cs:422-436`) setzt Asset-Abschnitt **und**
  Mandant korrekt, in dieser Reihenfolge. `RootPath` ist präfixfrei gespeichert
  (`/CustomerCare/Customers/3`), wie §50.7 es verlangt.
- **Das WebPart `AnonymousAssetShares`** ist aktiv, `IGetAnonymousAssetQuery` und das Schema sind
  registriert. Der Handler wird nur nie gefragt.
- **Nicht-anonyme Links** (Empfänger angemeldet) sind nach unserer Lesart des Codes in Ordnung.
  Wir haben sie noch **nicht** am laufenden System durchgespielt — das holen wir nach und melden es,
  falls sich dabei etwas anderes zeigt.

## Nebenbefund (kein Fehler, aber eine Falle beim Einrichten)

Vor diesem Fall standen wir eine Weile davor, dass `<ShareButton />` überhaupt nicht erschien.
Ursache war eine **abgelaufene** `TenantFeatureActivations`-Zeile für das in der Vorlage hinterlegte
`RequiredFeature`: `GetFeatures` filtert hart auf das Aktivierungsfenster
(`TreeShared/Security/DbSecurityRepository.cs:1297-1298`), die Vorlage fällt aus
`GetEligibleShares` heraus, und der Knopf rendert fail-silent nichts.

Das ist so gebaut und richtig so. Es wäre aber hilfreich, wenn §54.1 den Satz enthielte, dass ein
abgelaufenes Feature (nicht nur ein fehlendes) den Knopf verschwinden lässt — im Datenbestand sieht
die Zeile auf den ersten Blick vorhanden aus, und `Features.Enabled` steht daneben und meint etwas
anderes.

## Umgebung

| | |
|---|---|
| Toolkit | `5.0.0-PRE197` (31 PackageReferences über 4 csprojs) |
| Host | .NET 10, Blazor Web App (Server + WASM Auto), ASP.NET Core Identity |
| Mandanten | `TenantSource.PathSegment`, `app.UseTenantPathPrefix()`, TreeTenants |
| Sicherheitskontext | `AspNetTreeSecurityContext` (CoreIdentityTree), SQL Server LocalDB |

## Umsetzung

Umgesetzt ist **Weg A**, zusammen mit **C**. Beides in PRE198.

`TenantPathPrefixMiddleware` holt den Asset-Prinzipal selbst, sobald `SharedAssetPathMiddleware.HasRun`
für die Anfrage gilt und sie unangemeldet ankommt; gelingt das, läuft der Rest der Middleware — und
alles dahinter — als gewöhnliche angemeldete Anfrage, samt Strippen des Mandanten-Segments. Die
Asset-Claims kommen dabei aus der Claims-Transformation, die `AuthenticateAsync` ohnehin ausführt.

Der Schema-Name steht in `ScopedPermissionScopeOptions.SharedAssetAuthenticationScheme`, Vorgabe
`Shared-Asset-Key`. Bewusst als Zeichenkette: `Blazor` referenziert `Extras` nicht, und ein Host ohne
anonyme Links soll es auch nicht müssen. Ist das Schema nicht registriert, passiert nichts — die
Prüfung läuft über `IAuthenticationSchemeProvider`, bevor authentifiziert wird, sonst wäre der
fehlende Handler eine Ausnahme statt eines Nicht-Ereignisses.

**Weg B wurde nicht gebaut** — aus der Begründung des Reports selbst: was in jedem Blazor-Host mit
Mandant im Pfad gleich aussieht, gehört nicht in jeden Host.

Zum lautlosen Teil, der hier der teurere war: die Middleware schweigt in keinem der drei Fälle mehr.

- **Unbearbeiteter Asset-Abschnitt** (`UseSharedAssetPath()` fehlt oder steht zu spät): dieselbe
  `LogError`-Meldung zur Pipeline-Reihenfolge, die bisher nur der Handler kannte — der in genau dieser
  Lage nie gerufen wird. Einmal je Prozess, wie dort.
- **Schema nicht registriert**: `LogWarning` mit dem gesuchten Namen und dem Hinweis, dass Links für
  angemeldete Empfänger davon nicht betroffen sind.
- **Schema liefert keinen Prinzipal** (abgelaufen, zurückgezogen, falsches Token): `LogWarning` mit
  dem Grund aus `AuthenticateResult.Failure`.

Tests: 4 neue in `TenantPathPrefixMiddlewareTests` — der anonyme Asset-Aufruf wird hier authentifiziert
und der Mandant gestrippt, das nicht registrierte Schema, das ergebnislose Schema, und der
unbearbeitete Abschnitt. 112 Tests wurden 116, alle grün.

Der **Nebenbefund** steht jetzt in §54.1: ein abgelaufenes `RequiredFeature` lässt den Knopf ebenso
verschwinden wie ein fehlendes, und `Features.Enabled` daneben meint den Katalogeintrag, nicht die
Aktivierung. Der Fall selbst ist in §50.2 beschrieben — für den Host ändert sich an `Program.cs`
nichts.

Unabhängig davon aufgefallen und mitbehoben: `<ShareButton />` entschied nur beim Setzen seiner
Parameter, welche Vorlagen passen. Im Mantel ändern die sich beim Seitenwechsel nie, also erschien der
Knopf erst nach F5 und blieb beim Wegnavigieren stehen. Er hängt jetzt am Navigationsereignis, wie der
Hilfe-Knopf.
