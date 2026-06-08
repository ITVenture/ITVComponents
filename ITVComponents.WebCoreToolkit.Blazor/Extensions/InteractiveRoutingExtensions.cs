using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Blazor.Extensions
{
    /// <summary>
    /// Host helper for mixing static-SSR identity pages with an otherwise interactive Blazor Web App.
    /// </summary>
    public static class InteractiveRoutingExtensions
    {
        /// <summary>
        /// True when the current endpoint may render interactively, i.e. it is NOT marked
        /// <see cref="ExcludeFromInteractiveRoutingAttribute"/>. The toolkit's identity/account pages opt out
        /// via that attribute and must render as <b>static SSR</b>.
        /// <para>
        /// Use this in <c>App.razor</c> to set the page render mode conditionally:
        /// <code>
        /// &lt;Routes @rendermode="RenderModeForPage" /&gt;
        /// &lt;HeadOutlet @rendermode="RenderModeForPage" /&gt;
        /// @code {
        ///     [CascadingParameter] private HttpContext HttpContext { get; set; } = default!;
        ///     private IComponentRenderMode? RenderModeForPage =&gt;
        ///         HttpContext.AcceptsInteractiveRouting() ? RenderMode.InteractiveServer : null;
        /// }
        /// </code>
        /// Returning <c>null</c> for identity pages keeps the <i>whole</i> page (including the shell/nav from
        /// the layout) static, so no interactive root component is placed over a static-SSR page. That avoids
        /// the <c>blazor.web.js</c> <c>updateRootComponents</c> SignalR-send error you otherwise get when
        /// enhanced navigation tries to reconcile interactive roots across the SSR boundary, and the
        /// nav-menu links on identity pages behave as plain (working) anchors.
        /// </para>
        /// <para>
        /// Caveat: do not force <c>@rendermode="InteractiveServer"</c> directly on the nav-menu/shell
        /// component — an explicit per-component render mode overrides this page-level decision and the
        /// interactive root reappears on identity pages. Drive interactivity from <c>App.razor</c>/<c>Routes</c>.
        /// </para>
        /// </summary>
        public static bool AcceptsInteractiveRouting(this HttpContext context)
            => context.GetEndpoint()?.Metadata.GetMetadata<ExcludeFromInteractiveRoutingAttribute>() is null;
    }
}
