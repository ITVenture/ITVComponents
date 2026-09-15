> **BEHOBEN in PRE231.** Umgesetzt wurde Vorschlag **A** (der Handler löst Ticket-Abschnitte selbst
> auf) zusammen mit **B** (die stillen Stellen sprechen). Zwei Abweichungen vom Vorschlag, beide in
> Leitfaden §55.8 begründet:
>
> 1. **Der Prinzipal heisst `#ANONYMOUS#`, nicht `info.TicketNonce`.** Drei Stellen unterscheiden den
>    anonymen Besucher genau an diesem Namen von einem echten Benutzer (`KnownVisitor`,
>    `SharedAssetContext`, `AssetAccessRecorder`); die Nonce hätte den zweiten Durchlauf den eigenen
>    Besucher für einen Angemeldeten halten lassen. Die Frage nach der **Auditierung** beantwortet der
>    bestehende Code bereits: `AssetAccessEntry.TicketNonce` ist da und wird für Tickets gefüllt.
> 2. **Derselbe Fehler steckte ein zweites Mal in `AssetDrivenClaimsTransformation`**, die die Rechte
>    über `GetAssetInfo(AssetKey)` holte — für ein Ticket `null`. Nur Vorschlag A hätte den Besucher
>    hereingelassen und ihn dann am Berechtigungsriegel scheitern lassen: gleiches Symptom, neue
>    Ursache. Beide Schichten unterscheiden jetzt nach `SegmentKind`.
>
> Zusätzlich trägt ein Ticket seit PRE231 seinen Mandanten **in der Nutzlast** und wird gegen den Namen
> aus der URL geprüft (§55.2/§55.3) — ohne das hing die Mandantenbindung daran, ob der Mandant
> überhaupt ein `TenantPassword` hat. **Folge: alle ausgegebenen Tickets müssen neu erzeugt werden.**
>
> Die vier Nebenbefunde stehen als §55.9 im Leitfaden. Regressionstests:
> `ITVComponents.WebCoreToolkit.Tests/AdHocTicketAuthenticationTests.cs`.

# BUG (PRE230): Ad-hoc-Tickets melden anonyme Besucher nie an — `Shared-Asset-Key` kennt nur gespeicherte Freigaben

> Gemeldet aus **MiniStore** (Blazor Web App, .NET 10, `TenantSource.PathSegment`, TreeTenants,
> PostgreSQL, Toolkit `5.0.0-PRE230`), beim ersten Durchstich des Self-Checkout.
> Die Reihenfolge in `Program.cs` folgt §50.2; anonyme Links auf **gespeicherte** Freigaben
> funktionieren in derselben Anwendung einwandfrei.

## Übersicht

| | |
|---|---|
| **Betroffen** | `ITVComponents.WebCoreToolkit.Extras/AnonymousAssetAccess/AnonymousAssetAuthenticationHandler.cs:79-85` im Zusammenspiel mit `ITVComponents.WebCoreToolkit/Security/SharedAssets/SharedAssetContext.cs:498-504` |
| **Kern** | Der Handler authentifiziert ausschliesslich über `assetContext.AssetKey` + `assetContext.AccessToken`. Bei einem **Ticket**-Abschnitt sind beide `null` — `SharedAssetContext` füllt dort stattdessen `TicketTenant`/`TicketPayload`. Der Aufruf lautet also `Execute(null, null)`, liefert `null` ohne `denied`, und der Handler antwortet `NoResult`. |
| **Folge** | Jedes **anonyme Ad-hoc-Ticket** (`/~!{tenantB64}.{payload}/{tenant}/…`) endet in 404. `GetTicketInfo` wird auf diesem Weg nie gerufen — es hängt allein an `SharedAssetContext.Info` (Zeile 368) und damit an der *Argument*-Prüfung, nicht an der *Anmeldung*. |
| **Widerspruch zur erklärten Absicht** | `GetTicketInfo` setzt am Ergebnis `IsAnonymous = true` (`SharedAssetInfoProvider.cs:848`) mit dem Kommentar: *„Ein Ticket traegt sein Geheimnis selbst und kennt keine Filter — es ist per Bauart ohne Anmeldung benutzbar."* Genau diese Bauart greift nie. |
| **Nicht betroffen** | Gespeicherte anonyme Freigaben (`~{key}.{token}`). Der Handler findet sie über `IGetAnonymousAssetQuery` und stellt den Prinzipal aus. In derselben Anwendung, mit derselben Vorlage, denselben Rechten und demselben Pfadfilter. |
| **Nicht betroffen** | Ad-hoc-Tickets für **angemeldete** Empfänger. Dort liefert das Identity-Cookie den Prinzipal, und die Rechte kommen über die Claims-Transformation. |
| **Lautlos** | Der `NoResult`-Zweig in Zeile 81-85 protokolliert nichts. `GetTicketInfo` protokolliert jeden seiner Fehlerpfade — wird aber nicht erreicht. Übrig bleibt eine Meldung von `TenantPathPrefixMiddleware`, die vier Ursachen nennt, von denen **keine** zutrifft. |

