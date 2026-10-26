# ISSUE: Eine Freigabe je Ort — der Teilen-Knopf zeigt eine bestehende, statt eine zweite auszustellen

**Status:** umgesetzt (Future_10, Leitfaden §85; `CreateAsync` wendet die Regel selbst an, `RootPath` gehört zum
Ort) · **Datum:** 2026-10-25 · **Version:** 5.0.0-PRE261 · **Quelle:** MiniStore, Laden-QR an der Kasse
und Selbstbedienungs-Station

## Der Fall

An der Kasse hängt ein Knopf „Laden-QR für den Eingang“. Er stellt eine **gespeicherte, anonyme** Freigabe
der Vorlage `pos-start` aus (Pfad `^/checkout/start$`, keine Argumente, keine Frist), die ausgedruckt an der
Ladentür hängt. Bis jetzt war das ein `<ShareButton Path="/checkout/start" />`.

Der Knopf stellt **bei jedem Druck eine neue Freigabe aus.** Wer den Aushang nur nochmals sehen oder
nachdrucken will, hat danach zwei gültige, unbefristete Geheimnisse im Umlauf. Jedes steht unter „Meine
Freigaben“, bis es jemand von Hand zurückzieht. Nach ein paar Wochen Kassenbetrieb sind es Dutzende, und
keiner weiss mehr, welches davon an der Tür hängt.

Dasselbe Muster gibt es ein zweites Mal: Die Selbstbedienungs-Station läuft unter einer gespeicherten,
anonymen Freigabe der Vorlage `pos-kiosk` mit dem Argument `clientAppId`. Damit nicht jeder Start eine neue
erzeugt, merkt sich MiniStore den Schlüssel in einer eigenen Spalte (`PosTills.KioskAssetKey`) und fragt
beim nächsten Start `GetLinkAsync` darauf ab.

Für den Laden-QR hat MiniStore jetzt einen eigenen Dienst (`ShopEntranceShare`), der in `SharedAssets` die
jüngste gültige Freigabe der Vorlage im Mandanten sucht, über `GetLinkAsync(…, anonymous: true, …)` ihren
Link holt und nur anlegt, wenn es keine gibt. Den Link zeigt er im `ShareLinkDialog` mit `QrFirst`. Das ist
zweimal dieselbe Logik im Host, und sie gehört an die Vorlage.

## Betroffene Stellen

| Stelle | Heute |
|---|---|
| `ShareButton.razor`, `Open()` | öffnet immer den `ShareDialog`, danach `Handler.CreateAsync` |
| `ISharedAssetAdminHandler.CreateAsync` | legt immer eine neue Zeile an |
| `SharedAssetAdminHandler.GetLinkAsync` (≈ Z. 92) | liefert den anonymen Link, wenn der Aufrufer „Besitzer“ ist (`asOwner: true`, d. h. Recht + Feature der Vorlage) — **das passt bereits** |
| `SharedAssetInfoProvider.GetAssetInfo` (≈ Z. 98) | Besitzerrecht hängt an der Vorlage, nicht an der Person, die angelegt hat — ein zweiter Kassierer bekommt also denselben Link |
| `ShareLinkDialog.razor` | kein Weg, das Geheimnis zu erneuern |

## Vorschlag

### 1. Ein Schalter an der Vorlage

`AssetTemplates.ReuseStoredShare` (bool, Vorgabe `false`, bestehende Vorlagen unverändert). Ob es je Ort nur
einen Aushang geben soll, ist eine Eigenschaft des **Zwecks** und nicht der einzelnen Freigabe: Laden-QR und
Station sind „einer je Ort“, ein verschickter Link auf einen Einkauf ist das nicht. Pflegbar in der
Vorlagen-Maske.

### 2. Was „derselbe Ort“ heisst

Eindeutig ist eine Freigabe über **Mandant + Vorlage + Argumentwerte**:

- `pos-start` (keine Argumente): eine je Laden.
- `pos-kiosk` (`clientAppId`): eine je Kasse. Ohne die Argumente bekäme Kasse 2 den Link von Kasse 1.

Die Argumentwerte stehen als JSON in `ArgumentValuesJson`. Verglichen werden sollten sie normalisiert
(Schlüssel sortiert), nicht als Zeichenkette.

### 3. Nur gespeicherte, anonyme Freigaben ohne Empfänger

- **Ad-hoc-Tickets** stehen gar nicht in der Tabelle. Für sie gilt der Schalter nicht.
- **Freigaben mit Empfängern** (Benutzer- oder Mandantenfilter) gelten ebenfalls nicht: wer zum zweiten Mal
  teilt, meint einen anderen Empfänger. Wiederverwenden hiesse, ihm den Link des ersten zu geben.
- Kandidaten sind nur Zeilen, die **jetzt** gelten (`NotBefore`/`NotAfter`).

### 4. Der Teilen-Knopf

Mit gesetztem Schalter:

- Gibt es eine passende Freigabe und liefert `GetLinkAsync` dem Aufrufer einen Link: **direkt** den
  `ShareLinkDialog` zeigen, ohne `ShareDialog`. `QrFirst` wird durchgereicht wie heute.
- Gibt es keine (oder keine, deren Link der Aufrufer bekommt): wie heute ausstellen.

Dieselbe Regel sollte `CreateAsync` selbst anwenden (bzw. eine neue Methode `GetOrCreateAsync`), damit ein
Host, der im Code ausstellt (MiniStore: Station), nicht am Knopf vorbei doch eine zweite anlegt. Das
Ergebnis sollte sagen, ob neu angelegt wurde (`ShareResultViewModel.Created` o. ä.).

### 5. Erneuern statt neu ausstellen

Mit dem Schalter lässt sich keine zweite Freigabe mehr ausstellen. Wird ein Aushang missbraucht, braucht es
trotzdem einen Weg, ihn zu entwerten: **„Erneuern“ im `ShareLinkDialog`** (`RotateAsync`), sichtbar nur für
Besitzer. Danach zeigt der Dialog den neuen Link und QR. Ohne diesen Knopf muss man dafür in „Meine
Freigaben“.

### 6. Zwei gleichzeitige Klicks

Ein eindeutiger Index geht schlecht, weil die Argumente als JSON gespeichert sind. Entschieden:

- **Doppelte sind erlaubt, genommen wird die jüngste** (`SharedAssetId` absteigend).
- **Die ältere wird NICHT implizit widerrufen.** Bei einem Wettlauf haben beide Teilenden einen QR gesehen,
  und einer hat womöglich den älteren gedruckt. Ihn still zu entwerten hiesse, einen Aushang tot an die Tür
  zu hängen, ohne dass es jemand merkt. Er bleibt also gültig, wird aber nie mehr angezeigt.
- Findet die Suche mehr als eine Kandidatin, **eine Warnung ins Log** (Vorlage, Mandant, Schlüssel). Aufräumen
  geht bewusst über „Meine Freigaben“.

## Was MiniStore danach tut

- Kasse zurück auf `<ShareButton Path="/checkout/start" QrFirst="true" />`, `ShopEntranceShare` entfällt.
- Station: `PosTills.KioskAssetKey` entfällt, der Start ruft `GetOrCreateAsync`.
- Migration: `ReuseStoredShare = true` für `pos-start` und `pos-kiosk`.

## Umgehung (heute)

`ShopEntranceShare` in MiniStore (`src/MiniStore.Web/Services/ShopEntranceShare.cs`): sucht direkt in
`SharedAssets` (Mandant, `Template.SystemKey`, anonym, gültig, jüngste zuerst), holt den Link über
`GetLinkAsync` und zeigt den `ShareLinkDialog` mit `QrFirst = true`; sonst `CreateAsync`. Für die Station
der Schlüssel in `PosTills.KioskAssetKey`.
