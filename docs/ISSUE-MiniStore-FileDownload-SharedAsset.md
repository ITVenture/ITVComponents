# Issue: `<FileDownload />` reicht den Asset-Schlüssel nicht mit — unter einer Freigabe scheitert jeder Download

**Status:** ERLEDIGT — umgesetzt im Toolkit (siehe „Auflösung" am Ende)
**Datum:** 2026-09-15
**Quelle:** MiniStore-Session (Konsument). Der Self-Checkout-Kunde soll seinen Bon als PDF herunterladen
können. Er ist dabei **anonym unter einem Ad-hoc-Ticket** unterwegs.
**Toolkit-Stand:** `5.0.0-PRE237`. Alle Aussagen unten sind am Quelltext verifiziert.

## Der Fall

`ProcessFileDownload` kennt den Freigabe-Weg — es gibt `hasAsset` und `assetKey`, und mit ihnen baut es
über `IImpersonationControl.AsAssetAccessor` den Asset-Zugriff auf, bevor es die Rechte prüft:

```csharp
// ITVComponents.WebCoreToolkit.ServiceShared/Service/Impl/DefaultFileServiceHandler.cs:199
public async Task<FileOperationResult> ProcessFileDownload(string downloadModule, string reason,
    string fileIdentifier, ClaimsPrincipal principal, string defaultDownloadName = null,
    string defaultContentType = null, bool defaultFileDownload = true, bool withAuthorization = true,
    bool hasAsset = false, string assetKey = null)
```

**Die Blazor-Komponente benutzt ihn nicht.** Sie ruft ohne beides:

```razor
@* ITVComponents.WebCoreToolkit.Blazor.MudBlazor/SharedComponents/FileDownload.razor:62 *@
var result = await FileService.ProcessFileDownload(DownloadModule, DownloadReason, FileIdentifier,
    authState.User, defaultDownloadName: "download.bin", defaultContentType: "application/octet-stream");
```

Damit läuft die Prüfung in `ProcessFileDownload`

```csharp
var requiredPermissions = fileHandler.PermissionsForReason(reason);
if (!withAuthorization || (requiredPermissions != null && requiredPermissions.Length != 0 &&
                           services.VerifyUserPermissions(requiredPermissions)))
```

gegen einen **Benutzer, den es nicht gibt**: unter einem anonymen Ticket ist `authState.User` nicht
angemeldet, `VerifyUserPermissions` schlägt fehl, und der Download endet mit „Permission denied for
requested file." — obwohl das Ticket des Besuchers die verlangten Rechte sehr wohl gewährt.

Das ist derselbe Unterschied, den eine Seite unter einer Freigabe ohnehin kennt und den etwa
`MobileCheckout` in MiniStore ausdrücklich behandelt: *mit* Ticket ist das Ticket die Autorität, *ohne*
Ticket das Recht. Der Download-Knopf ist die einzige Stelle, an der diese Unterscheidung fehlt.

## Betroffene Stellen

| Datei | Was dort steht |
|---|---|
| `ITVComponents.WebCoreToolkit.Blazor.MudBlazor/SharedComponents/FileDownload.razor:62` | Der Aufruf ohne `hasAsset`/`assetKey` |
| `ITVComponents.WebCoreToolkit.ServiceShared/Service/Impl/DefaultFileServiceHandler.cs:199` | Kann beides — wird nur nicht damit gerufen |
| `ITVComponents.WebCoreToolkit/Security/SharedAssets/ISharedAssetContext.cs` | `HasAsset` und `AssetKey`, genau die zwei Angaben, die fehlen |

Der Upload-Pfad hat dasselbe Muster; wir haben ihn nicht geprüft, weil MiniStore ihn nicht benutzt.

## Vorschlag

`FileDownload.razor` injiziert `ISharedAssetContext` und reicht durch, was da ist:

```razor
@inject ISharedAssetContext AssetContext

@code {
    var hasAsset = AssetContext.HasAsset;
    var result = await FileService.ProcessFileDownload(
        DownloadModule, DownloadReason, FileIdentifier, authState.User,
        defaultDownloadName: "download.bin", defaultContentType: "application/octet-stream",
        hasAsset: hasAsset,
        assetKey: hasAsset ? AssetContext.AssetKey : null);
}
```

Für einen angemeldeten Benutzer ändert sich damit nichts (`HasAsset` ist dort `false`, der Aufruf bleibt
der heutige). Unter einer Freigabe greift der Weg, den `ProcessFileDownload` bereits vorsieht.

**Ohne neuen Parameter an der Komponente**, bewusst: ob eine Seite unter einer Freigabe läuft, weiss der
Kontext — der Aufrufer müsste es sonst wissen und mitgeben, und genau das ist die Sorte Angabe, die
irgendwo vergessen wird.

Sinnvoll wäre zusätzlich ein Protokolleintrag in `ProcessFileDownload`, wenn die Rechteprüfung bei einem
**nicht angemeldeten** Principal ohne `assetKey` fehlschlägt — das ist die Konstellation, in der die
Meldung „Permission denied" in die falsche Richtung weist.

## Umgehung bis dahin

MiniStore hat die Komponente kopiert und um genau diese zwei Angaben ergänzt
(`MiniStore.Web/Components/Shared/SharedAssetFileDownload.razor`). Das JS-Modul stammt weiterhin aus dem
Toolkit-Paket (`_content/ITVComponents.WebCoreToolkit.Blazor.MudBlazor/file-transfer.js`) — es gibt also
keinen zweiten Download-Weg, nur eine zusätzliche Angabe am selben.

Die Kopie verschwindet ersatzlos, sobald `<FileDownload />` den Asset-Kontext selbst berücksichtigt.

---

## Auflösung (Toolkit)

Umgesetzt — mit **einer Abweichung vom Vorschlag**, und die ist wichtig.

### `@inject ISharedAssetContext` geht nicht

`ISharedAssetContext` ist **nicht unbedingt registriert**. Die Registrierung hängt an
`UseSharedAssetPathContext()` (`DependencyExtensions.cs`, `TryAddScoped`) bzw. implizit an
`UseAssetDrivenClaimsTransformation` / `UseSharedAssets` in der WebPart-Konfiguration. Ein Host, der gar
keine Freigaben benutzt, hat den Dienst nicht — und `@inject` in einer **gemeinsamen** Komponente löst über
`GetRequiredService` auf. Der Vorschlag hätte dort jeden Download beim Rendern mit einer
`InvalidOperationException` gesprengt.

Deshalb ein kleiner Helfer, der den Kontext **optional** auflöst:

```csharp
// ITVComponents.WebCoreToolkit.Blazor.MudBlazor/SharedComponents/SharedAssetCallContext.cs
internal static (bool HasAsset, string? AssetKey) Resolve(IServiceProvider? services)
```

Die Komponenten injizieren `IServiceProvider` und fragen darüber. Ohne Registrierung und ohne laufende
Freigabe kommt `(false, null)` zurück — der Aufruf ist dann bitgleich der heutige.

### Was geändert wurde

| Datei | Änderung |
|---|---|
| `…Blazor.MudBlazor/SharedComponents/SharedAssetCallContext.cs` | **neu** — optionale Auflösung des Asset-Kontexts |
| `…Blazor.MudBlazor/SharedComponents/FileDownload.razor` | reicht `hasAsset`/`assetKey` durch; die Fehler-Protokollzeile führt jetzt `authenticated` und `asset` mit |
| `…Blazor.MudBlazor/SharedComponents/FileUpload.razor` | **dasselbe Loch, mitgeschlossen** — die Vermutung im Issue stimmte |
| `…ServiceShared/Service/Impl/DefaultFileServiceHandler.cs` | `LogDeniedTransfer(…)` für beide Richtungen |

Der Upload-Pfad ist mitgegangen, obwohl MiniStore ihn nicht benutzt: `ProcessFileUpload` hat dieselben zwei
Parameter, und ein halb geschlossenes Loch ist schlimmer als ein offenes — es fällt erst dem Nächsten auf.

### Die Protokollierung

`ProcessFileDownload`/`ProcessFileUpload` gaben `UnAuthorized()`/`Forbid()` bisher **ohne jede Spur**
zurück. Jetzt schreibt jede Abweisung eine Zeile, und die Konstellation aus dem Issue — nicht angemeldet
**und** kein `assetKey` — bekommt eine eigene Meldung, die auf den fehlenden Freigabe-Schlüssel zeigt statt
auf die Rechte. Beide Zeilen führen die verlangten Rechte mit; steht dort `<none configured>`, ist der
Handler die Ursache und nicht der Aufrufer (bei `withAuthorization: true` weist eine leere Rechteliste
grundsätzlich ab — auch das war bisher unsichtbar).

**Für MiniStore:** `SharedAssetFileDownload.razor` kann ersatzlos weg, `<FileDownload />` tut es jetzt selbst.
