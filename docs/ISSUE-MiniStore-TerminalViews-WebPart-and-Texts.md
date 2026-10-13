# ISSUE: Terminal-Verwaltung — kein WebPart-Schalter, keine Texte

**Status:** UMGESETZT (Toolkit, nach PRE255), siehe Leitfaden §74 · **Datum:** 2026-10-13 · **Quelle:** MiniStore auf `5.0.0-PRE255`

## Der Fall

Ein Host mit `BillingViews` + `ActivatePayments` öffnet `/Account/Manage/Payments/Terminals` (Leitfaden §70).
Die Seite ist geroutet und stürzt beim Rendern ab (sinngemäss, aus dem SystemLog):

```
System.InvalidOperationException: Cannot provide a value for property 'Handler' on type
'…BillingViews.Blazor.Components.Payments.TerminalsAdmin'. There is no registered service of type
'…ITerminalsHandler'.
```

Nachdem der Host die Ansichten selbst registriert hat, steht die Seite. Sie zeigt aber überall die Schlüssel
statt Texten (`Terminals_Title`, `Terminals_Add`, …).

## Betroffene Stellen

| Stelle | Befund |
|---|---|
| `Blazor.MudBlazor.BillingViews/WebPartInit.cs` `RegisterServices` | ruft `AddMudBlazorBillingViews` und (mit `PaymentViews:ActivatePaymentViews`) `AddMudBlazorPaymentViews`, aber **nie** `AddMudBlazorTerminalViews<TContext, TClientApp>`. Die Route liefert das Assembly trotzdem aus, weil es schon für die anderen Ansichten registriert ist. Die Seite ist also erreichbar, der Handler aber nicht registriert. |
| `…BillingViews/Extensions/DependencyInjectionExtensions.cs` `AddMudBlazorTerminalViews` | Der Doc-Kommentar zu `TClientApp` sagt: *„Das WebPart füllt den Parameter von selbst"*. Das stimmt heute nicht, kein WebPart ruft die Methode. |
| `…BillingViews/Resources/PaymentMessages*.resx` | Keine der 34 `Terminals_*`-Schlüssel ist vorhanden, auch nicht in der neutralen Datei. Benutzt werden sie in `TerminalsAdmin.razor` und `TerminalWizardDialog.razor`. |
| Leitfaden §70.5 | Nennt „Sprachressourcen `Terminals_*`" als Host-Aufgabe. Der Host kommt aber an `IStringLocalizer<PaymentMessages>` nicht heran: der `ResourceManager` hängt am Toolkit-Assembly. Ein Host könnte die Texte nur über eine eigene `IStringLocalizerFactory` als Dekorateur nachreichen. |

Fehlende Schlüssel:

```
Terminals_Add Terminals_Cancel Terminals_Check Terminals_Col_Name Terminals_Col_Provider Terminals_Col_Route
Terminals_Col_State Terminals_Col_TerminalId Terminals_DeviceUnreachable Terminals_Disable Terminals_Disabled
Terminals_Edit Terminals_Enable Terminals_Field_DisplayName Terminals_Field_DisplayNameHint Terminals_Field_Provider
Terminals_Field_ProviderTerminalId Terminals_Field_ProviderTerminalIdHint Terminals_Intro Terminals_Next
Terminals_NoDeleteHint Terminals_NoDeviceSettings Terminals_NoServerSettings Terminals_None Terminals_Offline
Terminals_Online Terminals_RequiredMissing Terminals_Save Terminals_Saved Terminals_Step_Device Terminals_Step_Kind
Terminals_Step_Service Terminals_Title Terminals_Unchecked
```

## Vorschlag

1. **Schalter im WebPart**, analog zu `PaymentViews`: z.B. `PaymentViews:ActivateTerminalViews`. Er ruft
   `AddMudBlazorTerminalViews<TContext, TClientApp>`. `TClientApp` wird dabei wie im Doc-Kommentar beschrieben
   aus dem Sicherheitskontext gebunden. Für die Dienstschicht entsprechend Schalter im
   `Billing.Stripe`-WebPart (`ActivateStripeTerminals`) bzw. im Billing-/AgentServices-Teil (`ActivateAgentTerminals`).
2. **Die `Terminals_*`-Texte in `PaymentMessages.resx` (+ de/fr/it) mitliefern**, wie alle übrigen Texte
   derselben Klasse. §70.5 entsprechend korrigieren.
3. Alternativ: `RegisterServices` meldet zumindest eine Warnung, wenn Payment-Views aktiv sind, aber kein
   `ITerminalsHandler` registriert ist. Heute merkt man es erst beim Öffnen der Seite.

## Umgehung (in MiniStore umgesetzt)

- `Program.cs`: `AddMudBlazorTerminalViews<ApplicationDbContext, CoreIdentityTree.Model.ClientApp>()`.
- `AddBillingAndPayments`: `AddStripeTerminals<ApplicationDbContext>()` und `AddAgentTerminals<ApplicationDbContext>()`.
- Für die Texte gibt es keine Umgehung. Die Seite funktioniert, zeigt aber die Schlüsselnamen.

## Umsetzung (Toolkit-Seite, 2026-10-13)

Alle drei Punkte aus dem Vorschlag sind umgesetzt, Punkt 3 nicht als Alternative, sondern zusätzlich:

1. **Schalter:** `PaymentViews:ActivateTerminalViews` (BillingViews), `ActivateStripeTerminals` (Billing.Stripe,
   nur mit `ActivatePayments`), `ActivateAgentTerminals` + `TerminalsContextType` (EntityFramework.Billing).
   Der Agenten-Weg liegt im provider-neutralen Billing-WebPart und nicht im AgentServices-WebPart, weil
   AgentServices Billing nicht kennt.
2. **Texte:** die 34 `Terminals_*` stehen in `PaymentMessages.resx` sowie de/fr/it. §70.5 ist korrigiert.
3. **Startprüfung:** ein `IHostedService` meldet eine Warnung, wenn die Abo- oder Zahlungs-Ansichten
   registriert sind, aber kein `ITerminalsHandler`. Er prüft am fertigen Container, die Umgehung in
   `Program.cs` zählt also mit.

**Für MiniStore:** entweder die Umgehung behalten oder auf die Schalter umstellen, nicht beides, sonst
stehen die Wege doppelt im Assistenten.
