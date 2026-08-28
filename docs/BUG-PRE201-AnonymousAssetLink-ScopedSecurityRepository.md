# BUG (PRE201): Der anonyme Freigabe-Link endet weiterhin im 404 — der Asset-Dekorator wird gebaut, bevor es den Asset-Prinzipal gibt

> Gemeldet aus **MLMManager** (Blazor Server/WASM-Auto, `TenantSource.PathSegment`, Toolkit
> `5.0.0-PRE201+2a7df2bd`, am laufenden System gemessen). Fortsetzung von
> `BUG-PRE197-AnonymousAssetLink-TenantPrefix-404.md` — der Fix von damals greift, dies hier ist die
> nächste Stufe derselben Kette.

## Der Diskriminator, der alles einordnet

**Derselbe anonyme Link funktioniert, wenn der Besucher angemeldet ist, und scheitert, wenn er es
nicht ist.** Das schliesst Freigabe, Reichweite (`##ANONYMOUS`), Zugangstoken, Vorlage und Pfadmuster
aus — sonst würde auch der angemeldete Aufruf scheitern. Übrig bleibt genau der Pfad, den nur der
abgemeldete Besucher nimmt: der, den PRE198 überhaupt erst gangbar gemacht hat.

## Übersicht

| | |
|---|---|
| **Betroffen** | `ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity/CoreIdentityTree/Extensions/DependencyExtensions.cs:42-51` (Scoped-Factory für `ISecurityRepository`) im Zusammenspiel mit `ITVComponents.WebCoreToolkit/Extensions/ServiceProviderExtensions.cs`, `GetAssetSecurityRepository` |
| **Kern** | `GetAssetSecurityRepository` entscheidet **einmal je Request**, ob `AssetSecurityRepository` auf den Stapel kommt — anhand des Prinzipals im Moment der **ersten** Auflösung. Beim anonymen Freigabe-Zugriff fällt diese erste Auflösung **in** `AuthenticateAsync` hinein, also bevor es den Asset-Prinzipal gibt. |
| **Folge** | Die Middleware bekommt später eine undekorierte Instanz, `GetEligibleScopes` liefert nichts, `TenantPathPrefixMiddleware` lässt den Pfad ungestrippt durch, das Routing findet keinen Endpunkt → 404. `SharedAssetAccesses` bleibt leer. |
| **Nicht betroffen** | Angemeldete Empfänger (auch auf einem anonymen Link): deren Prinzipal trägt die Asset-Claims schon aus `UseAuthentication`, damit ist die Instanz bei jeder Auflösung korrekt dekoriert. |

## Messung

Zwei Logzeilen aus demselben Aufruf, in dieser Reihenfolge:

```
AuthenticationScheme: Shared-Asset-Key was successfully authenticated
TenantPathPrefix: authenticated user has no eligible scopes; passing /ADM/CustomerCare/Customers/3 through untouched
```

Die erste beweist, dass das Schema gegriffen hat und ein Prinzipal entstanden ist — der PRE198-Fix
arbeitet also. Die zweite zeigt den Rest des Falls in einer Zeile: der Prinzipal ist authentifiziert,
hat aber keinen einzigen zulässigen Mandanten, und **der Pfad trägt das Mandantensegment `/ADM/` noch**.
Danach sucht das Routing nach `/ADM/CustomerCare/Customers/3`, findet nichts, und
`UseStatusCodePagesWithReExecute` liefert die `/not-found`-Seite.

Datenstand dazu: Freigabe mit Benutzerfilter `##ANONYMOUS`, Mandant ADM, `RootPath`
`/CustomerCare/Customers/3`, Vorlage ohne Gewährungen. `SharedAssetAccesses`: 0 Zeilen.

## Root Cause

```csharp
// CoreIdentityTree/Extensions/DependencyExtensions.cs:42-51
.AddScoped<ISecurityRepository>(i =>
{
    var retVal = new AspNetDbTreeSecurityRepository<AspNetTreeSecurityContext>(…);
    return i.GetAssetSecurityRepository(retVal);
})
```

```csharp
// WebCoreToolkit/Extensions/ServiceProviderExtensions.cs
public static ISecurityRepository GetAssetSecurityRepository(this IServiceProvider services, ISecurityRepository decorated)
{
    var userProvider = services.GetService<IContextUserProvider>();
    var authUser = userProvider.User.Identities.FirstOrDefault(n => n.IsAuthenticated);
    var decorator = new SecurityRepository();
    decorator.PushRepo(decorated);
    if (authUser != null && authUser.HasClaim(n => n.Type == ClaimTypes.FixedUserScope))
    {
        decorator.PushRepo(new AssetSecurityRepository(userProvider.User, decorated));
    }
    return decorator;
}
```

