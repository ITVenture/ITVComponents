> **AUFGENOMMEN in PRE233** als `<ShareCurrentButton />`, Leitfaden §54.6a. Eure Begründung hat
> getragen: weitergeben ist kein Ausstellen, und ein zweites Geheimnis für denselben Warenkorb ist
> schlechter, nicht besser.
>
> Der Knopf zeigt `NavigationManager.Uri` im bestehenden `ShareLinkDialog` — also mit Kopieren,
> QR-Code, PNG und Druck. **Die Restlaufzeit ist drin**, wie von euch vorgeschlagen: bei einer
> befristeten Freigabe steht sie als eigene Zeile im Dialog, unter fünf Minuten in Warnfarbe.
>
> **Eine Abweichung von eurem Entwurf:** sichtbar wird er über `AssetContext`, nicht über `HasAsset`.
> `HasAsset` beantwortet der Pfad allein und sagt nur, dass ein Abschnitt *da* ist — nicht, dass er noch
> auf etwas Gültiges zeigt. Mit `HasAsset` würde der Knopf auch den Link einer abgelaufenen,
> widerrufenen oder von der Gültigkeitsregel beendeten Freigabe weiterreichen, und der Empfänger liefe
> in eine Sackgasse.
>
> Zu eurer Rückfrage: ihr braucht `ShareLinkDialog` jetzt nicht mehr selbst zu öffnen — nehmt den
> Knopf. Er bleibt `public`, aber der Vertrag, auf den ihr euch verlassen könnt, ist
> `<ShareCurrentButton />`.

# Issue: „Zeig mir meinen eigenen Link als QR-Code" — Weitergabe ohne neue Freigabe (Anstoß aus MiniStore)

**Status:** OFFEN — Wunsch, kein Fehler. Bewusst als **eigener** Vorgang gemeldet, obwohl er
denselben Dialog berührt wie `ISSUE-MiniStore-AssetTemplate-Dialog-Defaults.md`: dort geht es um
das **Ausstellen** einer Freigabe, hier um das **Weiterreichen** einer bestehenden.
**Datum:** 2026-09-12
**Quelle:** MiniStore (Kassensystem, Self-Checkout über SharedAssets)

## Der Fall

Ein Kunde scannt den QR am Ladeneingang, bekommt ein anonymes Ad-hoc-Ticket auf seinen Einkauf und
füllt den Warenkorb am eigenen Telefon. Jetzt kommt der Partner dazu und will mitscannen — **auf
denselben Korb**.

Was er dafür braucht, hat er längst: **die Adresse, auf der er gerade steht.** Sie trägt das
Ticket, sie zeigt auf genau diesen Auftrag, sie läuft zur selben Zeit ab. Es fehlt nur der Weg vom
einen Telefon zum anderen — und das ist ein QR-Code, kein neuer Vorgang.

Heute führt der `<ShareButton />` an dieser Stelle über `ShareDialog` → `CreateAsync` → **eine
zweite Freigabe**. Das ist mehr, als gebraucht wird, und es ist auch nicht dasselbe:

* Es entsteht ein **zweites** Geheimnis mit eigener Frist. Wird der Einkauf bezahlt, muss die
  Gültigkeitsregel beide einziehen — sie tut das, aber es sind eben zwei Dinge statt einem.
* Der Kunde ist **anonym**. Ob er überhaupt teilen *darf*, hängt daran, dass seine Vorlage ihm das
  Ausstellungsrecht gewährt. Für „gib das Blatt weiter, auf dem du gerade sitzt" ist diese Frage
  nicht nötig — wer den Link hat, hat ihn.
* Im Protokoll stehen zwei Freigaben, wo ein Einkauf stattgefunden hat.

## Der Wunsch

Ein Dialog, der **die aktuelle Adresse** als QR-Code zeigt — ohne `CreateAsync`, ohne Vorlagenwahl,
ohne Formular. Arbeitstitel `ShowCurrentAssetCode`.

Inhaltlich ist das fast nichts Neues: `ShareLinkDialog` kann seit PRE230 bereits QR anzeigen,
speichern und drucken. Es fehlt nur der Einstieg, der ihm die **laufende** URL gibt statt einer neu
erzeugten:

```csharp
// sinngemäss
var link = navigation.Uri;                       // trägt Abschnitt, Mandant und Pfad bereits
await DialogService.ShowAsync<ShareLinkDialog>("Link",
    new DialogParameters<ShareLinkDialog> { { d => d.Link, link }, { d => d.Title, title } });
```

Sinnvoll nur, wenn `ISharedAssetContext.HasAsset` gilt — sonst wäre es ein Link, der beim Empfänger
an der Anmeldung endet. Ein Knopf, der sich sonst gar nicht erst zeigt, passt zum Verhalten des
`<ShareButton />`, der ebenfalls nichts zeichnet, wo es nichts zu teilen gibt.

## Das brauchen wir nicht zwingend von euch

Das lässt sich im Host bauen — es ist ein Dialogaufruf mit `navigation.Uri`. Wir melden es
trotzdem, aus zwei Gründen:

1. **Es ist nicht MiniStore-spezifisch.** Jede anonyme Freigabe, die auf einem Mobilgerät landet,
   hat denselben Fall: weitergeben, nicht neu ausstellen. Im Host gebaut, entsteht es in jedem Host
   neu — und die QR-Erzeugung dafür liegt bereits bei euch.
2. **Es gehört neben den `<ShareButton />`, nicht daneben gebastelt.** Wenn der eine Knopf „neu
   ausstellen" heisst und der andere „diesen hier weitergeben", sollten beide gleich aussehen und
   dieselbe Sichtbarkeitslogik haben.

Wenn ihr es nicht aufnehmen wollt, ist das völlig in Ordnung — dann bauen wir den Knopf bei uns.
Sagt uns in dem Fall bitte nur, ob wir `ShareLinkDialog` direkt öffnen dürfen (er ist heute
`public` erreichbar) oder ob wir uns auf die eigene Anzeige verlassen sollen, damit wir nicht an
einem internen Vertrag hängen.

## Ein Sicherheitspunkt, der dazugehört

Ein solcher Knopf macht sichtbar, was ohnehin gilt: **wer den Link hat, ist drin.** Das ist bei
einem anonymen Ticket die Bauart, nicht ein Versehen — der Warenkorb ist genau dafür geteilt.

Der Vollständigkeit halber gehört in einen solchen Dialog aber derselbe Satz, den `ShareLinkDialog`
heute schon trägt: *„Anyone holding this link uses the share."* Bei einem Ad-hoc-Ticket zusätzlich
die **Restlaufzeit** — sie steht im Ticket (`NotAfter`), und sie ist die Antwort auf die Frage, die
der Empfänger als Nächstes hat.

## Umgebung

| | |
|---|---|
| Toolkit | `5.0.0-PRE232` |
| Host | .NET 10, Blazor Web App (Server), MudBlazor 9 |
| Freigabe | `pos-checkout`, anonymes Ad-hoc-Ticket, 60 Minuten, Argument `orderId` unter `Strict`, `ValidityRuleKey = order-open` |