## Reproduktion

Zwei Vorlagen, bewusst als Paar: der QR am Ladeneingang darf nur *starten*, alles Weitere hängt an
einem Ticket auf genau einen Auftrag.

```
AssetTemplates
  SystemKey     pos-start                     pos-checkout
  AllowAdHoc    false                         true
  MaxAdHoc                                    60
  ArgEnforce    0 (None)                      2 (Strict)
  ValidityRule  —                             order-open
  Required      Checkout.Use / MiniStore.Core  Checkout.Use / MiniStore.Core
  Grants        Checkout.Use / MiniStore.Core  Checkout.Use / MiniStore.Core
  PathFilter    ^/checkout/start$             ^/checkout/\d+$

AssetTemplateArguments (pos-checkout)
  orderId, ArgumentType 1 (Int), Required
```

Ablauf:

1. `<ShareButton Path="/checkout/start" />` legt aus `pos-start` eine **gespeicherte, anonyme**
   Freigabe an. Der Link funktioniert: im privaten Fenster wird der Prinzipal ausgestellt, die
   Seite `/checkout/start` läuft.
2. Diese Seite ist ein Bootstrapper. Sie legt einen Auftrag an und gibt darauf ein **Ad-hoc-Ticket**
   aus:

   ```csharp
   var request = new ShareRequestViewModel
   {
       RequestPath    = "/checkout/12",
       TemplateKey    = "pos-checkout",
       ArgumentValues = { ["orderId"] = "12" },
       AdHoc          = true,
       Anonymous      = true,
       LifetimeMinutes = 60
   };
   var origin = new Uri(Navigation.BaseUri).GetLeftPart(UriPartial.Authority);
   var result = await ShareHandler.CreateAsync(UserProvider.User, request, origin);
   ```

   `result.Success` ist `true`, der Link ist wohlgeformt:

   ```
   https://<host>/~!QURN.IBDvQlKo…/ADM/checkout/12
   ```

   (`QURN` = Base64Url von `ADM`, wie `SharedAssetPath.BuildTicketSegment` es baut.)
3. Aufruf dieses Links — im selben anonymen Kontext oder in einem frischen privaten Fenster:

   ```
   -> 404, ausgeliefert als /not-found
   ```

Im Log steht dazu genau eine Zeile:

```
warn: ITVComponents.WebCoreToolkit.Blazor.Security.TenantPathPrefixMiddleware[0]
      TenantPathPrefix: the 'Shared-Asset-Key' scheme established no principal for
      /ADM/checkout/12 (no result). The link is expired, revoked, carries a wrong access token,
      or is one for signed-in recipients.
```

Keine der vier genannten Ursachen trifft zu: das Ticket ist Sekunden alt, `NotAfter` liegt 60
Minuten in der Zukunft, `RevokedAssetTickets` ist leer, und es ist ausdrücklich als anonym
ausgestellt.

## Root Cause

