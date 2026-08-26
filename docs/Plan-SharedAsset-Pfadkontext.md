# Plan: Geteilte Assets als Pfad-Kontext

Status: **umgesetzt** (Phasen 1-5), Stand 2026-08-26, Zweig Future_10. Builds gruen, 73 Tests gruen.
Was der Host tun muss, steht in `docs/Migration-Future_10-MLM.md` Abschnitt 50.

## 1. Ausgangslage

Das Toolkit kann heute schon, was hier gebraucht wird — nur nicht an der richtigen Stelle in der URL
und nicht in Blazor.

**Der Mechanismus** (Ist-Zustand, alles vorhanden):

| Baustein | Datei | was er tut |
|---|---|---|
| Modell | `EntityFramework.TenantSecurity/Shared/Models/Base/SharedAsset.cs` | `AssetKey`, `AnonymousAccessTokenRaw`, `RootPath`, `TenantId`, `NotBefore`/`NotAfter`, `UserFilters`, `TenantFilters`, `Template` |
| Rechte | `AssetTemplate` → `Grants` / `FeatureGrants` | welche Berechtigungen und Features das Asset verleiht |
| Anonymer Zugang | `Extras/AnonymousAssetAccess/` | eigenes Authentifizierungs-Schema `Shared-Asset-Key`; macht aus Schluessel+Token einen Prinzipal namens `#ANONYMOUS#` |
| Zugangspruefung | `SharedAssetInfoProvider.AssetIsAccessible` | Benutzer-/Mandantenfilter, `#ANONYMOUS#` und `%` als Platzhalter, Gueltigkeitsfenster |
| Ortsbindung | `SharedAssetInfoProvider.VerifyRequestLocation` | das Asset gilt nur unterhalb der Pfade seines Templates |
| Uebersteuerung | `Security/ClaimsTransformation/AssetDrivenClaimsTransformation.cs` | haengt `FixedUserScope` + `FixedAssetPermission` + `FixedAssetFeature` an den Prinzipal |
| Rechte-Ersatz | `Security/SharedAssets/AssetSecurityRepository.cs` | wird per `PushRepo` obendrauf gelegt und **ersetzt** die Berechtigungen |
| Scope-Klammer | `Security/UserScopes/ResolvingPermissionScope.cs:184` | `FixedUserScope` heisst "Scope steht fest, keine Auswahl" |

Weil `FixedUserScope` den Scope setzt, stimmt auch der Mandantenfilter der Datenbank
(`SetCurrentTenantInterceptor.cs:94`) — ohne dass der Zugreifende Mitglied des Mandanten waere.

**Was das inhaltlich ist:** eine Moeglichkeit, einem beliebigen Zugreifenden — angemeldet oder nicht —
in einem **abgegrenzten Pfadbereich** genau die Rechte zu geben, die dieser Bereich braucht, und keine
anderen. Ein Gastkonto oder ein impliziter Sammelbenutzer wird dafuer nicht gebraucht.

## 2. Warum die Query-Form nicht traegt

Der Schluessel kommt heute als `?SharedAssetKey=…&__AccessToken=…`. Daraus folgen drei Grenzen:

1. **Query-Parameter ueberleben Navigation nicht.** Sobald der Benutzer innerhalb des Bereichs
   weiterklickt, ist der Schluessel weg.
2. **Der Referer-Rueckfall ist ein Notnagel.** `ServiceProviderExtensions.cs:325` und
   `AnonymousAssetAuthenticationHandler` lesen die Query notfalls aus dem `Referer`, damit
   Unterressourcen (Bilder, Dateien) den Kontext nicht verlieren. Das ist unzuverlaessig, und im
   Blazor-Circuit gibt es je Navigation ueberhaupt keinen brauchbaren Referer.
3. **Alles haengt am `HttpContext`.** `AssetDrivenClaimsTransformation` und `IsLegitSharedAssetPath`
   verlangen `IHttpContextUserProvider` — der Mechanismus ist damit MVC-only und in einem
   Blazor-Circuit schlicht nicht vorhanden.

## 3. Entscheidung: der Asset-Abschnitt in der URL

```
/~{asset}/{mandant}/rest/des/pfades
```

**Der Abschnitt steht ganz vorne — vor dem Mandanten, in beiden Welten.** Das ist keine technische
Bequemlichkeit, sondern folgt daraus, was der Abschnitt inhaltlich sagt:

> "Wer bist du?" — "Ich bin der anonyme Besucher xyz und ich darf das Asset abc benutzen."

Der Mandant faellt dabei nur **nebenbei** an: er steht am Asset (`UserScopeName`) und wird von ihm
gesetzt. Der Abschnitt beantwortet also die Identitaets- und Rechtefrage, nicht die Ortsfrage — und
Identitaet steht vor Ort. Dieselbe Reihenfolge wie im Kopf einer Anfrage: erst wer, dann wo.

