# BUG (PRE234): `TenantPathPrefixMiddleware` beantwortet statische Dateien mit 404, sobald der Benutzer einen Geltungsbereich hat

> Gemeldet aus MiniStore (`5.0.0-PRE234`). Unabhängig von
> `BUG-PRE234-Onboarding-Reload-Without-TenantPrefix.md`, aber im selben Fall aufgefallen: jener
> Report erklärt, warum eine Seite ohne Mandantensegment steht, dieser, warum das die Seite zerlegt.

## Übersicht

| | |
|---|---|
| **Betroffen** | `ITVComponents.WebCoreToolkit.Blazor/Security/TenantPathPrefixMiddleware.cs`, Zeile 68-77 (Ausschlussliste) und Zeile 171-177 (die 404-Entscheidung) |
| **Kern** | Jede Anfrage eines angemeldeten Benutzers mit mindestens einem zulässigen Geltungsbereich wird über ihr erstes Pfadsegment geprüft — auch `/app.<hash>.css`, `/lib/…`, `/js/…`, `/favicon.png`. Wurde die Seite ohne Mandantenpräfix geladen, fragt der Browser genau so an, und jede dieser Anfragen endet mit 404. |
| **Auslösende Bedingung** | Benutzer hat ≥ 1 eligible scope **und** die Seite wurde ohne Präfix ausgeliefert. Das ist jede Seite unterhalb der `AuthPathExclusions`, also der gesamte `/Account/`-Bereich — Anmeldeseite, Profil, Freigaben, Onboarding. |
| **Folge** | Kein Stylesheet, keine Icon-Schrift, kein eigenes JS. Die Seite rendert roh; ein Fehler ist nirgends sichtbar ausser im Serverlog. |
| **Nicht betroffen** | Seiten **mit** Präfix: dort trägt `<base href="/{tenant}/"` alle Unterressourcen, das Segment wird validiert und gestrippt, `MapStaticAssets` findet die Datei. |

## Root Cause

Der Kommentar an Zeile 68-71 hält die Sache für erledigt:

> *Static files served from conventional paths are also skipped: their first segment ("css", "js",
> "lib", "img") will never match an eligible scope, so they would 404 anyway — listing them explicitly
> keeps log output cleaner.*

Übersprungen wird aber nur, was `IsExcluded(path, opts.AuthPathExclusions)` trifft, und die Vorgabe
(`ScopedPermissionScopeOptions.cs:66-74`) enthält ausschliesslich Auth-Routen: `/Account/`,
`/Identity/Account/`, `/Logout`, `/Login`, `/signin-`, `/signout-`. Kein `css`, kein `js`, kein `lib`.
Ein Host, der die Liste nicht selbst erweitert, bekommt für jede statische Datei die Segmentprüfung —
und damit den 404.

**Das lässt sich durch Konfiguration auch nicht sauber schliessen.** Die konventionellen Ordner
(`/lib/`, `/js/`, `/img/`) könnte ein Host nachtragen; die fingerprinted Dateien im Wurzelverzeichnis
haben jedoch kein Verzeichnispräfix, das man eintragen könnte:

```
/app.kj9jwg8c4q.css
/MiniStore.Web.0opedqpr5n.styles.css
/favicon.png
```

Deren Name ändert sich mit jedem Build. Es bliebe, `/app.` und den Projektnamen als Präfix
einzutragen — eine Liste, die bei jedem neuen Asset nachgezogen werden muss und deren Vergessen sich
als „Seite sieht komisch aus" äussert, nicht als Fehler.

## Messung

Zwei unabhängige Vorfälle in derselben Installation, beide aus `SystemLog` (UTC):

**12.09., Benutzer `mw@it-venture.ch` mit Mandant ADM, auf `/Account/Login`** — also lange vor jedem
Onboarding und mit einem völlig gewöhnlichen Benutzer:

```
16:11:58.285965  segment 'app.kj9jwg8c4q.css' not in user's eligible scopes; responding 404 for /app.kj9jwg8c4q.css
16:11:58.285981  segment 'lib' → 404 /lib/bootstrap/dist/css/bootstrap.min.46ein0sx1k.css
16:11:58.285994  segment 'MiniStore.Web.0opedqpr5n.styles.css' → 404
16:11:58.286002  segment 'js' → 404 /js/nav-menu.x5n40ai1ov.js
16:11:58.286005  segment 'favicon.png' → 404 /favicon.png
16:11:58.286080  segment 'Components' → 404 /Components/Layout/ReconnectModal.razor.js
```

**14.09., Benutzer `laden@ecke.ch`, unmittelbar nach der Mandantenanlage** — dieselben sechs Zeilen,
ausgelöst durch den Reload aus `MyTenants.razor` (siehe den zugehörigen Report).

Die 404s laufen anschliessend in die Statuscode-Neuausführung, was das Log zusätzlich füllt:

```
TenantPathPrefix: passing through the status-code re-execution to /not-found;
the request that actually failed was /app.kj9jwg8c4q.css(null).
```

## Lösungsvorschlag

Nicht am Anfang der Methode ansetzen, sondern **an der 404-Entscheidung** (Zeile 171-177): wenn das
erste Segment kein zulässiger Geltungsbereich ist, der Pfad aber wie eine Datei aussieht, weiterreichen
statt 404 zu beantworten. Dann entscheidet die statische Auslieferung selbst, ob es die Datei gibt.

```csharp
if (!eligible.Any(s => string.Equals(s.ScopeName, firstSegment, StringComparison.Ordinal)))
{
    // Eine Datei ist kein Mandant. Sie hier abzulehnen schuetzt nichts - ueber die Existenz eines
    // Mandanten sagt sie ohnehin nichts aus -, kostet aber jede praefixlos geladene Seite ihre
    // Gestaltung. Was es nicht gibt, beantwortet die statische Auslieferung selbst mit 404.
    if (Path.HasExtension(path))
    {
        await next(context);
        return;
    }

    LogRejectedSegment(context, path, firstSegment, eligible);
    context.Response.StatusCode = StatusCodes.Status404NotFound;
    return;
}
```

**Wichtig ist die Stelle.** Dieselbe Prüfung weiter oben, neben dem `/_`-Ausschluss, wäre falsch: sie
träfe auch `/{tenant}/app.css`, und dort muss das Präfix **gestrippt** werden, sonst sucht
`MapStaticAssets` eine Datei, die es unter diesem Pfad nicht gibt. An der 404-Entscheidung greift die
Regel nur für Pfade, deren erstes Segment ohnehin kein Geltungsbereich ist.

Kein Informationsleck: die Antwort für eine nicht existierende Datei bleibt 404, nur eben aus der
statischen Auslieferung. Über die Existenz von Mandanten sagt sie nichts.
