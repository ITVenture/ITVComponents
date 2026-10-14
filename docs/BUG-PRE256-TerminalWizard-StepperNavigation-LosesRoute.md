# BUG PRE256: Terminal-Assistent — die eingebaute Stepper-Navigation überspringt `CollectServer`, das Gerät wird ohne Kasse und Objekt gespeichert

**Status:** behoben auf `Future_10`, noch nicht publiziert — eingebaute Stepper-Knöpfe abgeschaltet (leeres `ActionContent`, `OnPreviewInteraction` lässt nur Zurück zu), eigener Zurück-Knopf; `SaveAsync` im Assistenten speichert nur, wenn beide Abschnitte eingesammelt wurden; `TerminalsHandler.SaveAsync` prüft die `Required`-Felder des Wegs (`TerminalSettings.MissingRequired`); Log-Einträge in `TerminalsAdmin` und `GetTerminalStatusAsync`. Tests `MissingRequired_NamesWhatTheRowLacks_RouteAndJsonAlike`, `SaveRefusesATerminalThatLacksARequiredSetting` · **Datum:** 2026-10-14 · **Quelle:** MiniStore, erstes Agent-Terminal (Simulator) über `/Account/Manage/Payments/Terminals`

## Der Fall

Ein Agent-Terminal wurde über den Assistenten angelegt. Der Assistent meldete keinen Fehler, die Auswahl
der Kasse und des Geräte-Objekts hat sichtbar funktioniert (der Agent wurde für die Objektliste gefragt).
„Status prüfen“ meldet danach: nicht erreichbar.

Die gespeicherte Zeile:

```
 TenantPaymentTerminalId | TenantId | Provider | ProviderTerminalId | Route | DisplayName | ConfigurationJson
-------------------------+----------+----------+--------------------+-------+-------------+-------------------
                       1 |        7 | agent    | gaga               |       | gaga        |
```

`Route` **und** `ConfigurationJson` sind `NULL`. Damit wirft `AgentConnectorTerminalLocator.Address`
sofort `TerminalDeviceException("… names no till (route) …")` — es wird nie ein Proxy gebaut, der Agent
nie gefragt. Im Log steht nach dem Laden der Terminal-Zeile **nichts** mehr (siehe Punkt 2).

## 1. Ursache: der Stepper hat eigene Weiter-Knöpfe

`TerminalWizardDialog.razor:24`:

```razor
<MudStepper @bind-ActiveIndex="step" ShowResetButton="false">
```

`MudStepper` (MudBlazor 9.9.0) zeichnet eine **eigene** Navigationsleiste (`ShowNavigation`,
`ShowNextButton`, `ShowPreviousButton`). Abgeschaltet ist nur der Reset-Knopf. Der Assistent hat zusätzlich
**seinen** Knopf in den `DialogActions` (`NextAsync`), und nur der führt die Schritt-Logik aus:

| Schritt | Was `NextAsync` tut | Was der Stepper-Knopf tut |
|---|---|---|
| 1 → 2 | Pflichtfelder prüfen | nur `step++` |
| 2 → 3 | `CollectServer()` — setzt `routeValue` und `serverJson`; `LoadDeviceFieldsAsync()` | nur `step++` |

Wer den Knopf des Steppers benutzt, steht in Schritt 3 mit `routeValue == null`, `serverJson == null`,
`deviceFields == []` (also „keine Geräteeinstellungen“). `SaveAsync` sammelt nur bei `step == 1` nach — in
Schritt 3 nicht —, überspringt das Anwenden mangels Felder und speichert `Draft()` mit leerer Route und
leerem JSON.

**Beleg, dass `CollectServer` nie lief:** `TerminalSettings.Compose` liefert nie `null`, schlimmstenfalls
`"{}"`; `TerminalAdministration.SaveAsync` macht nur aus Leer-/Whitespace `NULL`. Ein `NULL` in
`ConfigurationJson` geht also nur über `serverJson == null`, und das bleibt nur so, wenn `CollectServer`
nicht mit Feldern gerufen wurde.

### Vorschlag

- Die eingebaute Navigation abschalten (`ShowNavigation="false"` bzw. die einzelnen Knöpfe) — der Dialog hat
  seine eigenen Knöpfe, zwei Sätze sind einer zu viel. Ebenso Klicks auf die Schritt-Köpfe unterbinden,
  falls die bei `Linear` doch weiterschalten.
- **Zusätzlich** robust machen: `SaveAsync` sammelt immer, wenn `serverFields.Count != 0` (nicht nur bei
  `step == 1`), und bei `hasDeviceStep` ohne geladene Gerätefelder entweder laden oder ablehnen.
- Serverseitig: `TerminalAdministration.SaveAsync` sollte für `Provider == "agent"` Route und
  `objectName` als Pflicht prüfen (oder generischer: jedes `Required`-Feld aus `DescribeSettings`).
  Heute lässt sich ein Agent-Terminal speichern, das nie funktionieren kann.

## 2. Nebenbefund: die Verwaltungsseite schluckt Ausnahmen ohne Protokoll

`TerminalsAdmin.razor:148` und `:173`:

```csharp
catch (Exception ex)
{
    reachable[terminalId] = false;
    Snackbar.Add(ex.Message, Severity.Error);
}
```

Die Meldung erscheint nur als Snackbar. Im Log steht **nichts** — deshalb sah die Fehlersuche nur eine
Terminal-Abfrage und danach Stille. Bitte `Logger.LogWarning(ex, …)` (Statusabfrage: erwarteter
Betriebsfall „Gerät weg“) bzw. `LogError` beim Laden der Liste.

Ebenso liefert `TerminalPaymentServiceBase.GetTerminalStatusAsync` eine `TerminalDeviceException` aus
`Address` ungefangen und unprotokolliert durch — im Gegensatz zu Start und Nachfragen, die beide
protokollieren.

## Umgehung (MiniStore)

Das Terminal erneut bearbeiten und **ausschliesslich die Knöpfe unten rechts im Dialog** benutzen
(„Weiter“/„Speichern“), nicht die des Steppers.

**Bestätigt:** danach steht `Route = <ClientKey>`, `ConfigurationJson = {"objectName":"Kartenterminal"}`,
die Statusabfrage erreicht den Agenten (`LastSeenUtc` gesetzt). Die leeren Auswahllisten beim Bearbeiten
waren Folge der leeren Zeile, kein eigener Fehler der Vorbelegung.