Praktisch faellt daraus alles Weitere heraus: **eine** URL-Form fuer Blazor und MVC, **eine**
Registrierung in der Pipeline (Abschnitt 4), keine Positionsfallunterscheidung im Formatter
(5.4) und statische Dateien, die ohne Nachdenken stimmen (5.3).

Der Asset-Abschnitt wird — genau wie das Mandantensegment in `TenantPathPrefixMiddleware` — aus
`Request.Path` geschnitten und an `Request.PathBase` gehaengt. Das bringt drei Dinge auf einmal:

1. **Die Anwendung routet unveraendert.** Die Seite bleibt `@page "/rest/des/pfades"`.
2. **Relative Verweise bleiben von selbst im Asset-Kontext.** Blazors `NavigationManager` und jedes
   `<img src="bild.png">` erben das Praefix aus dem `<base href>`. Damit faellt der Grund fuer den
   Referer-Rueckfall weg, und der Kontext ueberlebt Navigation.
3. **Der Kontext bekommt eine harte Grenze.** Das Praefix zu verlassen heisst, den Base-Href-Raum zu
   verlassen — das ist ein Vollreload und ein neuer Circuit. Der Asset-Kontext kann nicht
   versehentlich mitwandern.

**Marker `~`.** Ein Abschnitt, der mit `~` beginnt, ist ein Asset-Abschnitt und sonst nichts. Ohne
Marker waere jeder Seitenpfad mit einem Asset-Schluessel verwechselbar; mit Marker ist die Erkennung
eindeutig und kostet einen Zeichenvergleich. `~` ist in Pfaden regulaer erlaubt und wird von keiner
Razor-Route benutzt.

**Warum das aufgeht — die Nachrechnung.** `PathBase` und `Path` muessen zusammen wieder die
aufgerufene URL ergeben; `PathBase` kann also nur ein **zusammenhaengendes Praefix** aufnehmen. Mit
dem Asset ganz vorne stapelt sich das sauber:

| Schritt | `PathBase` | `Path` |
|---|---|---|
| Anfrage | `` | `/~asset/kunde/rest` |
| nach dem Asset-Strip | `/~asset` | `/kunde/rest` |
| nach `UseTenantPathPrefix` (nur Blazor) | `/~asset/kunde` | `/rest` |
| MVC (kein Mandanten-Strip) | `/~asset` | `/kunde/rest` — die Route sieht `{mandant}` weiterhin |

Beide Zeilen ergeben zusammengesetzt wieder `/~asset/kunde/rest`. Der Mandant bleibt in MVC dort, wo
`{mandant:permissionScope}` ihn braucht, und wandert in Blazor wie gewohnt ins `PathBase`.

Der Parser nimmt den Marker damit immer im **ersten** Abschnitt — kein Suchen ueber zwei Positionen,
keine Fallunterscheidung nach Host.

**Der Abschnitt traegt alles, es braucht keine Query mehr:**

```
~{Base64Url(AssetKey)}                 -> geteiltes Asset fuer angemeldete Empfaenger
~{Base64Url(AssetKey)}.{AccessToken}   -> anonymer Zugang, Token wie bisher
```

Der Schluessel wird Base64Url-kodiert, damit ein selbst vergebener `AssetKey` (128 Zeichen laut
Modell) den Pfad nicht sprengen kann. Der `AccessToken` ist bereits Base64Url
(`DefaultAnonymousAssetUserResolver.CreateAnonymousLink`) und wird **inhaltlich nicht angefasst** —
Nutzlast und Pruefung (`ValidateAnonymousToken`) bleiben, wie sie sind. Der Punkt trennt; beide
Alphabete kennen ihn nicht.

## 4. Reihenfolge in der Pipeline

```
UseSharedAssetPath()        <- erkennt, strippt nach PathBase, legt Schluessel+Token in Items
UseStaticFiles              <- sieht /css/… , nicht /~asset/css/…
UseAuthentication           <- Shared-Asset-Schema liest aus Items statt aus der Query
UseTenantPathPrefix         <- unveraendert (nur Blazor-Hosts)
UseRouting / UseAuthorization / MapControllerRoute bzw. MapRazorComponents
```

**Eine einzige Registrierung, ganz vorne.** Das ist der zweite Gewinn der Entscheidung aus
Abschnitt 3: weil der Abschnitt vor allem anderen steht, koennen Erkennen und Strippen in einem
Schritt passieren, und der Aufruf hat nur eine sinnvolle Stelle — den Anfang der Pipeline. Die
Bedingungen, die vorher zwei Registrierungen erzwangen, sind damit alle erfuellt:

- **vor `UseAuthentication`**, weil das Asset-Schema den Schluessel dort braucht;
- **vor `UseRouting`**, weil sonst die Route ein Segment zuviel saehe;
- **vor `UseStaticFiles`**, damit Unterressourcen unter dem Praefix gefunden werden;
- **vor `UseTenantPathPrefix`**, damit die Mandanten-Middleware den Mandanten als ersten Abschnitt
  des Restpfads sieht — genau so, wie sie es ohne Asset auch tut.

