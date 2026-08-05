# Issue: Konto-Seiten — Navigation mobil unbrauchbar, Bereich wirkt wie eine Fremdanwendung

**Status:** BEIDE Anliegen umgesetzt (Future_10, noch nicht publiziert) — Host-Test offen. Die Begruendung
in Anliegen 2 traegt allerdings nicht; der Weg dorthin war ein anderer, siehe Nachtrag am Ende.
**Datum:** 2026-08-05
**Quelle:** MLMManager-Session (Konsument), beim Mobil-Durchgang über `/Account/Manage`.
**Toolkit-Stand:** `5.0.0-PRE159` · **MudBlazor:** `9.6.0`
**Betroffen:**
- `…Blazor.MudBlazor.IdentityPages/Components/Account/Shared/ManageNavMenu.razor`
- `…Blazor.MudBlazor.IdentityPages/Components/Account/Shared/ManageLayout.razor`
- `…Blazor.MudBlazor.IdentityPages/Components/Account/Pages/Manage/*.razor` (15 Seiten)

> **Kurzfassung:** Zwei Anliegen. (1) Die Konto-Navigation frisst mobil **61 % der Bildschirmhöhe**,
> bevor der Inhalt beginnt — sie sollte dort ein Dropdown sein. (2) Der Konto-Bereich fühlt sich wie
> eine andere Anwendung an, weil er static SSR ist. Der dafür angenommene Zwang ist aber **schwächer
> als gedacht**: `RefreshSignInAsync` kommt im ganzen Paket **nicht ein einziges Mal** vor.
>
> ⚠️ **Der letzte Satz ist falsch** — der Aufruf sitzt eine Ebene tiefer in den Handlern und wird sehr
> wohl erreicht. Siehe „Nachtrag der Toolkit-Session" am Ende; Anliegen 2 ist damit neu zu bewerten.

## Anliegen 1 — Die Navigation ist mobil ein Rollo vor dem Inhalt

`ManageNavMenu.razor` rendert sieben `MudNavLink` untereinander, ohne Breakpoint-Unterscheidung.
`ManageLayout.razor:27-36` legt sie in `MudItem xs="12" md="3"` — auf `md` aufwärts also eine
Seitenspalte, darunter ein voller Block **über** dem Inhalt.

**Gemessen** (angemeldet, Gerät 400 × 978, Seite `/Account/Manage`):

| | Wert |
|---|---|
| Höhe des Navigationsblocks | **596 px** |
| Sichtbare Viewporthöhe | 978 px |
| Anteil | **61 %** |

