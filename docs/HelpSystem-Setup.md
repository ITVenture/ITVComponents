# Help-System — Host-Wiring

Globales (systemweites) Hilfesystem: hierarchische Hilfethemen (Container / Content-Pages) mit
pro-Sprache Markdown-Inhalten, benannte Medien-Ressourcen, ein anonym erreichbarer Viewer und
Admin-Seiten (Permission- + `ITVAdminViews`-gegated).

## Bausteine

- `ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem` — EF-Entities (global, kein Tenant-FK, kein Flat/Tree):
  `HelpTopic`, `HelpTopicContent`, `HelpResource`, `HelpResourceFile`, `HelpResourceBlob` (Referenz-Store) +
  `IHelpSystemContext`, `ConfigureHelpSystemModel()`, `HelpSystemOptions`, `IHelpResourceStore` (+ Referenzimpl
  `HelpResourceBlobStore<TContext>`), `HelpRoutes`.
- `…Blazor.MudBlazor.AdminViews` (Ordner `HelpViews/`) — Admin-Seiten, Viewer, Handler, Markdig-Renderer,
  anonymer Ressourcen-Endpoint. Wird über den **bestehenden AdminViews-WebPart** aktiviert.

## 1. DbContext

Der Host-Context implementiert zusätzlich `IHelpSystemContext` (die 5 DbSets) und ruft in `OnModelCreating`:

```csharp
public class ApplicationDbContext : AspNet…SecurityContext<…>, …, IHelpSystemContext
{
    public DbSet<HelpTopic> HelpTopics { get; set; }
    public DbSet<HelpTopicContent> HelpTopicContents { get; set; }
    public DbSet<HelpResource> HelpResources { get; set; }
    public DbSet<HelpResourceFile> HelpResourceFiles { get; set; }
    public DbSet<HelpResourceBlob> HelpResourceBlobs { get; set; }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.ConfigureHelpSystemModel();
    }
}
```

→ EF-Migration `AddHelpSystem` (5 neue Tabellen). Keine Query-Filter (globale Daten).

## 2. WebPart-Konfiguration (AdminViews)

Der AdminViews-WebPart hat einen neuen Config-Zweig `Help` (`HelpViewOptions`) — nur ein Enable-Flag:

```json
{
  "Help": {
    "ConfigureContext": true
  }
}
```

Der Kontext-Typ wird **aus `SecurityContextOptions.ContextType` übernommen** (der Help-Kontext ist immer auch
ein `ICoreSystemContext`), muss also hier nicht wiederholt werden. Das verdrahtet die Handler
(`AddMudBlazorHelpViews<ApplicationDbContext>`) **und** mappt den anonymen Ressourcen-Endpoint
(`/help/res/{name}`). Implementiert der konfigurierte Kontext kein `IHelpSystemContext`, wird Help
still übersprungen. Ohne WebPart-Nutzung geht auch direktes DI:
`services.AddMudBlazorHelpViews<ApplicationDbContext>()` + `app.MapHelpResourceEndpoints<ApplicationDbContext>()`.

## 3. HelpSystemOptions (Section `HelpSystem`)

```json
{
  "HelpSystem": {
    "Enabled": true,
    "AnonymousResourceAccess": true,
    "MaxUploadBytes": 26214400,
    "AllowedContentTypePrefixes": ["image/", "video/"]
  }
}
```

## 4. Ressourcen-Storage

Standardmäßig läuft Up-/Download über die mitgelieferte EF-Blob-Referenz
(`HelpResourceBlobStore<TContext>`, Tabelle `HelpResourceBlob`) — **kein Plugin nötig**. Grund: der Plugin-
FileHandler-Pfad (`IFileServiceHandler` → `WebPluginHelper`) ist security-gegated und kann für **anonyme**
Requests keinen Handler laden; deshalb ist der Store ein schlanker DI-Service, den Upload (Admin) und
Download (anonymer Viewer) gemeinsam nutzen.