Der Gewinn fuer den Mandantenweg: ab `UseAuthentication` ist der Asset-Besucher ein
**authentifizierter** Prinzipal. `TenantPathPrefixMiddleware` braucht keinen Sonderfall — sie sieht
einen Prinzipal, dessen einziger zulaessiger Scope der Mandant des Assets ist, und validiert wie
immer.

## 5. Beide Welten: Blazor und MVC

Der Mechanismus ist **nicht Blazor-gebunden** — im Gegenteil, er stammt aus dem MVC-Weg und
funktioniert dort heute schon (per Query). Die **URL-Form und die Middleware sind in beiden Welten
dieselben** (Abschnitt 3 und 4) — verschieden sind nur die Mandanten-Schranke und die Linkerzeugung.

### 5.1 Was gleich ist

Der Asset-Abschnitt steht immer im ersten Segment, wird immer von derselben einen Middleware am
Anfang der Pipeline nach `PathBase` gestrippt, und danach sieht **jeder** Host denselben Restpfad,
den er ohne Asset auch gesehen haette:

- Blazor: `/{mandant}/rest` → `TenantPathPrefixMiddleware` macht damit, was sie immer macht.
- MVC: `/{mandant}/rest` → `{mandant:permissionScope}` matcht wie immer.
- ohne Mandant im Pfad: `/rest` → der Mandant kommt aus `UserScopeName` des Assets.

Es gibt damit **keine host-abhaengige Positionsregel** und nichts, was Middleware und Linkbau
getrennt wissen muessten. Das war der Grund, den Abschnitt nach vorne zu ziehen.

### 5.2 Die Mandanten-Schranke ist eine andere — und sie passt bereits

Blazor prueft den Mandanten in `TenantPathPrefixMiddleware` gegen `GetEligibleScopes` (dort schlaegt
8.1 zu). MVC prueft ihn im Routen-Constraint gegen
`ISecurityRepository.PermissionScopeExists(name)`. Und `AssetSecurityRepository`
(`AssetSecurityRepository.cs:191`) beantwortet genau das mit
`decoratedUser.HasClaim(FixedUserScope, name)`:

> Unter einem aktiven Asset **existiert genau ein Mandant** — der des Assets. Eine tenant-geroutete
> URL mit fremdem Mandanten matcht die Route nicht und ergibt 404.

Das ist das MVC-Gegenstueck zur Scope-Schranke, und es funktioniert ohne Aenderung. **Bedingung**:
der Host muss `ISecurityRepository` ueber `GetAssetSecurityRepository` registrieren (steht so in der
Fehlermeldung von `IsLegitSharedAssetPath`), sonst liegt der Dekorator zum Routing-Zeitpunkt nicht
auf dem Stapel.

### 5.3 Linkerzeugung: MVC hat es leichter

Blazor braucht den `<base href>`; MVC braucht gar nichts. `IUrlHelper.Action`, `RedirectToAction`
und die Tag-Helper stellen `PathBase` von sich aus voran — jeder generierte Verweis traegt den
Asset-Abschnitt automatisch mit, und der Mandant kommt aus den Ambient-Routenwerten dazu.

Drei Stolpersteine bleiben:

- **`LinkGenerator`-Ueberladungen ohne `HttpContext`** setzen `PathBase` **nicht** davor. Wer sie
  benutzt, faellt aus dem Asset-Kontext.
- **Root-absolute Verweise** (`href="/…"`, `Redirect("/…")`) verlassen den Praefix-Raum — dieselbe
  Klasse wie `BUG-PRE141`, nur eine Ebene tiefer.
- **Statische Dateien**: `Url.Content("~/css/site.css")` erbt den Praefix und landet als
  `/~asset/css/site.css` bei `UseStaticFiles`. Das geht auf, weil der Strip laut Abschnitt 4 davor
  steht — mit dem Abschnitt ganz vorne ist das ohne Verrenkung moeglich. Wer trotzdem sicher gehen
  will, verweist statische Dateien **root-absolut** (`/css/…`): sie sind weder mandanten- noch
  asset-spezifisch, der Praefix hat dort nichts zu suchen.

### 5.4 `IUrlFormat`: die Platzhalter muessen den Abschnitt kennen

`UrlFormatImpl` (`Routing/Impl/UrlFormatImpl.cs:45`) fuellt heute `[permissionScope]`,
`[permissionScopeSlash]` und `[SlashPermissionScope]` aus `IPermissionScope` — und nur, wenn
`IsScopeExplicit`. Der Asset-Abschnitt fehlt dort, also faellt **jede so gebaute URL aus dem
Asset-Kontext**. Das trifft beide Welten: die Blazor-Hilfeansichten benutzen den Formatter genauso
(`HelpContentRenderer.cs:101`, `HelpNavTree.razor:30`, `ConsentDocumentLink.razor:29`) wie die
MVC-Ansichten.

