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
            var href = string.IsNullOrEmpty(segment) ? "/" : $"/{segment}/";

            builder.OpenElement(0, "base");
            builder.AddAttribute(1, "href", href);
            builder.CloseElement();
        }
    }
}