Eigener Storage (Azure/DMS): eine eigene `IHelpResourceStore`-Implementierung **vor**
`AddMudBlazorHelpViews` registrieren (die Registrierung nutzt `TryAddScoped`, überschreibt also nicht).

## 5. Permissions seeden

`Help.Admin.Topics.View` / `.Write`, `Help.Admin.Resources.View` / `.Write` (`.Write` ⊇ `.View`).
Admin-Seiten brauchen zusätzlich das Feature **`ITVAdminViews`**. Der Viewer (`/help`) ist anonym.

## 6. Host-Page-Skripte (Monaco)

Der Markdown-Editor nutzt BlazorMonaco; die AdminViews-WebPart registriert die Client-Skripte bereits
(`AddToolkitClientScript`) — sofern die Host-Page `<ITVentureReferences />` rendert, ist nichts weiter nötig.

## Nutzung

- Admin: `/Help/Admin/Topics` (Baum: Container/Content-Pages, pro Sprache Titel + Markdown mit Vorschau),
  `/Help/Admin/Resources` (Medien, pro Sprache Datei).
- Content-Embeds: im Markdown `resource:{name}` bzw. `/resource/{name}` → beim Rendern automatisch auf
  `/help/res/{name}?c={culture}` umgeschrieben (Range-fähig für Video).
- Modul-Links: im Markdown `[Titel](module:/MasterData/Customers)`. Beim Rendern:
  - **angemeldet** → echter Link, mit aktuellem Tenant präfixiert (`/TENANT1234/MasterData/Customers` im
    Path-Segment-Modus bzw. `/MasterData/Customers` im Cookie-Modus, via `IUrlFormat`/`[SlashPermissionScope]`);
  - **anonym** → nur der Titel als `<span class="itv-help-module-link">Titel</span>` (kein Link).
  (CSS-Klasse `itv-help-module-link` optional im Host stylen.)
- Viewer: `/help` und `/help/{slug}` (anonym), nur **veröffentlichte** Themen.

## Veröffentlichen vs. auflisten (`IsPublished` / `ShowInMenu`)

Zwei verschiedene Dinge, und die Unterscheidung ist der Zweck des zweiten Flags:

- **`IsPublished`** — ob es das Thema für die Öffentlichkeit überhaupt gibt. Aus = Entwurf, nur im Admin sichtbar.
- **`ShowInMenu`** (Default `true`) — ob es im Navigationsbaum des Viewers **aufgelistet** wird. Aus = das Thema
  bleibt unter `/help/{slug}` abrufbar, erscheint aber weder in der Navigation noch im Teilbaum des
  Kontext-Popups — **zusammen mit allem, was unter ihm hängt**.

Damit lassen sich verlinkte Dokumente (AGB, Datenschutzerklärung, Widerrufsbelehrung) im Hilfesystem pflegen,
ohne dass sie mitten in der Produkthilfe stehen: einen Container *Dokumente* mit `ShowInMenu = false` anlegen und
die Dokumente darunter hängen. Verlinkt werden sie dort, wo sie hingehören — etwa aus den Zustimmungs-Schaltern
des Onboardings (GlobalSetting `Consent`, Feld `HelpSlug`; siehe `Migration-Future_10-MLM.md` §21).

Dass die Themen **anonym** lesbar sind, ist dafür die Voraussetzung: die Zustimmung fällt beim Anlegen des
Kontos, also bevor jemand angemeldet ist. Im Admin-Baum tragen nicht gelistete Themen den Chip *not in menu* —
sonst sähe ein fehlendes Thema im Viewer nach einem Defekt aus.

## Sprach-Fallback

`Culture` je Content/Datei ist ein BCP-47-Tag (`de`, `de-CH`, …) oder `DEFAULT`. Aufgelöst most-specific-first:
`de-CH` → `de` → `DEFAULT`.