Die Regel ist dieselbe wie in 5.1, eine Ebene hoeher: **was der Formatter liefern muss, haengt davon
ab, ob der Aufrufer `~` voranstellt.** Deshalb zwei Familien statt einer:

| Platzhalter | liefert | fuer |
|---|---|---|
| `[permissionScope]`, `[permissionScopeSlash]`, `[SlashPermissionScope]` | den **vollen** Praefix: `PathBase` (enthaelt den Asset-Abschnitt, in Blazor auch den Mandanten) plus den Mandanten, falls er nicht schon drin steckt | root-absolute Verwendung (`href="[SlashPermissionScope]/help/x"`) |
| neu `[scopeUnderBase]`, `[SlashScopeUnderBase]` | nur den Teil, der **nicht** schon in `PathBase` steckt — in MVC der Mandant, in Blazor nichts | Aufrufer, die `~` voranstellen |
| neu `[assetSegment]`, `[SlashAssetSegment]` | nur den Asset-Abschnitt, sonst leer | Sonderfaelle |

**Der Grund fuer die zweite Familie**: die drei bestehenden Aufrufer mit `~[SlashPermissionScope]`
(`TenantTemplateActivation.cshtml:23`, `ApiForeignKey.cshtml:14`, `MultiSelect.cshtml:12`) bekaemen
den Praefix sonst **doppelt**, weil `~` bereits auf `baseUrl` aufloest. Heute faellt das nicht auf,
weil `PathBase` bei diesen Hosts leer ist und der Mandant ein Routenwert bleibt — mit dem
Asset-Abschnitt in `PathBase` faellt es sofort auf. Die drei gehoeren auf die zweite Familie
umgestellt.

Die Reihenfolge muss der Formatter seit Abschnitt 3 **nicht** mehr wissen — sie ist fest. Was er
wissen muss, ist, **ob der Mandant schon in `PathBase` steckt** (Blazor ja, MVC nein), und das
kommt aus derselben Hilfsmethode, die auch die Middleware benutzt (`SharedAssetPath.BuildPrefix`).
Eine Quelle, sonst driften Linkbau und Strippen auseinander, und das faellt erst im Betrieb auf.

### 5.5 `ResolveUrl` und `ITVenture.Ajax.baseUrl`

Das Client-Skript loest `~/…` ueber `ITVenture.Ajax.baseUrl` auf (`lib/js/ItvGlobalScript.js:168`,
Vorgabe `"/"`), und der Host setzt diesen Wert ueber ein erzeugtes Mini-Skript.

**Bildet er ihn aus `Request.PathBase` (`Url.Content("~/")`), traegt jeder `~/`-Aufruf den
Asset-Abschnitt von selbst mit** — in MVC liegt der Abschnitt genau dort. Das ist der eigentliche
Grund, warum der MVC-Weg so glatt aufgeht: `ResolveUrl` braucht keine Zeile Aenderung.

Drei Punkte folgen daraus:

- **Der Wert muss aus `PathBase` kommen, nicht aus einer Konfiguration.** Ein fest verdrahtetes
  `"/"` bricht den Asset-Kontext genauso wie jedes virtuelle Verzeichnis. Das Toolkit sollte den
  Schnipsel selbst liefern (ein `IHtmlContent`-Helfer neben `HtmlExtensions`), statt ihn jedem Host
  zu ueberlassen — dann gibt es eine Stelle, an der er richtig ist, und nicht eine je Host.
- **Der Abschnitt gehoert zusaetzlich einzeln in den Client** (`ITVenture.Ajax.assetSegment`), fuer
  Skripte, die URLs ohne `~` zusammensetzen.
- **`ITVenture.Helpers.GetControllerName` schneidet `window.location.pathname` bei `baseUrl.length`
  ab** (`ItvGlobalScript.js:179`). Solange der Abschnitt in `baseUrl` steht, stimmt das weiterhin;
  steht er es nicht, liefert die Funktion den Asset-Abschnitt statt des Controllers. Im Repo wird
  sie derzeit nirgends aufgerufen — sie ist trotzdem der Musterfall fuer "Skript rechnet mit
  Segmentpositionen", und wer so etwas im Host hat, muss es mitziehen.

### 5.6 Was gemeinsam bleibt

`ISharedAssetContext`, die Middleware, das Auth-Schema, der Resolver, die Claims-Transformation und
`IsLegitSharedAssetPath` sind in beiden Welten dieselben — deshalb liegen sie im Kern-Paket
(Abschnitt 6). Ebenso gemeinsam: `IUrlFormat` (5.4) — der wird in beiden Welten benutzt.
Blazor-spezifisch ist **nur** `TenantBaseHref` und die Veroeffentlichung des Abschnitts im
`BlazorContextUserProvider`; MVC-spezifisch ist **nur** das Client-Skript (5.5). Der MVC-Weg braucht
von Phase 3 nichts, aber Phase 4 braucht er genauso.

