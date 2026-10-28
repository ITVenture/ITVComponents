# ISSUE: Ein Mandant aus der Admin-Maske bekommt keinen eigenen Schlüssel (`TenantPassword`)

**Status:** erledigt (Leitfaden §95, ab PRE265) · **Datum:** 2026-10-28 · **Version:** 5.0.0-PRE264 · **Quelle:** MiniStore, Prüfung vor dem
ersten echten Laden

## Der Fall

`CipherExtensions.GetEncryptionKey` nimmt `Tenants.TenantPassword` als Schlüssel des Mandanten und fällt ohne
ihn auf den anwendungsweiten Schlüssel aus `PasswordSecurity.InitializeAes` zurück. Ein Mandant ohne eigenen
Schlüssel verschlüsselt seine anonymen Freigabe-Tickets und seine Plugin-Konstanten damit **mit demselben
Schlüssel wie alle anderen** solchen Mandanten. Seit PRE231 verhindert `AssetTicket.TenantName` zwar, dass
ein Ticket unter einem anderen Mandanten angenommen wird. Der Schlüssel bleibt trotzdem geteilt.

Wo ein Mandant entsteht, wird der Schlüssel heute unterschiedlich behandelt:

| Weg | Schlüssel |
|---|---|
| `OnboardingHandler` / `HierarchyOnboardingHandler` (MudBlazor), auch über eine Einladung zur Filiale | **immer gesetzt** (`AesEncryptor.CreateKey()`) |
| Telerik `CreateTenant.cshtml.cs`, `RegistrationController` | immer gesetzt |
| Telerik `TenantControllerStruct`/`TenantControllerClass` (Admin-Maske) | nur mit `SecurityViewsOptions.UseExplicitTenantPasswords` |
| **MudBlazor `TenantAdminHandler.CreateTenantAsync`** (`/Security/Tenants`) | **nie**, und es gibt keinen Schalter |

In MiniStore ist das Fehlerbild geprüft: der über das Onboarding angelegte Laden hat einen Schlüssel, ADM
keinen (gewollt). Ein Sysadmin, der einen Laden oder eine Filiale direkt unter `/Security/Tenants` anlegt,
bekäme dagegen einen Mandanten ohne Schlüssel. Das fällt nirgends auf: alles funktioniert, nur eben mit dem
gemeinsamen Schlüssel.

## Betroffene Stellen

| Stelle | Heute |
|---|---|
| `Blazor.MudBlazor.AdminViews/TenantSecurityViews/Handlers/Impl/TenantAdminHandler.cs`, `CreateTenantAsync` (≈ Z. 162) | setzt Name, Anzeigename, Zeitzone, Typ oder ruft `UpdateTenant`, nie `TenantPassword` |
| `AspNetCoreTreeTenantSecurityUserView/Extensions/DependencyInjectionExtensions.cs` (≈ Z. 102) | belegt `UpdateTenant` selbst. Ein Host, der den Schlüssel dort nachrüsten wollte, müsste diesen Delegaten ersetzen, und derselbe Delegat läuft auch beim **Ändern** |

Ein Nachrüsten im Host über `UpdateTenant` ist deshalb keine gute Lösung: beim Ändern darf ein bestehender
Schlüssel nie überschrieben werden (jedes ausgegebene Ticket und jede verschlüsselte Konstante würde
unlesbar), und ob ein leerer Schlüssel beim Ändern nachgetragen werden soll, ist eine bewusste Entscheidung
(siehe ADM).

## Vorschlag

1. `CreateTenantAsync` setzt beim **Anlegen** `TenantPassword = Convert.ToBase64String(AesEncryptor.CreateKey())`,
   wie die Onboarding-Handler. Am einfachsten nach `tenantAssign`, nur wenn er danach noch leer ist.
2. Falls es Hosts gibt, die das bewusst nicht wollen: ein Schalter wie `UseExplicitTenantPasswords` bei
   Telerik, dann aber mit Vorgabe `true`. Ein geteilter Schlüssel als stille Vorgabe ist die unsichere
   Richtung.
3. `UpdateTenantAsync` fasst den Schlüssel weiterhin nicht an.

Optional: `ValidateTrustEntries` oder ein vergleichbarer Start-Report könnte Mandanten ohne Schlüssel (ausser
dem Wurzelmandanten) als Warnung melden. Dann fällt ein Altbestand auf.


---

## Erledigt (2026-10-28, ab 5.0.0-PRE265, Leitfaden §95)

- **Vorschlag 1 umgesetzt:** `TenantAdminHandler.CreateTenantAsync` setzt beim Anlegen `TenantPassword`
  (`AesEncryptor.CreateKey()`), nach `UpdateTenant` und nur, wenn er dann noch leer ist.
- **Vorschlag 2 umgesetzt:** Schalter `TenantOptions<TTenant>.GenerateTenantPassword`, Vorgabe `true`.
- **Vorschlag 3:** `UpdateTenantAsync` fasst den Schlüssel weiterhin nicht an.
- **Telerik:** `SecurityViewsOptions.UseExplicitTenantPasswords` steht jetzt ebenfalls auf `true`.
- **Folgefehler mit behoben:** Die Blazor-Plugin-Maske hat `encrypt:`-Konstanten immer mit dem anwendungsweiten
  Schlüssel verschlüsselt, zur Laufzeit wird bei einem Mandanten mit Schlüssel aber mit dessen Schlüssel
  entschlüsselt. Jetzt nimmt die Maske den Mandantenschlüssel (wie Telerik). Für MiniStore: verschlüsselte
  Konstanten des Ladens, die über die Blazor-Plugin-Maske gespeichert wurden, einmal neu mit `encrypt:` eingeben.
- **Nicht umgesetzt:** Start-Report für Mandanten ohne Schlüssel (optional). Altbestand wird nicht nachgetragen.
