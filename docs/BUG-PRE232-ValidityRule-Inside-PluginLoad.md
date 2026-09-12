> **BEHOBEN in PRE233**, Leitfaden §55.11. Umgesetzt wurden **A**, **B** und **C** zusammen — sie
> hängen an derselben Ursache.
>
> Die Kette war vollständig so, wie ihr sie beschreibt, und sie ist von uns:
> `WebPluginHelper` → `VerifyUserPermissions` → `IsLegitSharedAssetPath` → `VerifyRequestLocation` →
> Auflösung → Regel. `IsLegitSharedAssetPath` hängt an **jeder** Berechtigungs- und Feature-Prüfung des
> Toolkits, und die läuft beim Laden jedes Plugins — daher die 138 Aufrufe und die
> Wiedereintrittssperre. Eingebaut haben wir das in PRE232, beim Verschieben der Regel aus dem
> Anmeldeweg. Damit stand sie zweimal hintereinander falsch; eure beiden Meldungen haben das jeweils
> aufgedeckt, und dafür danke.
>
> **A:** die Regel wird beim Auflösen einer Freigabe gar nicht mehr gefragt. Sie ist ein eigener
> Schritt (`ISharedAssetAdapter.VerifyAssetValidity`), gerufen vom seitenzugewandten Weg des
> `SharedAssetContext`. Dort steht der Geltungsbereich, und es läuft kein Ladevorgang.
> **B:** einmal je Vorgang, je Scope gepuffert — und nicht für Unterressourcen.
> **C:** die Zusage steht jetzt an `IAssetValidityRule`, und sie fällt dank A anders aus als von euch
> vermutet: **eine Regel darf ihren Fachkontext ganz normal über `IFreshInjectablePlugin<T>` leasen.**
>
> **Damit könnt ihr euren Notbehelf zurückbauen** — die eigene `NpgsqlConnection` und der direkte
> `Npgsql`-Verweis sind nicht mehr nötig, und der globale Mandantenfilter greift wieder.
>
> **Eine Grenze bleibt**, und die solltet ihr kennen: die Regel läuft auf dem Weg, den die Seite geht.
> Eine Seite, die weder `AssetContext` liest noch den Riegel bemüht, fragt sie nicht. Bei euch ist das
> unkritisch (`pos-checkout` steht auf `Strict`, der Riegel fragt mit), aber eine Vorlage mit
> `ArgumentEnforcement = None` und Gültigkeitsregel wäre eine Kombination, auf die man sich nicht
> verlassen sollte.

# BUG (PRE232): `IAssetValidityRule` wird aus einem laufenden Plugin-Ladevorgang gerufen — eine Regel kann dort nichts lesen

> Gemeldet aus **MiniStore** (Blazor Web App, .NET 10, `TenantSource.PathSegment`, TreeTenants,
> PostgreSQL, Toolkit `5.0.0-PRE232`).
>
> Direkte Folgemeldung zu `BUG-PRE231-AdHocTicket-Location-And-Scope.md`. Befund **C** dort war
> „die Gültigkeitsregel läuft ohne Mandanten-Geltungsbereich". Der Fix hat sie in den Riegel
> verschoben — und dort zeigt sich, dass das Problem tiefer liegt: **an dieser Stelle lässt sich
> überhaupt kein Plugin laden.**

## Übersicht

| | |
|---|---|
| **Betroffen** | `SharedAssetInfoProvider.cs:891` (`IsStillValid`, Aufruf) und `:952-975` (Auflösung der Regel) |
| **Kern** | Der Rückruf läuft **innerhalb** eines Plugin-Ladevorgangs. Versucht die Regel ihrerseits, einen Fachkontext über die Plugin-Fabrik zu leasen, schlägt die Wiedereintrittssperre zu (`PluginFactory.cs:1747`). |
| **Folge** | Jede Regel, die Fachdaten liest — und „gilt das noch?" ist fast immer eine Frage an Fachdaten — bekommt eine Ausnahme statt einer Antwort. Sie muss `false` liefern (fail closed), und die Freigabe fällt. |
| **Verstärkend** | Das passiert **je Anfrage**, nicht je Vorgang. Bei uns: 138 Regelaufrufe und 114 Wiedereintritts-Ausnahmen für *einen* Seitenaufruf. Aus einer Fehlermeldung wurde dadurch eine weisse Seite. |
| **Nicht betroffen** | Regeln, die ohne Datenzugriff auskommen (reine Zeit- oder Argumentprüfungen). Die dürften die Minderheit sein. |

## Beobachtung

Unsere Regel `order-open` beantwortet „gilt die Freigabe noch?" mit „ist der Auftrag noch offen?".
Sie hat den Fachkontext wie alles andere im Projekt über `IFreshInjectablePlugin<SalesDbContext>`
geleast — so, wie es der Leitfaden für Fachkontexte vorsieht.

Erster Versuch, `Lease()` ohne Namen (die Standardauflösung):

```
System.InvalidOperationException: No plugin could be resolved for a fresh 'SalesDbContext' lease (name '<default>').
   at ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins.Impl.FreshInjectablePluginImpl`1.Lease(String name)
   at MiniStore.Web.Services.OrderOpenValidityRule.IsValid(AssetArgumentValues values)