Die Prüfung auf `FixedUserScope` läuft **im Factory-Aufruf**, und der läuft bei `AddScoped` genau
einmal je Request. Der Rückgabewert wird für den ganzen Request behalten.

Im abgemeldeten Fall ist die Reihenfolge:

1. `TenantPathPrefixMiddleware` sieht einen anonymen Benutzer und ruft (seit PRE198, korrekt)
   `context.AuthenticateAsync("Shared-Asset-Key")`.
2. Der Handler zieht `IGetAnonymousAssetQuery`. `DefaultAnonymousAssetUserResolver` nimmt
   **`ISecurityRepository` im Konstruktor** (es braucht es für `Encrypt`/`Decrypt` des Tokens). Das ist
   die **erste** Auflösung im Request — `context.User` ist noch anonym, es gibt keinen
   `FixedUserScope`, also entsteht eine **undekorierte** Instanz und wird gecacht.
3. `AuthenticateAsync` lässt die Claims-Transformation laufen, der Prinzipal bekommt
   `FixedUserScope` → *„was successfully authenticated"*.
4. `TenantPathPrefixMiddleware` setzt `context.User = result.Principal`. Ab hier stimmt der Benutzer.
5. Dieselbe Middleware holt `context.RequestServices.GetService<ISecurityRepository>()` — und bekommt
   die **gecachte, undekorierte** Instanz aus Schritt 2.
6. `ResolveEligibleScopes` fragt sie nach den Mandanten des Benutzers `#ANONYMOUS#`. Zu dem gibt es
   keine Zeile → leeres Ergebnis.
7. `eligible.Length == 0` → der Zweig „passing through untouched" → **kein Strip** → 404.

### Die Asymmetrie, die den Fall verrät

`IsLegitSharedAssetPath` repariert den Stapel an seiner eigenen Aufrufstelle nachträglich:

```csharp
if (seco.Current is not AssetSecurityRepository)
{
    seco.PushRepo(new AssetSecurityRepository(userProvider.User, seco.Current, …));
}
```

`TenantPathPrefixMiddleware.ResolveEligibleScopes` ruft dagegen `repo.GetEligibleScopes(…)` direkt und
geht an dieser Reparatur vorbei. Genau deshalb funktioniert der Weg über die Autorisierung (der über
`IsLegitSharedAssetPath` läuft) und der Weg über die Mandanten-Middleware nicht.

## Fix-Vorschläge

### A) Die Entscheidung nicht einfrieren (bevorzugt)

Seit PRE198 ist ein Prinzipal, der erst **mitten in der Pipeline** entsteht, der vorgesehene Normalfall
— nicht die Ausnahme. Ein Scoped-Dekorator, der den Benutzer bei der ersten Auflösung festhält, ist mit
dieser Bauart grundsätzlich unverträglich, und dieser Report beschreibt nur die erste Stelle, an der
das auffällt. `SecurityRepository` könnte die Asset-Sicht **je Aufruf** prüfen statt einmal bei der
Konstruktion; `GetAssetSecurityRepository` würde dann einen Dekorator liefern, der sich selbst
nachzieht, sobald der Prinzipal die Claims trägt.

Das ist der einzige Vorschlag, der auch die Stellen erwischt, die noch niemand ausprobiert hat.

### B) Gezielt: die Middleware über den reparierenden Weg gehen lassen

`TenantPathPrefixMiddleware` holt die zulässigen Mandanten über denselben Weg wie
`IsLegitSharedAssetPath`, statt `ISecurityRepository` direkt zu fragen. Kleiner Eingriff, behebt genau
diesen 404 — lässt aber die Ursache stehen.

### C) Unabhängig davon: dieser eine Zweig darf nicht auf `Debug` stehen

```csharp
logger.LogDebug("TenantPathPrefix: authenticated user has no eligible scopes; passing {Path} through untouched", path);
```

Für einen normalen angemeldeten Benutzer ohne Mandanten ist `Debug` richtig — das ist ein
Alltagszustand. Für eine Anfrage, die **einen Asset-Abschnitt trägt**, ist es dagegen die letzte
Abzweigung vor einem 404, dem man nichts von einer Freigabe ansieht. Vorschlag: wenn
`SharedAssetPathMiddleware.HasRun(context)`, dann `LogWarning` mit dem Hinweis, dass der Pfad
ungestrippt weitergereicht wird.

Bei uns war die Zeile nur deshalb sichtbar, weil der Host den Toolkit-Logger benutzt, der seine Level
über `GlobalSettings` bezieht und standardmässig **alles** protokolliert. Ein Host mit den üblichen
ASP.NET-Filtern (`"Default": "Information"`) sähe an dieser Stelle wieder **gar nichts** — und damit
denselben stummen 404, dessen Beseitigung der ganze §50-Strang eigentlich zum Ziel hatte.

