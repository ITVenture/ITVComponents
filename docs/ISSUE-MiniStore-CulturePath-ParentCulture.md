# Issue: `/c/fr-ch` fällt nicht auf `fr` zurück

**Status:** umgesetzt (2026-11-16) — Leitfaden §107. Variante „angefragte Kultur weiterreichen“: die
Middleware prüft nur, ob das Segment direkt oder über die Elternkette abgedeckt ist (gekoppelt an
`FallBackToParentUICultures`), und reicht `fr-CH` weiter; den Rückfall macht `RequestLocalizationMiddleware`,
getrennt für Format und Texte. `PathBase` bleibt wie geschrieben, gewarnt wird nur, wenn nichts trifft. Der Satz
zu `?culture=` steht in §61.5.
**Datum:** 2026-11-16
**Quelle:** MiniStore-Session (Konsument), beim Durchsehen der Mehrsprachigkeit.
**Toolkit-Stand:** `5.0.0-PRE272` (Commit `32403e463`).

## Der Fall

Der MiniStore führt die Sprachen `de`, `fr`, `it`, `en`, also neutrale Kulturen, und hat
`FallBackToParentUICultures = true` und `FallBackToParentCultures = true` gesetzt. Damit landet ein Browser mit
`Accept-Language: fr-FR` richtig bei `fr`.

Über den Pfad geht das nicht:

| Adresse | Erwartet | Tatsächlich |
|---|---|---|
| `/c/fr/…` | Französisch | Französisch |
| `/c/fr-ch/…` | Französisch (Elternsprache) | Deutsch (Cookie, `Accept-Language` oder Vorgabe) |

Im Log steht dazu die Warnung der Middleware:

```
CulturePath: /c/fr-ch/… asks for the culture 'fr-ch', which is not among the supported ones (de, fr, it, en).
The prefix is stripped, but the language falls back to cookie/Accept-Language/default.
```

## Ursache

`CulturePathMiddleware.Resolve` vergleicht das Segment genau gegen die unterstützten Kulturen, nur ohne
Rücksicht auf Gross- und Kleinschreibung:

```csharp
private string Resolve(string requested)
    => SupportedCultures().TryGetValue(requested, out var canonical) ? canonical : null;
```

Den Rückfall auf die Elternkultur macht in ASP.NET erst die `RequestLocalizationMiddleware`, und zwar für jeden
Kandidaten, den ein Provider liefert. Weil die Middleware `fr-ch` vorher verwirft, kommt der Kandidat bei ihr gar
nicht an; der `CulturePathRequestCultureProvider` liefert `null`, und der nächste Provider entscheidet.

Damit verhält sich der Pfad anders als `Accept-Language` derselben Anwendung: dieselbe Angabe `fr-CH` ergibt
über den Browser Französisch, über die Adresse nicht.

## Vorschlag

1. **`Resolve` fällt wie ASP.NET auf die Elternkultur zurück**, wenn
   `RequestLocalizationOptions.FallBackToParentUICultures` gesetzt ist (bzw. eine eigene Option an
   `CulturePathOptions`, falls eine Kopplung an die ASP.NET-Optionen unerwünscht ist):
   `fr-CH` → `fr`, `de-AT` → `de`. Die Kette über `CultureInfo.Parent` bis vor die invariante Kultur, der erste
   Treffer in der Liste gewinnt.
   - Der Provider liefert dann die **aufgelöste** Kultur (`fr`). Alternativ liefert er die angefragte (`fr-ch`)
     und überlässt den Rückfall der `RequestLocalizationMiddleware`. Das hätte den Vorteil, dass `CurrentCulture`
     und `CurrentUICulture` dieselben Regeln durchlaufen wie bei den anderen Providern; dann darf aber auch die
     Middleware das Segment nicht mehr als „nicht unterstützt“ melden.
   - Ein Host, der `fr-CH` selbst führt, merkt nichts: der exakte Treffer kommt zuerst.
2. **`PathBase` behält die geschriebene Form** (`/c/fr-ch`). Das tut der Code schon heute, und der Kommentar
   dazu begründet es (der `<base href>` des Circuits muss zeichengenau zur Adresse im Browser passen). Beim
   Umbau sollte das so bleiben; ein Test dafür wäre die Absicherung.
3. **Die Warnung erst, wenn auch die Elternkette nichts trifft.** Sonst steht für jeden Aufruf mit `fr-ch` eine
   Warnung im Log, obwohl alles richtig läuft.

Tests in `CulturePathMiddlewareTests`: `/c/fr-CH/x` bei unterstützten `de, fr` → Kultur `fr`, `PathBase`
`/c/fr-CH`, keine Warnung; `/c/es-ES/x` → keine Kultur, Warnung wie bisher; `/c/fr-CH/x` bei unterstütztem
`fr-CH` → `fr-CH`.

## Am Rand: `?culture=` und interaktives Blazor

Beim Prüfen ist uns aufgefallen, dass `?culture=fr` (der `QueryStringRequestCultureProvider` von ASP.NET) im
MiniStore nichts bewirkt. Unsere Vermutung, **nicht nachgeprüft**: die Seiten laufen mit `prerender: false` als
Blazor Server. Der Inhalt entsteht im Circuit, und der übernimmt die Kultur der Anfrage an `…/_blazor`. Der
Pfad-Präfix reist über den `<base href>` dorthin mit, der Query-Parameter der ersten Seitenanfrage nicht.

Das ist kein Fehler des Toolkits, aber eine Falle, die man nur einmal suchen möchte. Ein Satz im Leitfaden beim
Kultur-Präfix („`?culture=` wirkt bei interaktivem Blazor nur auf die erste Anfrage, nicht auf den Circuit; für
einen Wechsel den Pfad oder das Sprach-Cookie benutzen“) würde reichen. Falls die Vermutung nicht stimmt, ist
es eine eigene Untersuchung wert.

## Umgehung

Links mit der neutralen Sprache bauen (`/c/fr/…`). Der MiniStore selbst erzeugt keine Links mit `fr-ch`; der
Fall entsteht nur, wenn jemand die Adresse von Hand tippt oder ein fremder Link sie so trägt.
