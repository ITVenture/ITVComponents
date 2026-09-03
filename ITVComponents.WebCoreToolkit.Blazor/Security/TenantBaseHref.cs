using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Blazor.Security
{
    /// <summary>
    /// Emits the <c>&lt;base href&gt;</c> tag for Blazor in <see cref="TenantSource.PathSegment"/> mode,
    /// using the tenant segment that <see cref="TenantPathPrefixMiddleware"/> validated and stashed in
    /// <see cref="HttpContext.Items"/>. Place once in <c>App.razor</c> inside <c>&lt;head&gt;</c> (replaces
    /// the static <c>&lt;base href="/"/&gt;</c>). If no segment is present (auth/excluded paths, or
    /// Query-mode hosts) the tag falls back to <c>&lt;base href="/"/&gt;</c>, so the same component is
    /// safe to keep in App.razor regardless of which mode the host is configured for.
    /// </summary>
    public sealed class TenantBaseHref : ComponentBase
    {
        [Inject] private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;

        /// <inheritdoc/>
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            var segment = HttpContextAccessor.HttpContext?.Items[TenantPathPrefixMiddleware.TenantSegmentItemKey] as string;
            // Der Asset-Abschnitt gehoert mit in den base-href, und zwar VOR den Mandanten: nur so erben
            // relative Verweise und der NavigationManager den Asset-Kontext, statt beim ersten Klick
            // herauszufallen. Er steht in der URL ohnehin an dieser Stelle.
            var assetSegment = HttpContextAccessor.HttpContext?.Items[Global.SharedAssetSegmentItemKey] as string;
            // Die Kultur fuehrt die URL an, noch vor dem Asset - und aus demselben Grund gehoert sie in den
            // base-href: sonst faellt die erste relative Navigation aus der Sprache heraus. Der Abschnitt
            // steht hier so, wie er in der URL stand; eine andere Schreibweise waere kein Praefix der
            // aktuellen Adresse mehr und der Circuit koennte sich selbst nicht mehr einordnen.
            var culturePrefix = HttpContextAccessor.HttpContext?.Items[Global.CulturePathPrefixItemKey] as string ?? string.Empty;
            var prefix = culturePrefix + SharedAssetPath.BuildPrefix(assetSegment, segment);
            var href = prefix.Length == 0 ? "/" : $"{prefix}/";

            builder.OpenElement(0, "base");
            builder.AddAttribute(1, "href", href);
            builder.CloseElement();
        }
    }
}