**Merke fuer 8.2**: die kanonische Pfadform faellt in den beiden Welten unterschiedlich an — in
Blazor ist der Mandant schon aus `Path` heraus, in MVC steht er noch drin. Die Hilfsmethode muss ihn
also **rechnen**, nicht `Request.Path` roh weiterreichen; sonst prueft `VerifyRequestLocation` in
MVC gegen einen Pfad mit Mandantensegment und in Blazor gegen einen ohne.

## 6. Neu zu bauen

- `ITVComponents.WebCoreToolkit/Security/SharedAssets/SharedAssetPathMiddleware.cs` — Parser und
  Stripper. **Ins Kern-Paket, nicht nach Blazor**: der Mechanismus ist nicht Blazor-spezifisch, und
  die Adapter/Resolver liegen ohnehin dort. Blazor bekommt nur den `<base href>`-Teil.
- `ITVComponents.WebCoreToolkit/Security/SharedAssets/ISharedAssetContext.cs` — host-neutraler
  Zugriff auf Schluessel und Token der laufenden Anfrage (Items, mit Query als Rueckfall). Das ist
  der Vertrag, den Claims-Transformation, Resolver und `IsLegitSharedAssetPath` kuenftig benutzen,
  statt jeweils selbst in der Query zu wuehlen.
- `UseSharedAssetPath()` in `ITVComponents.WebCoreToolkit/Extensions/AppBuilderExtensions.cs` —
  **ein** Aufruf, ohne Modus-Parameter (siehe Abschnitt 4).
- Konstanten in `Global.cs`: `SharedAssetPathMarker = "~"`, `SharedAssetSegmentItemKey`,
  `SharedAssetTokenItemKey` — der Marker gehoert neben `FixedAssetRequestQueryParameter`.

### 6.1 Verdrahtung: was der WebPart uebernimmt und was nicht

**Dienste: ja.** `WebPartInitOptions.UseSharedAssets` gibt es schon; `WebPartInit.RegisterServices`
(`WebPartInit.cs:108`) haengt daran heute nur die Claims-Transformation. Der Schalter bekommt alles
dazu, was der Pfad-Kontext braucht: `ISharedAssetContext`, den erweiterten `IUrlFormat` und die
Optionen (`AcceptQuerySharedAssetKey`). Ein Host, der `UseSharedAssets` gesetzt hat, muss also
**keinen einzigen Dienst von Hand registrieren**.

**Pipeline: nein — und daran fuehrt kein Weg vorbei.** Das WebPart-System kennt Haken fuer Dienste,
Authentifizierung, MVC, Endpunkte, HealthChecks und typisierte Optionen
(`AspExtensions/Impl/*Attribute.cs`), aber **keinen fuer Middleware**. Es gibt im ganzen Toolkit nur
zwei Stellen mit `IApplicationBuilder` (`Extensions/AppBuilderExtensions.cs`,
`Blazor/Extensions/DependencyExtensions.cs`), und beide sind ausdruecklich vom Host zu rufen — so
wie `UseTenantPathPrefix()` auch. Die Reihenfolge aus Abschnitt 4 ist damit Startup-Sache des Hosts.

Einen Haken dafuer zu erfinden waere der falsche Weg: Middleware-Reihenfolge ist die eine
Konfiguration, die sich **nicht** sinnvoll aus einer Liste von WebParts ableiten laesst — sie haengt
davon ab, was der Host sonst noch in die Pipeline stellt (Statische Dateien, Lokalisierung,
Fehlerseiten). Was hilft, ist nicht Automatik, sondern **lautes Scheitern**:

- **Zu spaet registriert**: sieht das Asset-Schema in `HandleAuthenticateAsync` einen Pfad, der mit
  dem Marker beginnt, waehrend in `Items` kein Schluessel liegt, dann stand `UseSharedAssetPath()`
  hinter `UseAuthentication`. Das ergibt **einmal je Prozess** ein `LogError` mit der richtigen
  Reihenfolge — nicht stilles Nichtstun. Ein Asset-Link, der wortlos als normale URL behandelt wird,
  ist genau die Sorte Fehler, die Stunden kostet.
- **Gar nicht registriert**: dieselbe Probe greift, weil der Marker dann bis in die Route
  durchschlaegt und dort 404 ergibt — die Meldung nennt den fehlenden Aufruf.
