# BUG PRE254: `/Account/Manage/TwoFactorAuthentication` stürzt im Circuit ab, weil der HttpContext nicht `null` ist

**Status:** behoben (nach PRE254, siehe „Behebung“) · **Datum:** 2026-10-13 · **Quelle:** MiniStore, Toolkit 5.0.0-PRE254

## Der Fall

Die Seite `/Account/Manage/TwoFactorAuthentication` lässt sich nicht öffnen. Die Komponente wirft bereits in
`OnInitializedAsync`, und der Circuit stirbt. Auszug aus `SystemLog` (UTC):

```
14:21:09  CircuitHost  Unhandled exception in circuit '…'.
System.InvalidOperationException: OnStarting cannot be set because the response has already started.
   at …HttpProtocol.ThrowResponseAlreadyStartedException(String value)
   at …CookieAuthenticationHandler.InitializeHandlerAsync()
   at …AuthenticationService.AuthenticateAsync(HttpContext context, String scheme)
   at Microsoft.AspNetCore.Identity.SignInManager`1.IsTwoFactorClientRememberedAsync(TUser user)
   at ITVComponents.WebCoreToolkit.IdentityShared.Helpers.UserGuard`1.BuildUserQueryTicket(…)
   at ITVComponents.WebCoreToolkit.IdentityShared.Helpers.UserGuard`1.FetchUser(ClaimsPrincipal user, …)
   at …IdentityPages.Components.Account.Pages.Manage.TwoFactorAuthentication.OnInitializedAsync()
```

Das passiert bei jedem Aufruf, nicht nur gelegentlich.

## Ursache

`UserGuard.BuildUserQueryTicket` (`IdentityShared/Helpers/UserGuard.cs`, um Z. 124–140) schützt den
cookie-gebundenen Aufruf so:

```csharp
if (httpContextAccessor?.HttpContext != null)
{
    machineRememberedForTwoFactor = await signInManager.IsTwoFactorClientRememberedAsync(userInstance);
}
```

Die Annahme ist: auf dem Circuit gibt es keinen HttpContext. **Das stimmt nicht.** Unter Blazor Server
liefert `IHttpContextAccessor.HttpContext` im Circuit den Kontext der **SignalR-Verbindung** (WebSocket bzw.
Long-Polling). Er ist nicht `null`, aber seine Antwort ist längst gestartet. `AuthenticateAsync` initialisiert
den Cookie-Handler, der `Response.OnStarting` registriert, und das wirft.

Die Absicherung über das Vorab-Rendering (Commit `04004796`, `[PersistentState] MachineRemembered`) greift
dabei nicht, aus zwei Gründen:

1. **Ohne Vorab-Rendering** (MiniStore: `InteractiveServerRenderMode(prerender: false)` im `App.razor`) gibt es
   keinen herübergetragenen Wert. Der Circuit ruft `FetchUser(…, TwoFactorConfiguration)` selbst, also kracht
   es immer.
2. **Mit Vorab-Rendering** ruft die Seite `FetchUser` im Circuit trotzdem auf (`MachineRemembered ??= …` steht
   *nach* dem Aufruf). Der Wert wird dann zwar nicht gebraucht, aber `UserGuard` fragt das Cookie
   ab, sobald ein HttpContext da ist. Das sollte denselben Absturz geben. Ich habe es nicht nachgeprüft, nur
   aus dem Code gelesen.

## Vorschlag

1. **Die Bedingung in `UserGuard` schärfen:** nicht „HttpContext vorhanden“, sondern „Anfrage, deren Antwort
   noch nicht begonnen hat“:
   ```csharp
   var http = httpContextAccessor?.HttpContext;
   if (http != null && !http.Response.HasStarted)
   ```
   Dann greift im Circuit der bestehende `else`-Zweig mit seiner Report-Meldung, und `MachineRememberForTwoFactor`
   bleibt `null`, also „nicht feststellbar“. Die Seite zeigt dann „Browser vergessen“ (`MachineRemembered != false`),
   was der vorgesehene, robuste Rückfall ist.
2. Optional: Die Seite ruft `FetchUser` mit `TwoFactorConfiguration` nur, wenn `MachineRemembered` noch `null`
   ist. Oder es gibt ein eigenes Flag ohne die Cookie-Frage, damit der Circuit gar nicht erst in die Nähe kommt.
3. Derselbe Fehler steckt vermutlich in jedem anderen Circuit-Aufrufer von `SignInManager`-Methoden, die das
   Cookie lesen oder schreiben (`IsTwoFactorClientRememberedAsync`, `RefreshSignInAsync`, `ForgetTwoFactorClientAsync`).
   Ein Durchgang über die interaktiven Manage-Seiten lohnt sich.
4. Ein Test, der den Handler mit einem `HttpContext` aufruft, dessen `Response.HasStarted == true` ist
   (`DefaultHttpContext` mit einem eigenen `IHttpResponseFeature`).

## Umgehung

In MiniStore keine sinnvolle: Die Seite trägt ihr `@rendermode` selbst, und das Vorab-Rendering global
einzuschalten hiesse, das Verhalten jeder Seite zu ändern. Ausserdem behebt es den Absturz nach Punkt 2 oben
vermutlich nicht. Bis zur Behebung ist die 2FA-Verwaltung unter `/Account/Manage` nicht benutzbar.

## Behebung

- **`UserGuard.BuildUserQueryTicket`:** Das Cookie wird nur noch gelesen, wenn `HttpContext != null &&
  !Response.HasStarted`. Auf dem Circuit greift der `else`-Zweig: `MachineRememberForTwoFactor` bleibt
  `null`, und eine Report-Meldung nennt den Grund („response has already started“). Kommentar und
  Konstruktor-Doku sind entsprechend korrigiert.
- **Punkt 2 entfällt:** Mit der geschärften Bedingung ist der zusätzliche `FetchUser`-Aufruf im Circuit
  harmlos. Er liefert nur `null`, und `MachineRemembered ??= …` behält den vorgerenderten Wert.
- **Punkt 3 geprüft:** Kein anderer interaktiver Aufrufer schreibt das Cookie. Alle Manage-Seiten mit
  `InteractiveServer` gehen für `RefreshSignIn`, `SignOut` und `ForgetBrowser` über `ManageRedirects`, also
  über eine eigene Anfrage. `IsTwoFactorClientRememberedAsync` in `UserGuard` war der einzige Zugriff aus
  dem Circuit. `GetTwoFactorAuthenticationUserAsync` nutzt nur `LoginWith2fa`, und diese Seite läuft als
  statisches SSR.
- **Punkt 4 offen:** `IdentityShared` hat kein Testprojekt.