1. **`SharedAssetPathMiddleware`** erkennt den Abschnitt und legt ihn ab. `SharedAssetContext.Resolve`
   unterscheidet dann zwei Fälle (`SharedAssetContext.cs:493-504`):

   ```csharp
   if (/* Ticket */)
   {
       segmentKind   = AssetSegmentKind.Ticket;   // :453
       ticketTenant  = parsed.TenantName;         // :498
       ticketPayload = parsed.Payload;            // :499
       // assetKey und accessToken bleiben null
   }
   else
   {
       assetKey    = parsed.AssetKey;             // :503
       accessToken = parsed.AccessToken;          // :504
   }
   ```

   `HasAsset` (`:375-380`) ist für beide Fälle `true` — es prüft `assetKey` **oder** `ticketPayload`.

2. **`AnonymousAssetAuthenticationHandler.HandleAuthenticateAsync`** kommt damit über die ersten
   beiden Hürden (`HasAsset` ist gesetzt, kein angemeldeter Besucher) und greift dann zu:

   ```csharp
   // AnonymousAssetAuthenticationHandler.cs:79
   var existingAsset = getAnonymousAssetQuery.Execute(
       assetContext.AssetKey, assetContext.AccessToken, out bool denied);

   if (existingAsset == null && !denied)
   {
       // Ein Asset ohne Zugangs-Token ist ein Link fuer angemeldete Empfaenger - dieses Schema ist
       // dafuer nicht zustaendig, die Claims-Transformation uebernimmt.
       return Task.FromResult(AuthenticateResult.NoResult());   // :85
   }
   ```

   Für ein Ticket sind beide Argumente `null`. Die Abfrage findet nichts, `denied` bleibt `false` —
   und der Kommentar an Zeile 82-84 beschreibt den Fall, den er *meint*, nicht den, der hier
   eintritt. Ein Ticket ist kein „Link für angemeldete Empfänger"; es ist der einzige Fall, in dem
   der Zugangs-Token **nicht** in der Datenbank steht, weil die Freigabe nirgends steht.

3. **`GetTicketInfo`** (`SharedAssetInfoProvider.cs:752`) könnte die Frage beantworten — es liest die
   Nutzlast, prüft Frist, Widerruf, Vorlage, Pfadmuster und Gültigkeitsregel, und setzt am Ergebnis
   `IsAnonymous = true` (`:848`). Es wird auf dem Anmeldeweg nur nie gefragt: der einzige Aufrufer im
   ganzen Repository ist `SharedAssetContext.Info` (`:368`), und der läuft erst, wenn eine Seite ihre
   Argumente bestätigt — also **nach** der Anmeldung, die es nie gibt.

Der Prinzipal eines Tickets entsteht also nirgends.

## Was den Fall teuer macht: er ist lautlos — und die eine Meldung führt in die Irre

Der `NoResult` in Zeile 85 protokolliert nichts. Das ist für den Fall, den der Kommentar meint,
richtig — ein Link für angemeldete Empfänger ist ein Nicht-Ereignis für dieses Schema. Für ein
Ticket ist es ein stiller Abbruch am einzigen Ort, der ihn hätte auflösen können.

