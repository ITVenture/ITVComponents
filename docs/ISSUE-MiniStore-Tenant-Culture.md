# Issue: Eine Sprache je Mandant — am Tenant oder am Firmenprofil?

**Status:** umgesetzt als **B** (2026-11-16, noch nicht committet) — Leitfaden §106. `Tenants.Culture` (nullbar,
wie gewählt — abweichend von Punkt 3 auch spezifisch, `de-CH`, siehe unten); `ISecurityRepository.GetTenantLocale(scope)` (ohne Default-Umsetzung) vererbt Zeitzone und Sprache je
für sich im Baum, in einer Abfrage; `ITenantLocaleResolver` ergänzt die Host-Vorgaben
(`TimeZoneInfo.Local`, `DefaultRequestCulture`/erste `SupportedUICulture`, `MapCulture`). Erfassung im
MudBlazor- und Telerik-Onboarding sowie im `TenantDialog`. Baum-Hosts müssen die Prozeduren neu ausrollen
(§106.4).
**Datum:** 2026-11-16
**Quelle:** MiniStore-Session (Konsument), beim Vorbereiten der Mehrsprachigkeit (de/fr/it/en).
**Toolkit-Stand:** `5.0.0-PRE271`.

## Der Fall

Der MiniStore stellt seine Oberflächen auf vier Sprachen um. Für die Oberfläche genügt die Sprache des
Lesers (`CurrentUICulture`). Für **Dokumente, die der Mandant ausgibt**, genügt sie nicht:

- Der **Kassenbon** entsteht im Stripe-Webhook, am anonymen Self-Checkout und an einer unbeaufsichtigten
  Station. Dort gibt es keinen angemeldeten Benutzer, und die Browsersprache eines Kunden ist die falsche
  Grundlage: der Bon ist ein Beleg des Ladens und soll in der Sprache des Ladens entstehen, gleich wer
  an der Kasse steht.
- Dasselbe gilt für den Kassenabschluss (Z-Bon) und die Mail mit dem Beleg.

Im Toolkit gibt es dafür heute **keine Angabe**. In der ganzen Datenbank des MiniStore steht keine Spalte
für Sprache oder Kultur, die einem Mandanten gehört (vorhanden sind nur `ConsentRecords.Culture`, die
Hilfe-Tabellen und `WidgetLocales`). Die nächste Verwandte ist `Tenants.TimeZone`.

Das Toolkit hätte selbst Verwendung dafür: die eigenen Mails (Einladungen, Abo-Rechnungen),
Rechnungs-PDFs, und die bevorzugte Sprache eines Stripe-Connect-Kontos.

## Was in jedem Fall dazugehört

Unabhängig vom Ablageort:

1. **Ein Auflöser statt direktem Spaltenzugriff:** „Sprache für Mandant X“, mit Rückfall in dieser
   Reihenfolge: eigener Wert → übergeordnete Mandanten im Baum (eine Filiale ohne eigene Angabe, ADM
   ohne Profil) → `Localization:DefaultCulture` bzw. die erste der `SupportedCultures`.
2. **Über Mandanten-Id/Scope und ohne angemeldeten Benutzer** auflösbar, denn der Bon entsteht im
   Webhook und unter einer anonymen Freigabe.
3. **Gespeichert wird die neutrale Sprache** (`de`, `fr`, nicht `de-CH`). Das Zahlformat regelt der Host
   über `CultureOptions.MapCulture`. Zur Auswahl stehen nur die `SupportedCultures`.
4. **Nullbar:** leer heisst Rückfall. Eine Datenmigration ist nicht nötig.
5. **Erfassung** im Onboarding (vorbelegt mit der aktuellen UI-Sprache) und nachträglich änderbar —
   wo, hängt am Ablageort.

**Abweichung von Punkt 3 (Toolkit-Entscheid):** gespeichert wird die Kultur, wie gewählt — `de-CH`, `de-DE` oder nur `de`. Zur Ortseinstellung gehört auch das Format, und das ist Sache des Mandanten: zwei Läden in verschiedenen Ländern auf demselben Host liessen sich mit einer Zuordnung des Hosts (`MapCulture`) nicht unterscheiden. Für die Texte schadet das nicht, der Localizer fällt `de-CH` → `de` → neutral zurück. Wählbar sind die `SupportedUICultures`, wie der Host sie konfiguriert; eine nur als Sprache gespeicherte Kultur bekommt ihr Format weiterhin über `MapCulture`.