```

Nachvollziehbar: die Standardauflösung stellt den Mandanten-Präfix voran, und der existiert im
Rückruf nicht.

Zweiter Versuch, **namentlich** auf die global registrierte Zeile (`UniqueName = 'SalesContext'`,
`TenantId IS NULL`, `AllowAnonymous = true`):

```
System.InvalidOperationException: There already is a plugin-load in progress in this thread!
   at ITVComponents.Plugins.PluginFactory …
```

Das ist die eigentliche Schranke, und sie ist unabhängig von Name, Mandant und Filtern: **der
Rückruf läuft selbst innerhalb eines Ladevorgangs.** Ein Plugin lässt sich dort per Bauart nicht
laden.

Beide Zahlen aus **einem** Seitenaufruf über ein anonymes Ad-hoc-Ticket:

```
[138x] Asset validity rule 'order-open' could not check order 24.
[114x] System.InvalidOperationException: There already is a plugin-load in progress in this thread!
[ 90x] System.InvalidOperationException: No plugin could be resolved for a fresh 'SalesDbContext' lease (name 'SalesContext').
[138x] WARN  Die Gueltigkeitsregel 'order-open' beendet den Zugriff auf 'cc9ade…'.
```

## Warum das die Schnittstelle im Kern trifft

`IAssetValidityRule` ist ausdrücklich für Fragen gedacht, die ein Datum nicht beantworten kann — so
motiviert es der Leitfaden selbst: *„Bis der Auftrag abgeschlossen ist" ist kein Ablauf.* Eine
solche Frage ist ohne Datenzugriff nicht zu beantworten. Die Schnittstelle lädt also dazu ein,
genau das zu tun, was an ihrer Aufrufstelle nicht möglich ist.

Dazu kommt die Häufigkeit: der Riegel prüft laut §56.2 bewusst **je Vorgang**, und für das
Zugriffsprotokoll wird das auch eingehalten. Die Gültigkeitsregel wird hier aber offensichtlich je
Anfrage gefragt — sonst kämen für einen Seitenaufruf nicht 138 Aufrufe zusammen. Selbst eine Regel,
die funktioniert, zahlt diesen Preis dann mehrfach pro Seite.

## Was wir vorläufig gemacht haben

`OrderOpenValidityRule` öffnet jetzt eine **eigene** `NpgsqlConnection` aus
`ConnectionStrings:DefaultConnection` und liest einen Zustand über den Primärschlüssel. Kein
Fachkontext, keine Plugin-Fabrik, kein Geltungsbereich.

Das funktioniert, ist aber ausdrücklich ein Notbehelf und in der Klasse auch so kommentiert:

* Es umgeht den globalen Mandantenfilter. Vertretbar nur, weil die Frage eng ist — **ein** Zustand
  zu **einer** Nummer, die aus dem Ticket selbst stammt, und die Mandantenbindung des Tickets prüft
  das Toolkit seit PRE231 davor.
* Es zwingt uns zu einem direkten `Npgsql`-Paketverweis in einem Projekt, das sonst keinen braucht.
* Es ist nicht übertragbar: jeder Host müsste dieselbe Ausnahme bauen, und jeder müsste dabei
  dieselbe Abwägung selbst treffen.

## Fix-Vorschläge

### A) Den Rückruf aus dem Ladevorgang herausziehen (bevorzugt)

Die Gültigkeitsregel beantwortet eine fachliche Frage, keine Konstruktionsfrage. Sie muss nicht
während des Bauens des Asset-Objekts laufen. Wenn `GetTicketInfo` bzw. der Riegel die Regel
**nach** Abschluss des Ladevorgangs fragt — mit gesetztem Geltungsbereich —, kann eine Regel die
normale Infrastruktur benutzen, und der Host braucht keine Sonderbehandlung.

### B) Je Vorgang statt je Anfrage

Unabhängig von A: 138 Aufrufe für einen Seitenaufruf sind auch dann zu viel, wenn jeder gelingt.
Dieselbe Begründung wie in §56.2 für das Zugriffsprotokoll — die Regel gehört an die Stelle, an der
ein Vorgang beginnt.

### C) Mindestens: den Vertrag benennen

Falls A nicht geht, sollte an `IAssetValidityRule` stehen, **was** dort zur Verfügung steht und was
nicht: kein Mandanten-Geltungsbereich, keine Plugin-Fabrik, kein Fachkontext. Dann weiss ein
Implementierer vorher, dass er sich selbst versorgen muss, statt es über mehrere Runden
herauszufinden. Ein Beispiel in §55, wie eine solche Regel korrekt aussieht, wäre das
Naheliegendste.

## Umgebung

| | |
|---|---|
| Toolkit | `5.0.0-PRE232` |
| Host | .NET 10, Blazor Web App (Server + WASM Auto), `prerender: false` |
| Mandanten | `TenantSource.PathSegment`, `app.UseTenantPathPrefix()`, TreeTenants |
| Datenbank | PostgreSQL 17 (Npgsql 10.0.3), EF Core 10 |
| Fachkontext | Plugin-Fabrik, Zeile `SalesContext` global (`TenantId IS NULL`, `AllowAnonymous`) |
| Vorlage | `pos-checkout`, `AllowAdHoc`, `ValidityRuleKey = order-open`, Argument `orderId` unter `Strict` |