## Abgrenzung / geprüft und in Ordnung

- **Der PRE198-Fix arbeitet.** `AuthenticateSharedAssetAsync` wird erreicht, das Schema greift, der
  Prinzipal entsteht, `context.User` wird gesetzt. Belegt durch die erste Logzeile.
- **Der PRE199-Punkt (Reichweite)**: die Freigabe hat einen Benutzerfilter `##ANONYMOUS`.
- **Der PRE200-Punkt (leere Gewährungen)**: die Vorlage gewährt nichts, und genau das ist seither in
  Ordnung — der angemeldete Zugriff auf dieselbe Freigabe funktioniert.
- **Token / Entschlüsselung**: nicht die Ursache. `FindAnonymousAsset` und `GetEncryptionKey` lesen
  beide unter `ShowAllTenants = true`, sind also nicht vom fehlenden Mandanten abhängig — und das
  Schema meldet ohnehin Erfolg.
- **Host-Verdrahtung**: `app.UseSharedAssetPath()` an der von §50.2 verlangten Stelle,
  `SharedAssetAuthenticationScheme` auf dem Vorgabewert `"Shared-Asset-Key"`, WebPart
  `AnonymousAssetShares` aktiv. Die eingesetzten Binaries sind nachweislich
  `5.0.0-PRE201+2a7df2bd`.

## Umgebung

| | |
|---|---|
| Toolkit | `5.0.0-PRE201+2a7df2bd` (31 PackageReferences über 4 csprojs) |
| Host | .NET 10, Blazor Web App (Server + WASM Auto), ASP.NET Core Identity |
| Mandanten | `TenantSource.PathSegment`, `app.UseTenantPathPrefix()`, TreeTenants |
| Sicherheitskontext | `AspNetTreeSecurityContext` (CoreIdentityTree), SQL Server LocalDB |

## Umsetzung

Umgesetzt ist **Weg A**, zusammen mit **C**. Beides in PRE202.

`SecurityRepository` kennt jetzt eine **späte Sicht**: eine Funktion, die vor jedem Durchgriff über den
Stapel gelegt wird, statt einmal beim Bauen. `GetAssetSecurityRepository` hinterlegt dort die
Asset-Prüfung und entscheidet gar nichts mehr selbst — die Fabrik weiss nichts mehr über den Prinzipal,
weil sie zum Zeitpunkt ihres Aufrufs nichts darüber wissen *kann*.

Drei Punkte, die beim Umsetzen dazukamen:

- **`UseRoot` schlägt die Sicht.** Wer ausdrücklich die Wurzel verlangt, will an allen Aufsätzen vorbei;
  die späte Sicht greift deshalb nur im `TryPeek`-Zweig.
- **Eine bereits aufgesetzte Asset-Sicht bleibt stehen.** Sitzt oben schon ein
  `AssetSecurityRepository` (der Weg über `IsLegitSharedAssetPath`), wird nichts darübergelegt: jene
  Sicht kennt die Freigabe selbst, diese hier nur die Claims.
- **Gemerkt wird pro Prinzipal und pro darunterliegender Sicht**, damit nicht jeder Zugriff ein neues
  Objekt erzeugt. Trägt der Prinzipal keinen `FixedUserScope`, wird bewusst *nichts* gemerkt — sonst
  bliebe die Antwort „keine Freigabe" hängen, und das wäre derselbe Fehler eine Ebene tiefer.

Die im Report beschriebene Asymmetrie ist damit weg: der Weg über die Autorisierung und der über die
Mandanten-Middleware sehen dasselbe. **Weg B wurde deshalb nicht gebaut** — ein Sonderweg für die
Middleware hätte die Ursache stehen lassen, und der Report benennt selbst, dass dies nur die erste
Stelle ist, an der sie auffällt.

**Punkt C** wie vorgeschlagen: trägt die Anfrage einen Asset-Abschnitt, ist der Zweig „no eligible
scopes" jetzt `LogWarning` statt `LogDebug`, mit dem Hinweis, dass das Mandantensegment im Pfad bleibt
und woher der Mandant der Freigabe kommen müsste. Für einen gewöhnlichen Benutzer ohne Mandanten bleibt
es `LogDebug` — dort ist es ein Alltagszustand. Das Argument mit den üblichen Log-Filtern war
ausschlaggebend: sichtbar nur bei einem Host, der alles protokolliert, heisst unsichtbar.

Test: `Asset_Scope_Applies_When_The_Principal_Arrives_After_The_Repository` stellt genau die Reihenfolge
nach — Repository auflösen, *dann* den Prinzipal entstehen lassen, dann nach den Mandanten fragen. 119
Tests wurden 120, alle grün.