Das Formular („Profil" mit Benutzername und Telefonnummer) beginnt erst darunter. Auf jeder der
15 Konto-Seiten scrollt man zuerst an derselben Liste vorbei.

### Vorschlag

Auf `xs`/`sm` statt der Liste ein Dropdown, das den **aktiven** Eintrag zeigt und die übrigen
aufklappt. Ab `md` bleibt die Seitenspalte wie sie ist.

**Wichtig für die Umsetzung — es muss ohne Circuit funktionieren.** Die Konto-Seiten sind static SSR
(siehe Anliegen 2), deshalb scheidet der Weg aus, den die Tasks-Liste inzwischen geht:
`<MudHidden Breakpoint="…" @bind-Hidden="narrow" />` misst per JS-Interop und braucht einen Circuit —
hier bliebe `narrow` schlicht auf seinem Startwert.

Tragfähig ist stattdessen **natives `<details>/<summary>` plus Media Query**: reines HTML/CSS, kein
JS, kein Circuit. MLM benutzt genau dieses Muster im Hauptmenü (`NavMenuItem.razor`), gerade *weil*
es auf den static-SSR-Identity-Seiten funktionieren muss. Das `<summary>` trägt den aktiven
Eintrag, der geöffnete Zustand die restlichen Links; ab `md` wird `<details>` per CSS auf `open`
gestellt bzw. der Aufklapp-Pfeil ausgeblendet.

## Anliegen 2 — Der Bereich wirkt wie eine Fremdanwendung

Beim Wechsel von einer normalen Seite auf `/Account/Manage` ändert sich sichtbar mehr als der Inhalt:
Der Tenant-Umschalter wird zu einem rohen Aufklapp-Element, der Sprachwähler zu einem ungestylten
`<select>`, das auf 400 px zweizeilig umbricht, und die Typografie springt. Jede Navigation innerhalb
des Bereichs ist ein Full-Page-Load statt eines weichen Wechsels.

Die Ursache steht im Kommentar von `ManageLayout.razor:12-19` und ist sauber begründet: Die Seiten
tragen `[ExcludeFromInteractiveRouting]`, das umgebende OuterLayout bringt aber interaktive
Root-Komponenten mit; Enhanced Navigation würde versuchen, diese über die SSR-Grenze zu reconcilen
(`updateRootComponents` → SignalR-Fehler), deshalb `data-enhance-nav="false"` und damit ein
vollständiger Seitenaufbau bei jedem Klick.

### Was die Annahme dahinter nicht trägt

Der Zwang zu static SSR wird üblicherweise mit dem Cookie-Neuaufbau nach Login-Änderungen begründet
(„bei externen Logins ist ein Refresh nötig"). Über alle 15 Seiten in `Pages/Manage/` ausgezählt:

| Aufruf | Treffer |
|---|---|
| `RefreshSignInAsync` | **0** — im ganzen Paket nicht vorhanden |
| `SignInAsync` | **0** |
| `SignOutAsync` | **2** — nur `ExternalLogins.razor` und `ExternalLoginsCallback.razor` |
| `HttpContext` | 3–9 pro Seite, überall |

Und wofür `HttpContext` auf der Profilseite (`Index.razor`) tatsächlich gebraucht wird:

- `HttpContext.User` (Zeilen 84, 90, 108, 114) — dasselbe liefert interaktiv der
  `AuthenticationStateProvider`
- `HttpMethods.IsPost(HttpContext.Request.Method)` (Zeile 95) — die Post-Erkennung des
  Static-SSR-Formularmusters, die eine interaktive Seite gar nicht braucht

Damit hat mindestens die Profilseite **keinen** technischen Grund für static SSR. Die
`HttpContext`-Nutzung ist geerbtes Template-Muster, keine Notwendigkeit.

### Vorschläge, von konservativ nach weitgehend

1. **Nur das Nötige statisch lassen.** `[ExcludeFromInteractiveRouting]` behalten für
   `ExternalLogins`, `ExternalLoginsCallback` und den Login-/Logout-Pfad; die übrigen Konto-Seiten
   interaktiv rendern. Dann entfällt für sie auch `data-enhance-nav="false"`, das Chrome bleibt
   stehen, Navigation im Bereich wird weich. Pro umgestellter Seite: `HttpContext.User` →
   `AuthenticationStateProvider`, Form-Post → `EditForm` mit `OnValidSubmit`.
   **Vorsicht bei `Passkeys`/`RenamePasskey`** — WebAuthn läuft über JS-Interop, das Zusammenspiel
   mit dem Formular ist zu prüfen, bevor sie mit umgestellt werden.

2. **Gemischt mit klarer Grenze.** Der Konto-Bereich bekommt ein eigenes, interaktives Layout; die
   zwei bis drei wirklich statischen Seiten behalten `data-enhance-nav="false"` als Insel. Der Bruch
   bliebe, aber nur noch an zwei Stellen statt auf allen fünfzehn.

3. **Wenn statisch bleiben muss: den Bruch unsichtbar machen.** Das Chrome so bauen, dass es in
   beiden Modi gleich aussieht — also die im OuterLayout interaktiv gerenderten Bedienelemente durch
   Varianten ersetzen, die static SSR überstehen (dasselbe `<details>`-Muster wie oben). Das ist die
   aufwändigste Variante, weil sie jedes Shell-Element betrifft, und sie behält den Full-Page-Load.

Aus MLM-Sicht ist **Vorschlag 1** der interessanteste: er löst Ursache statt Symptom, und die
Zählung oben legt nahe, dass der Aufwand pro Seite klein ist.

## Abgrenzung — was MLM selbst macht

Der Tenant- und der Sprachumschalter in der Kopfzeile sind **MLM-eigene** Komponenten
(`MLMManager.Web/Components/Layout/`), nicht Toolkit. Dass sie auf den Konto-Seiten anders aussehen,
ist MLMs Baustelle und wird dort gelöst — hier nur erwähnt, weil es zum Gesamteindruck „andere
Anwendung" beiträgt und weil Vorschlag 1 dieses Symptom nebenbei mit erledigen würde.

## Nachtrag der Toolkit-Session — Korrektur und Umsetzung

### Die Zaehlung in Anliegen 2 misst am Ziel vorbei

`RefreshSignInAsync` kommt in den 15 Seiten tatsaechlich nicht vor — aber es wird sehr wohl erreicht. Die
Seiten rufen die Handler-Fassade, nicht die Identity-API:

| Seite | Aufruf in der Seite | landet in |
|---|---|---|
| `Manage/Index.razor:128` | `Handler.RefreshSignIn(user)` | `IndexHandler.cs:46` → `signInManager.RefreshSignInAsync` |
| `Manage/ChangePassword.razor:133` | `Handler.RefreshSignIn(user)` | `ChangePasswordHandler.cs:44` |
| `Manage/SetPassword.razor:129` | `Handler.RefreshSignIn(user)` | `SetPasswordHandler.cs:55` |
| `Manage/ExternalLogins.razor:174` | `HttpContext.SignOutAsync` | direkt |
| (ausserdem) | | `ResetAuthenticatorHandler.cs:47`, `ExternalLoginsHandler.cs:79` |

`RefreshSignInAsync` schreibt das Auth-Cookie und braucht dafuer eine echte HTTP-Antwort; ueber einen
Circuit geht das nicht. **Auch die Profilseite braucht static SSR** — der Schluss „mindestens die
Profilseite hat keinen technischen Grund dafuer" gilt nicht.

Damit ist **Vorschlag 1 nicht der billige, sondern der teuerste Weg**, und Vorschlag 2 (gemischt, klare
Grenze) ist der realistische. Was er braucht, ist eine Seite-fuer-Seite-Audit statt einer Pauschalannahme:
`Email`, `TwoFactorAuthentication`, `PersonalData`, `GenerateRecoveryCodes` und `Passkeys` sind Kandidaten,
`Index`, `ChangePassword`, `SetPassword`, `ResetAuthenticator`, `ExternalLogins*` sind es nicht.

### Was stattdessen umgesetzt wurde

**Anliegen 1 — erledigt.** `ManageNavMenu` rendert die Eintraege ab `md` als Seitenspalte und darunter als
`<details>/<summary>`, das den aktiven Eintrag traegt. Die Eintraege kommen aus **einer** Liste, die beiden
Fassungen sind nur zwei Huellen darum. Ein einzelnes `<details>` auf breiten Schirmen per CSS „offen" zu
erzwingen, ist quer durch die Browser nicht verlaesslich — die Sichtbarkeit ueber Media Query zu schalten
ist es. Die Regeln liegen in `itv-mudblazor.css`, nicht als scoped `.razor.css`: diese Datei liefert der
Host ueber `<ITVentureReferences />` im `<head>` aus, unabhaengig vom scoped-CSS-Bundle — auf genau diesen
Seiten der Unterschied zwischen „wirkt" und „wirkt still nicht".

**Ein Teil des „Fremdanwendung"-Eindrucks war gar keine Rendermodus-Frage.** Die Seiten trugen
Bootstrap-Klassen aus dem Identity-Template: `form-floating`, `form-control`, `text-danger` — 12 Stellen in
7 Dateien, dazu `btn btn-primary` in `Passkeys`. In einer Anwendung ohne Bootstrap heisst das: voellig
ungestylt, und `form-floating` notiert das Label bewusst **nach** dem Eingabefeld (es legt es per CSS
darueber) — ohne Bootstrap steht die Beschriftung also unter ihrem Feld. Das erklaert den Typografie-Sprung
mindestens mit. Die Klassen sind jetzt `itv-field*` und in `itv-mudblazor.css` an den Mud-Variablen
gestylt; `order:-1` stellt die Reihenfolge wieder her, ohne dass eine Seite ihre DOM-Reihenfolge aendern
muss. Das ist unabhaengig vom Rendermodus und ohne Risiko.

**Warum nicht gleich MudTextField:** die Bindung laeuft ueber `[SupplyParameterFromForm]` und braucht das
`name`-Attribut, das `InputText` aus dem EditContext ableitet. `MudTextField` ist kein `InputBase` und
setzt es nicht — der Post kaeme still ohne Werte an. Die Felder bleiben deshalb native Eingabefelder.

### Nebenbefund, der in der Meldung fehlt

`ManageNavMenu` ist eine feste Liste. Die Abo-Seite liegt unter `/Account/Manage/Subscription`, rendert
`InteractiveServer`, hat kein `@layout ManageLayout` — sie steht also weder in der Konto-Navigation noch im
Konto-Layout, und ein Paket wie `BillingViews` kann dort nichts eintragen. Eine erweiterbare
Manage-Navigation (DI-gesammelte Eintraege) waere die saubere Antwort. Nebenbei zeigt diese Seite, dass
eine interaktive Insel im Manage-Bereich schon heute laeuft.

### Die Rendermodus-Frage — geloest, aber anders als vorgeschlagen

Nicht „welche Seite darf interaktiv werden", sondern: **es gibt zwei verschiedene Probleme, und nur eines
davon ist der Rendermodus.**

- **Auswaerts** — ein Cookie muss geschrieben werden. Loesbar: die Seite arbeitet auf dem Circuit und
  navigiert danach mit `forceLoad` auf einen Endpunkt, der das Cookie schreibt und zurueckleitet.
- **Einwaerts** — die Seite muss einen Browser-Form-Post ueber `[SupplyParameterFromForm]` *empfangen*.
  Da hilft der Ruecksprung nicht, weil das Problem vor der Seite liegt.

Einwaerts betraf genau **eine** Seite (`Passkeys`), und auch dort nur, weil unser eigenes JS das Credential
per Formular zurueckgab. Alles andere war auswaerts.

**Umgesetzt: alle 14 Seiten unter `Account/Manage` rendern interaktiv.** Dazu:

- `ISignInSessionHandler` neu in `IdentityShared` — die drei Aufrufe, die zwingend eine HTTP-Antwort
  brauchen (`RefreshSignInAsync`, `SignOutAsync`, `ForgetTwoFactorClientAsync`), an einer Stelle statt in
  sieben Seiten-Handlern verstreut.
- Vier neue Endpunkte in `IdentityPagesEndpoints`: `RefreshSignIn`, `SignOutSession`, `ForgetBrowser`,
  `LinkLogin` — dazu `ExternalLoginsCallback` als fuenfter. `ManageRedirects` buendelt die Ruecksprünge,
  damit die Adressen nicht in sechs Seiten einzeln entstehen.
- `ExternalLoginsCallback.razor` ist **geloescht**: die Datei hatte kein Markup, war reine Durchgangs-
  station und ist jetzt ein Minimal-API-Endpunkt unter derselben Adresse.
- Drei Handler-Methoden bekamen einen Schalter (`DeleteAccount(…, signOut)`, `ResetAuthenticator(…,
  refreshSignIn)`, `RemoveExternalAuthentication(…, refreshSignIn)`), weil sie das Cookie bisher selbst
  schrieben. Default unveraendert `true` — die MVC-Seite ruft weiter wie bisher.
- `PasskeySubmit.js` bekam `attachPasskeyCreate`: der Klick wird in JS abgefangen, `credentials.create()`
  laeuft dort in der Benutzer-Geste, und nur das fertige Credential reist per `DotNetObjectReference`
  zurueck. Ueber SignalR hin und zurueck haette den Gesten-Kontext gekostet (Safari lehnt dann ab). Die
  Anmelde-Optionen erzeugt der Server beim Seitenaufbau, damit zur Klickzeit nichts mehr zu holen ist.
- Die Eingabefelder der Manage-Seiten sind jetzt **echte Mud-Komponenten** — dort ist kein Formular-Post
  mehr, also faellt der Grund fuer native Felder weg. Das `itv-field`-CSS bleibt fuer die anonymen Seiten
  (Anmelden, Registrieren, Passwort vergessen), die wegen `SignInAsync` static SSR bleiben muessen.
- `data-enhance-nav="false"` ist aus `ManageLayout` verschwunden.

**Zwei Verhaltensaenderungen zum Besseren**, die keine Meldung verlangt hatte: `Disable2fa` und
`EnableAuthenticator` stellen das Cookie jetzt neu aus. Beide stempeln ueber `SetTwoFactorEnabledAsync`
den Security-Stamp neu und liessen das Cookie bisher stehen — der Stamp-Validator haette den Benutzer
beim naechsten Intervall abgemeldet.

**Das verbleibende Zeitfenster:** aendert eine Seite den Security-Stamp, traegt die Ruecksprung-Anfrage
noch das alte Cookie. Der Validator prueft nur alle 30 Minuten (Default, im Toolkit nirgends
konfiguriert), greift also normalerweise nicht. Wer ein Formular laenger als das offen liegen laesst und
dann abschickt, landet auf der Anmeldung — die Aenderung ist trotzdem erfolgt. In der statischen Fassung
gab es diesen Fall nicht, weil Aenderung und Cookie in derselben Anfrage lagen.

**Offen geblieben:** die Manage-Navigation als Erweiterungspunkt (die Abo-Seite steht bis heute nicht
darin), und der Host-Test des Ganzen.

## Verifikationsstand

- Die Höhenmessung (596/978 px) stammt aus dem laufenden System, angemeldet, bei 400 px Gerätebreite.
- Die Aufrufzählung (`RefreshSignInAsync` 0, `SignOutAsync` 2, …) ist ein Grep über alle 15 Dateien in
  `Pages/Manage/`; die Zeilennummern zu `Index.razor` sind aus der Datei.
- **Nicht geprüft:** ob eine interaktiv gerenderte Konto-Seite tatsächlich funktioniert — das ist der
  eigentliche Test von Vorschlag 1 und braucht einen Umbau. Ebenso ungeprüft, ob es ausserhalb von
  `Pages/Manage/` (Login, Register, Passwort-Reset) Stellen gibt, die den statischen Modus zwingend
  brauchen; dort ist er wegen `SignInAsync` beim Anmelden auch klar erwartbar.
- **Nicht geprüft:** das Zusammenspiel von WebAuthn/Passkeys mit interaktivem Rendering.