- **Dienste fehlen**: laeuft ein Asset-Kontext, ohne dass `UseSharedAssets` in der
  WebPart-Konfiguration gesetzt ist, wirft die Middleware mit klarer Meldung — dieselbe Linie wie
  die bestehende in `IsLegitSharedAssetPath` ("Use GetAssetSecurityRepository in your
  ISecurityRepository dependency injection call").
- Der XML-Kommentar an `UseSharedAssetPath()` nennt die Reihenfolge ausdruecklich — wie
  `UseTenantPathPrefix()` es vormacht ("Place AFTER UseAuthentication … and BEFORE
  MapRazorComponents"). Hier lautet sie schlicht: **so frueh wie moeglich, vor allem anderen.**

## 7. Bestand, der angefasst wird

| Datei | Aenderung |
|---|---|
| `Extras/AnonymousAssetAccess/IGetAnonymousAssetQuery.cs` | `Execute(IQueryCollection, out bool)` → quellen-neutrale Signatur. **Breaking**, aber ein Ein-Methoden-Vertrag mit genau einer Implementierung im Repo |
| `DefaultAnonymousAssetUserResolver.cs` | liest ueber `ISharedAssetContext`; `CreateAnonymousLink` erzeugt die **Pfadform** statt der Query |
| `AnonymousAssetAuthenticationHandler.cs` | liest ueber `ISharedAssetContext`; Referer-Rueckfall gilt nur noch fuer die Query-Form |
| `Security/ClaimsTransformation/AssetDrivenClaimsTransformation.cs` | `IHttpContextUserProvider` → `IContextUserProvider` + `ISharedAssetContext` |
| `Extensions/ServiceProviderExtensions.cs` (`IsLegitSharedAssetPath`) | dito; ausserdem: welcher Pfad an `VerifyRequestLocation` geht (siehe 8.2) |
| `SharedAssetInfoProvider.CreateLink` | Pfadform statt `?SharedAssetKey=` |
| `SharedAssetInfoProvider.VerifyRequestLocation` | prueft gegen die kanonische Pfadform |
| `Blazor/Security/BlazorContextUserProvider.cs` | veroeffentlicht den Asset-Abschnitt wie heute schon das Mandantensegment (Basis-URI → `Items` → Routenwerte) |
| `Blazor/Security/TenantBaseHref.cs` | haengt den Asset-Abschnitt an den `<base href>` |
| `Security/SharedAssets/AssetSecurityRepository.cs:202` | der Dreher (siehe 8.1) |
| `Routing/Impl/UrlFormatImpl.cs` | Asset-Abschnitt in die Platzhalter, zweite Platzhalter-Familie fuer `~`-Aufrufer (5.4) |
| `TelerikUi.AdminViews/.../TenantTemplateActivation.cshtml:23`, `ApiForeignKey.cshtml:14`, `MultiSelect.cshtml:12` | von `~[SlashPermissionScope]` auf die `UnderBase`-Familie (sonst doppelter Praefix) |
| `TelerikUi.AdminViews/Extensions/HtmlExtensions.cs` | neuer Helfer, der `ITVenture.Ajax.baseUrl` aus `PathBase` und `assetSegment` ausgibt (5.5) |

**Keine Migration.** Das Datenmodell traegt alles schon; es aendert sich nur, wie der Schluessel zum
Server kommt.

## 8. Fallstricke

### 8.1 `GetEligibleScopes` liefert Anzeigename und Name vertauscht

`AssetSecurityRepository.cs:202` gibt `ScopeName = "Limited Asset Scope"` und
`ScopeDisplayName = assignedUserScope` zurueck. Sobald `TenantPathPrefixMiddleware` das
Mandantensegment gegen diese Liste validiert — und genau das ist ab Abschnitt 4 der Fall —,
vergleicht sie das Segment gegen den Literalstring und antwortet 404. Heute faellt das nicht auf,
weil dieser Weg die Middleware nie erreicht.

**Das ist die erste Stelle, an der es haengen wird.** Zu pruefen ist auch, ob irgendwo im Bestand auf
dem Literal `"Limited Asset Scope"` aufgebaut wird, bevor er getauscht wird.

### 8.2 Welchen Pfad prueft die Ortsbindung?

Nach dem Strippen ist `Request.Path` weder der Pfad aus dem Link noch der aus `RootPath`. Das ist
exakt die Fehlerklasse aus `BUG-PRE187` (Middleware strippt vor `UseRouting`, die Quelle stimmt
danach nicht mehr).

**Festlegung: die kanonische Form ist mandanten- und asset-praefixfrei** — also der Pfad, wie ihn
`@page` sieht. `RootPath` wird in dieser Form gespeichert (Altbestand pruefen!), und
`VerifyRequestLocation` bekommt sie. Eine Hilfsmethode, ein Aufrufer-Muster, keine zweite Meinung
darueber im Code.

**Und sie faellt in den beiden Welten unterschiedlich an**: in Blazor ist der Mandant zu diesem
Zeitpunkt schon aus `Path` heraus (er liegt in `PathBase`), in MVC steht er noch drin (Routenwert).
Die Hilfsmethode muss den Mandanten also **rechnen** statt `Request.Path` roh weiterzureichen —
sonst prueft dieselbe Regel in MVC gegen einen Pfad mit Mandantensegment und in Blazor gegen einen
ohne, und `RootPath` kann nur einer von beiden Formen entsprechen.

### 8.3 Der Dekorator ersetzt die Rechte, er ergaenzt sie nicht

Ein angemeldeter Administrator, der eine Asset-URL oeffnet, hat in diesem Kontext **nur** die
Asset-Rechte. Navigation, Mandantenumschalter und Kachelrahmen werden nackt. Das ist die gewollte
Wirkung — aber die Huelle muss es aushalten, oder die Asset-Seiten benutzen ein schlankes Layout.
Gehoert in den Leitfaden, sonst wird es als Fehler gemeldet.

### 8.4 An- und Abmeldelinks tragen das Praefix mit

Sobald der Abschnitt in `PathBase` steht, erben ihn auch Anmelde-, Abmelde- und Rueckkehr-URLs. Die
`AuthPathExclusions` aus `ScopedPermissionScopeOptions` muessen fuer die Asset-Middleware genauso
gelten wie fuer die Mandanten-Middleware.

### 8.5 Der Circuit haelt die Asset-Claims fuer seine Lebensdauer

Die Claims haengen am Prinzipal, der beim Start des Circuits eingefroren wird. `NotAfter` und
`MaximumLinkDuration` wirken damit erst beim naechsten HTTP-Request, nicht mitten im Circuit. Wer
harten Ablauf will, braucht eine Revalidierung — und die darf die Asset-Identitaet **nicht** gegen
den Benutzer-Store pruefen, sonst fliegt sie sofort raus. Zunaechst bewusst nicht umgesetzt; die
Entscheidung steht in Abschnitt 12.

### 8.6 Alle anonymen Asset-Besucher heissen `#ANONYMOUS#`

`AssetSecurityRepository` haengt durchgehend an `Identity.Name`, und der ist fuer den anonymen Weg
konstant. Der Mechanismus liefert **Rechte, keine Identitaet**. Wer Besucher auseinanderhalten will
(Warenkorb, Zuordnung, Protokoll), braucht dafuer etwas Eigenes — eine Besucherkennung neben dem
Asset, nicht darin.

## 9. Sicherheit

- **Der Abschnitt ist ein Zugangsmittel in der URL.** Er landet in Browser-Verlauf, Server-Logs und
  — bei Verweisen nach aussen — im `Referer`. Das gilt fuer die Query-Form genauso, es ist also kein
  Rueckschritt; trotzdem gehoert `Referrer-Policy: same-origin` in die Empfehlung, und die
  Gueltigkeitsfenster (`NotBefore`/`NotAfter`, `MaximumLinkDuration`) sind das eigentliche Mittel.
- **Die Zugangspruefung bleibt unveraendert.** `AssetIsAccessible` verlangt weiterhin einen
  passenden Benutzer- oder Mandantenfilter. `%` ist der Platzhalter fuer "alle" — ein Template mit
  `%` und `#ANONYMOUS#` ist damit oeffentlich, und das muss in der Verwaltungsmaske auch so
  aussehen.
- **Ein Asset kann Rechte geben, die der Zugreifende sonst nicht hat.** Das ist sein Zweck. Die
  Schranken sind: **wer** (die Filter), **wo** (die Pfade des Templates), **wie lange** (das
  Gueltigkeitsfenster) und **in welchem Mandanten** (`UserScopeName`, per `FixedUserScope`
  festgenagelt). Vier Schranken, alle am Asset, alle sichtbar.

## 10. Abkuendigung der Query-Form

Die Form `?SharedAssetKey=…&__AccessToken=…` **bleibt lesend erhalten** — bestehende Links, die
verschickt wurden, duerfen nicht brechen.

- **Erzeugt** werden ab Phase 4 nur noch Pfad-Links.
- Der **Referer-Rueckfall** bleibt ausschliesslich fuer die Query-Form; fuer die Pfadform wird er
  nicht gebaut (er ist dort gegenstandslos).
- Ein Schalter `AcceptQuerySharedAssetKey` (Vorgabe **true**) in den WebPart-Optionen erlaubt Hosts,
  die Altform abzuschalten, sobald ihre Links abgelaufen sind.
- Entfernt wird sie fruehestens, wenn kein Host sie mehr braucht — mit Ankuendigung im Leitfaden.

## 11. Phasen

1. **Fundament, ohne Verhaltensaenderung.** Konstanten, `ISharedAssetContext` (Items → Query als
   Rueckfall), die eine Middleware, `UseSharedAssetPath()`. Die Query-Form laeuft unveraendert
   weiter. Tests fuer Erkennung und Strippen.
2. **Anonymer Weg auf die neue Quelle.** `IGetAnonymousAssetQuery`, Resolver, Auth-Handler. Ab hier
   funktioniert ein Pfad-Link im MVC-Weg.
3. **Host-neutral machen.** Claims-Transformation und `IsLegitSharedAssetPath` auf
   `IContextUserProvider`; `BlazorContextUserProvider` und `TenantBaseHref` nachziehen. **Ab hier
   funktioniert der Mechanismus im Blazor-Circuit** — das ist der eigentliche Zugewinn.
4. **Linkerzeugung umstellen.** `CreateLink`/`CreateAnonymousLink` auf die Pfadform, der Dreher aus
   8.1, kanonischer Pfad aus 8.2, Verwaltungsmaske zeigt die neuen Links. **Dazu gehoert der
   Formatter (5.4) samt Umstellung der drei `~[SlashPermissionScope]`-Aufrufer und der
   Client-Schnipsel fuer `ITVenture.Ajax.baseUrl`/`assetSegment` (5.5)** — ohne die beiden faellt
   jede generierte URL aus dem Kontext, und zwar still.
5. **Leitfaden.** Abkuendigung, Reihenfolge in der Pipeline, die Wirkung aus 8.3.

## 12. Tests

- **Segment-Erkennung**: Marker an Position 1 und 2; Schluessel mit und ohne Token; kaputtes
  Base64; ein Abschnitt, der zufaellig mit `~` beginnt, aber kein gueltiges Asset ist (muss 404
  ergeben, nicht 500).
- **Strippen**: `PathBase`/`Path` nach der Asset- und nach der Mandanten-Middleware; Pfad endet auf dem Asset-Abschnitt
  (`/kunde/~asset` → `Path == "/"`); virtuelles Verzeichnis (das Praefix wird **angehaengt**, der
  Asset-Abschnitt ist also nicht `PathBase[0]` — dieselbe Falle wie in PRE187).
- **Ortsbindung**: `VerifyRequestLocation` gegen die kanonische Form, mit und ohne Mandantensegment.
- **Scope**: Mandantensegment plus Asset-Abschnitt kommt durch `TenantPathPrefixMiddleware` (das ist
  der Test, der 8.1 aufdeckt).
- **MVC**: nach dem Strip muss `{mandant}` **noch in `Path`** stehen, und
  `CheckPermissionScopeExists` muss einen fremden Mandanten unter aktivem Asset ablehnen
  (`PermissionScopeExists` → nur der Scope des Assets).
- **Formatter** (5.4): `[SlashPermissionScope]` mit und ohne aktives Asset, in beiden
  Positionsvarianten; `~[SlashScopeUnderBase]` ergibt nach Aufloesung durch `baseUrl` **genau
  einmal** den Praefix. Der Test, der die Doppelung faengt.
- **Circuit**: `BlazorContextUserProvider` meldet den Asset-Abschnitt aus der Basis-URI, aus `Items`
  und aus den Routenwerten — die drei Quellen in dieser Reihenfolge, analog zu den bestehenden
  Tests fuer das Mandantensegment.

## 13. Offene Punkte

- **Harter Ablauf im Circuit** (8.5): bauen oder bewusst offenlassen? Vorschlag: offenlassen, aber im
  Leitfaden benennen — ein Asset-Kontext ist so langlebig wie der Circuit, nicht laenger.
- **Besucherkennung** (8.6): braucht es eine? Sie gehoert nicht in dieses Vorhaben, aber die
  Entscheidung faellt beim ersten Konsumenten, der Zuordnung braucht.
- **MVC-Hosts**: der Weg ist in Abschnitt 5 durchgeplant und braucht keinen eigenen Code, aber es
  gibt im Repo derzeit **keinen MVC-Host, an dem er verifiziert wuerde**. Gebaut wird er mit, geprueft
  wird er erst, wenn einer ihn benutzt. Ungewiss ist dort nur das Zusammenspiel mit dem
  Client-Skript (5.5), nicht der Mechanismus.
- **`RootPath` im Altbestand**: liegt er schon in der kanonischen Form aus 8.2, oder braucht es eine
  einmalige Bereinigung am Host?

## 14. Was daraus faellt

Der oeffentliche Shop-Bereich aus `Plan-Stripe-Connect-TenantPayments.md` ist damit **ein
Anwendungsfall und kein eigenes Vorhaben**: ein Asset-Template "Shop" mit dem Root-Pfad des Shops,
`UserFilter = #ANONYMOUS#`, Grants = die Shop-Berechtigungen. Kein Gastkonto, kein Sammelbenutzer,
keine Sonderlogik im Mandantenweg. Was dort zusaetzlich zu klaeren bleibt, ist allein die
Besucherkennung fuer den Warenkorb (8.6) — die Rechtefrage ist mit diesem Plan beantwortet.

## 15. Referenzen

- `docs/BUG-PRE187-TenantPathPrefix-RouteValues.md` — dieselbe Fehlerklasse wie 8.2.
- `docs/BUG-PRE141-TenantUrlGuard-Absolute-Links.md` — warum root-absolute Verweise den
  Praefix-Raum verlassen; gilt fuer den Asset-Abschnitt genauso.
- `docs/Plan-Stripe-Connect-TenantPayments.md` — Abschnitt 12.