Gleichzeitig protokolliert `GetTicketInfo` **jeden** seiner Fehlerpfade einzeln und sehr gut
(„gilt noch nicht", „ist abgelaufen", „wurde zurueckgezogen", „Vorlage fehlt", „gilt an dieser
Stelle nicht", Gültigkeitsregel). Wir haben über mehrere Runden genau diese Meldungen erwartet und
ihr Ausbleiben als Hinweis gelesen, dass die jeweilige Prüfung *bestanden* wurde — bis auffiel, dass
die Methode gar nicht läuft.

Die einzige Meldung, die es gibt, kommt aus `TenantPathPrefixMiddleware` und nennt vier Ursachen
(abgelaufen, zurückgezogen, falsches Token, für Angemeldete). Die tatsächliche — *dieses Schema kann
Tickets nicht* — ist nicht darunter und lässt sich aus der Meldung auch nicht erschliessen.

Nebenbei fiel dabei eine zweite stille Stelle auf: der erste Guard in `GetTicketInfo`

```csharp
// SharedAssetInfoProvider.cs:754
if (ImpersonationDeactivated || string.IsNullOrEmpty(tenantName) || string.IsNullOrEmpty(payload))
{
    return null;
}
```

kehrt ebenfalls ohne Logzeile zurück. Sie war hier nicht die Ursache, hat uns aber Zeit gekostet,
weil sie als einziger stiller Pfad *innerhalb* der Methode lange der beste Kandidat war.

## Fix-Vorschläge

### A) Der Handler löst Ticket-Abschnitte selbst auf (bevorzugt)

Vor dem Zweig für gespeicherte Freigaben:

```csharp
if (assetContext.SegmentKind == AssetSegmentKind.Ticket)
{
    // CurrentAsset loest ueber GetTicketInfo auf; IsAnonymous ist dort bereits true.
    var info = assetContext.CurrentAsset;
    if (info == null)
    {
        // GetTicketInfo hat den Grund bereits protokolliert.
        return Task.FromResult(AuthenticateResult.Fail("The ad-hoc ticket is not valid."));
    }

    var identity  = new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, info.TicketNonce)], Options.AuthenticationType);
    var principal = new ClaimsPrincipal(identity);
    var ticket    = new AuthenticationTicket(principal, Options.Scheme);
    ticket.Properties.SetString("##ANONYMOUS_ASSET", "true");
    return Task.FromResult(AuthenticateResult.Success(ticket));
}
```

`ISharedAssetContext` stellt alles Nötige bereits bereit — `SegmentKind` (`:41`), `TicketTenant`
(`:47`), `TicketPayload` (`:52`), `CurrentAsset` (`:58`). Es braucht keine neue Schnittstelle und
keinen Zugriff auf `Extras`-Interna.

Zu klären bei der Umsetzung:

* **Name des Prinzipals.** Bei gespeicherten Freigaben ist es `existingAsset.Key`. Für ein Ticket
  wäre `info.TicketNonce` das Gegenstück — es ist ohnehin der Schlüssel, unter dem ein Widerruf
  läuft (`RevokedAssetTickets.Nonce`).
* **Auditierung.** `AuditMode` steht auf dem Ticket-Ergebnis. Ob ein Ticket in
  `SharedAssetAccesses` auftauchen soll, obwohl es keine `SharedAssets`-Zeile gibt, ist eine
  Entscheidung, die wir nicht treffen können.
* **`KnownVisitor()`** (`:60-76`) bleibt unverändert davor: wer angemeldet ist, bleibt er selbst.
  Das gilt für Tickets genauso.

### B) Mindestens: den Fall hörbar machen

Unabhängig von A — und selbst wenn ihr entscheidet, dass Tickets anonym *nicht* funktionieren
sollen: der Zweig in Zeile 81-85 sollte unterscheiden, ob er wegen eines fehlenden Zugangs-Tokens
(gemeinter Fall) oder wegen eines **Ticket-Abschnitts** (dieser Fall) aussteigt, und für Letzteren
eine Zeile schreiben. Und die Meldung von `TenantPathPrefixMiddleware` sollte ihre Ursachenliste um
den Fall ergänzen.

Das ist die billigste Massnahme im ganzen Report und hätte uns hier fünf Runden erspart.

### C) Falls Tickets anonym nicht unterstützt werden sollen

Dann wäre die Kombination `AllowAdHoc = true` **und** anonym ein Konfigurationsfehler, den
`CreateAdHocTicket` (`:677`) beim Ausstellen abweisen sollte, statt einen Link zu liefern, der
nachweislich nie funktionieren kann. Und der Kommentar an `IsAnonymous = true` (`:848`) gehört
korrigiert, weil er heute das Gegenteil zusagt.

## Abgrenzung / was ausdrücklich geprüft wurde

Damit die Analyse nicht in die falsche Richtung läuft — alles am laufenden System nachgewiesen:

* **Linkerzeugung**: korrekt. `CreateAdHocTicket` liefert `{origin}{prefix}{rootPath}`, der Abschnitt
  entspricht `BuildTicketSegment`, das Mandantensegment steht genau einmal darin.
* **Rechte**: `Checkout.Use` ist der Vorlage als `RequiredPermission` **und** über
  `AssetTemplateGrants` als Gewährung zugeordnet.
* **Feature**: `MiniStore.Core` ebenso, über `AssetTemplates.FeatureId` **und**
  `AssetTemplateFeatures`. (Dass diese zweite Hälfte nötig ist, war uns anfangs nicht klar; der
  anonyme Besucher bringt nur mit, was die Vorlage gewährt. Das ist stimmig, steht aber nirgends
  zusammenhängend — ein Satz im Leitfaden wäre hilfreich.)
* **Vertrauensstellung**: ``SharedAssetInfoProvider`47`` steht in `TrustedFullAccessComponents` mit
  `ShowAllTenants`. **Nachtrag (PRE242):** die Zahl hinter dem Backtick ist die Stelligkeit des Typs
  und kein Teil seines Namens — sie ändert sich, sobald der Sicherheitskontext eine Entität bekommt
  oder verliert. Seit PRE240 heisst der Typ ``SharedAssetInfoProvider`46``. Wer diesen Eintrag
  abschreibt, nimmt die Stelligkeit **aus der eigenen Assembly**, nicht aus diesem Dokument; siehe
  Leitfaden §67. Ohne den Eintrag kam je Anfrage *„No Trust Configuration found for the caller …
  No special permissions will be granted."* — 101-mal im Log. Nach dem Eintrag sind diese Warnungen
  weg, das Verhalten ist unverändert. **Nicht die Ursache**, aber erwähnenswert: MLM führt diesen
  Eintrag nicht, weil dort Freigaben von angemeldeten Benutzern geöffnet werden.
* **Verschlüsselung**: `PasswordSecurity.InitializeAes` steht ganz oben in `Program.cs`.
  `Tenants.TenantPassword` ist für `ADM` leer, der anwendungsweite Schlüssel greift.
* **Gültigkeitsregel** `order-open`: registriert, und sie protokolliert jeden Fehlschlag. Sie hat
  nie gemeldet — konsistent damit, dass sie nie gerufen wurde.
* **Gespeicherte anonyme Freigabe auf derselben Vorlagen-Familie** (`pos-start`): funktioniert. Das
  ist der Vergleichsfall, der die Abgrenzung trägt.

## Nebenbefund (kein Fehler, aber eine Falle)

`ISharedAssetAdminHandler.CreateAsync(user, request, origin)` hängt Segmente an `origin` **an**. Wird
der Link auf einer Seite erzeugt, die selbst hinter einer Freigabe läuft, und übergibt man dort
`NavigationManager.BaseUri`, entsteht eine Adresse mit zwei Freigabe- und zwei Mandantensegmenten.
`<ShareButton />` macht es richtig (`new Uri(Navigation.BaseUri).GetLeftPart(UriPartial.Authority)`),
aber das steht nur im Code des Knopfes.

Dazu: navigiert man aus dem Circuit auf eine **andere** Freigabe, greift `TenantUrlGuard` ein und
hält das aktuelle Präfix fest — auch gegen `NavigateTo(link, forceLoad: true)`. Das Ergebnis sieht
identisch aus wie der `origin`-Fehler (doppeltes Präfix, 404), hat aber eine andere Ursache; der
Unterschied steht nur im Navigations-Log des Circuits. Wir gehen jetzt über
`window.location.replace`. Ein Satz im Leitfaden, dass ein Freigabe-Link nicht über den Circuit
angesteuert werden sollte, würde das abkürzen.

## Umgebung

| | |
|---|---|
| Toolkit | `5.0.0-PRE230` |
| Host | .NET 10, Blazor Web App (Server + WASM Auto), ASP.NET Core Identity, MudBlazor 9 |
| Mandanten | `TenantSource.PathSegment`, `app.UseTenantPathPrefix()`, TreeTenants |
| Sicherheitskontext | `AspNetTreeSecurityContext` (CoreIdentityTree) |
| Datenbank | PostgreSQL 17 (Npgsql), EF Core 10 |
| WebParts | `Extras` mit `AnonymousAssetShares` (`MaxAnonymousLinkAge: 365`), `UseSharedAssets: true` |
