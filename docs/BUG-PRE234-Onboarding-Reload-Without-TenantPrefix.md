# BUG (PRE234): Nach dem Onboarding lädt `MyTenants` präfixlos neu — der frisch angelegte Mandant kommt nie in die Adresse

> Gemeldet aus MiniStore (`5.0.0-PRE234`), am laufenden System nachgemessen. Hängt mit
> `BUG-PRE234-TenantPathPrefix-Static-Assets.md` zusammen: dieser Report erklärt, **warum** die Seite
> ohne Mandantensegment steht, jener, **warum** das ab diesem Moment die ganze Seite zerlegt. Beide
> sind unabhängig voneinander behebbar.

## Übersicht

| | |
|---|---|
| **Betroffen** | `ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews/OnboardingViews/Components/Onboarding/MyTenants.razor`, Zeile 87 und Zeile 108 |
| **Kern** | Beide Stellen laden mit `Nav.NavigateTo(Nav.Uri, forceLoad: true)` neu. `Nav.Uri` ist `/Account/Onboarding/MyTenants` — **ohne** Mandantensegment. Ein Präfix kommt auch beim Full-Reload nicht dazu: die Seite liegt unter `/Account/`, und das steht in den Default-`AuthPathExclusions` (`ScopedPermissionScopeOptions.cs:66-74`), die `TenantPathPrefixMiddleware.cs:73` überspringt. |
| **Folge** | Der Benutzer bleibt nach der Mandantenanlage auf einer präfixlosen Adresse. `<base href>` bleibt `/`, jede Unterressource wird präfixlos angefragt — und ab dem ersten zulässigen Geltungsbereich beantwortet dieselbe Middleware genau die mit 404. Ergebnis: Seite ohne Gestaltung, Menü ohne Funktion. |
| **Wann sichtbar** | Genau einmal pro Benutzer: beim ersten angemeldeten Aufruf nach dem Onboarding, wenn `CompletePendingOnboardingAsync` `true` liefert. Derselbe Pfad gilt für `AssignDefaultTenantAsync` (Zeile 108). |
| **Nicht betroffen** | Hosts ohne `TenantSource.PathSegment`. Dort ist `Nav.Uri` die vollständige Adresse. |

## Messung

MiniStore, `laden@ecke.ch`, erster Mandant über den Direct-Onboarding-Fluss. Auszug aus `SystemLog`
(Zeitstempel UTC, Kategorien gekürzt):

```
09:00:29.120  ScopedPermissionScope     No eligible scopes for user labels [laden@ecke.ch].
09:00:29.903  ScopedPermissionScope     Default-Value of current scope: 45e6968d5a7044c38ee17787efe0ee66
09:00:30.267  MyTenants                 Pending onboarding completion for laden@ecke.ch returned True.
09:00:30.267  RemoteNavigationManager   Requesting navigation to URI
                                        https://localhost:7088/Account/Onboarding/MyTenants with forceLoad=True
09:00:30.279  TenantPathPrefixMiddleware segment 'lib' not in user's eligible scopes;
                                        responding 404 for /lib/bootstrap-icons/fonts/bootstrap-icons.woff2
09:00:30.420  TenantPathPrefixMiddleware segment 'lib' → 404 /lib/bootstrap-icons/bootstrap-icons.min.css
09:00:30.431  TenantPathPrefixMiddleware segment 'app.kj9jwg8c4q.css' → 404 /app.kj9jwg8c4q.css
09:00:30.446  TenantPathPrefixMiddleware segment 'js' → 404 /js/nav-menu.x5n40ai1ov.js
09:00:30.446  TenantPathPrefixMiddleware segment 'MiniStore.Web.0opedqpr5n.styles.css' → 404
09:00:30.498  TenantPathPrefixMiddleware segment 'Components' → 404 /Components/Layout/ReconnectModal.razor.js
09:00:30.733  MyTenants                 Pending onboarding completion for laden@ecke.ch returned False.
```

Das ist der ganze Fall in zwei Zeilen: der Geltungsbereich ist um 09:00:29.903 **aufgelöst und
bekannt** (`45e6968d…`), und die Navigation 364 ms später geht trotzdem auf die Adresse **ohne** ihn.

## Root Cause

Der Kommentar an Zeile 81-86 benennt den Zweck des Reloads richtig — „the interactive circuit that ran
the completion never went through that resolution, so the shell renders half-initialised until a
reload". Er unterstellt aber, dass ein Full-Reload denselben Pfad in einen Mandantenkontext bringt.
Für Seiten unterhalb von `/Account/` trifft das nicht zu: die Middleware prüft ihre Ausschlussliste
**vor** jeder Segment-Behandlung, sie setzt dort also nie ein Präfix und schreibt auch keines in
`HttpContext.Items`. `TenantBaseHref` findet nichts, `<base href>` bleibt `/`, und die neu geladene
Seite steht wieder genau dort, wo sie vorher stand.

Der Fall ist neu, weil er den Übergang berührt: **vor** der Anlage hat der Benutzer keinen zulässigen
Geltungsbereich, die Middleware lässt alles unangetastet durch, und die präfixlose Seite funktioniert
vollständig. **Nach** der Anlage ist die Voraussetzung für genau dieses Durchlassen weg.

## Lösungsvorschlag

Im Reload das Mandantensegment selbst voranstellen. In MiniStore lokal gebaut (`dotnet build` des
AdminViews-Projekts, 0 Fehler) und anschliessend wieder zurückgenommen — der Toolkit-Baum ist
unverändert:

```razor
@using ITVComponents.WebCoreToolkit.Security
@inject IPermissionScope Scope
```

```csharp
private const string PageRoute = "/Account/Onboarding/MyTenants";

private string ReloadTarget()
{
    // Der Geltungsbereich wurde in diesem Circuit aufgeloest, als es den Mandanten noch nicht gab,
    // und dieses Ergebnis - keiner - ist gemerkt.
    Scope.Refresh();
    var scope = Scope.PermissionPrefix;
    // Leeres Segment ergaebe "//Account/..." - protokoll-relativ, der Browser suchte einen fremden Host.
    return !string.IsNullOrEmpty(scope) ? $"/{scope}{PageRoute}" : Nav.Uri;
}
```

…und an beiden Stellen `Nav.NavigateTo(ReloadTarget(), forceLoad: true)`. Schleifenfrei bleibt es wie
bisher: nach dem Reload ist der Pending-Datensatz `Committed`, die Vervollständigung liefert `false`.

**Alternative, falls der Fix nicht in der Komponente liegen soll:** `TenantPathPrefixMiddleware` einen
angemeldeten Benutzer mit Geltungsbereichen auf einem präfixlosen, nicht auth-relevanten Pfad
grundsätzlich auf `/{defaultScope}{path}` umleiten — analog zu dem, was sie für `/` bereits tut
(`TenantPathPrefixMiddleware.cs:156-168`). Das wäre die allgemeinere Lösung, berührt aber jede
`/Account/`-Seite und damit auch Fälle, in denen Mandantenfreiheit gewollt ist (die Mandantenauswahl
selbst). Deshalb hier nur als Notiz, nicht als Empfehlung.