## Die Abwägung: wohin?

### A — am Firmenprofil (`BillingProfileBase`)

Das war der erste Gedanke: der Tenant ist absichtlich schlank, und das Firmenprofil trägt ohnehin die
Angaben zum Kunden (Firmenname, Kontakt, MWST-Nummer, Adressen).

- **Für A spricht:** Die Sprache ist eine Angabe über den Kunden, wie seine Adresse. Sie wird dort
  erfasst und gepflegt, wo der Kunde seine übrigen Angaben pflegt. `Tenant` bleibt unberührt.
- **Gegen A spricht:** Es gibt sie nur, wo das **Onboarding** im Einsatz ist. Ein Host ohne Onboarding
  (oder mit der DB-losen Konfiguration aus `Extras/DbLessConfig`) hätte keine Sprache je Mandant. Und
  nicht jeder Mandant hat ein Profil: ADM hat keines, und ein Profil entsteht in der Admin-Maske erst
  beim ersten Speichern. Der Auflöser müsste damit über zwei Kontexte hinweg fragen.

### B — am Tenant, neben `TimeZone`

- **Für B spricht:** **Die Zeitzone ist der Präzedenzfall.** Sie liegt am Tenant, weil sie auch in
  Projekten ohne Onboarding gebraucht wird, und sie ist über `ISecurityRepository.GetTimeZoneHelper(scope)`
  in **jeder** Umsetzung erreichbar, auch in `SettingsSecurityRepository` (DB-los, dort
  `TimeZoneInfo.Local`). Die Sprache ist dieselbe Art Angabe, nämlich die Ortseinstellung des Mandanten.
  Wer die Zeitzone generell braucht, braucht die Sprache aus denselben Gründen: Datum und Uhrzeit auf
  einem Beleg ohne die passende Sprache sind nur die halbe Lösung. Ein `GetCulture(scope)` daneben
  wäre in allen Umsetzungen gleich erreichbar, und der Baum-Rückfall liesse sich in der
  Sicherheitsschicht lösen, die den Baum ohnehin kennt.
- **Gegen B spricht:** eine Spalte mehr an einer Entität, die bewusst schlank bleiben soll, und eine
  Migration in jedem Host (wie damals `TenantTimeZone`). Das Onboarding müsste beim Anlegen in den
  Tenant schreiben statt ins Profil. Das Telerik-Onboarding tut das für die Zeitzone bereits
  (`CompanyInputModel` führt sie), das MudBlazor-Onboarding (`OnboardingViews`) erfasst sie bisher
  nicht. Dort kämen Sprache und Zeitzone also gemeinsam neu dazu.

### C — als `TenantSetting`

Der Vollständigkeit halber: ein Schlüssel in den Mandanten-Einstellungen bräuchte keine Schemaänderung,
und die Vererbung im Baum gäbe es dort schon. Er wäre aber nur ein Text unter einem verabredeten
Schlüssel, ohne Prüfung, ohne Platz in den Masken und nicht über `ISecurityRepository` erreichbar.
Aus Sicht des MiniStore die schwächste Variante.

### Einschätzung aus dem MiniStore

**Eher B.** Der Grund, aus dem die Zeitzone am Tenant liegt, gilt für die Sprache genauso, und die
Ablage neben ihr gibt beiden denselben Zugang in jeder Umsetzung. „Der Tenant bleibt schlank“ spricht
gegen *Kundendaten* am Tenant. Die Sprache ist aber wie die Zeitzone eine Ortseinstellung und
kein Kundendatum. Für den MiniStore selbst funktionieren beide Varianten; massgeblich ist, was für
die Hosts ohne Onboarding nötig ist, und das kann nur die Toolkit-Seite beurteilen.

## Umgehung

Bis dahin entsteht der Bon im MiniStore weiter fest auf Deutsch (`de-CH`). Die Umstellung der
Oberflächen hängt nicht daran.
