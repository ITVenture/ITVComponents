using System;
using System.Linq;
using ITVComponents.WebCoreToolkit.Caching;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents
{
    /// <summary>
    /// Permission- and feature-gated content wrapper. Renders <see cref="Permitted"/> when the current user
    /// holds the <see cref="RequiredPermissions"/> AND the <see cref="RequiredFeatures"/> are active (each set
    /// OR-semantics by default, like the <c>HasPermission(a,b,c),HasFeature(x)</c> policy on the MVC side), and
    /// <see cref="NotPermitted"/> otherwise. The gate is re-evaluated on every write to the
    /// <see cref="EntityChangeTopics.Security"/> topic via the singleton <see cref="IEntityChangeSignal"/> —
    /// both permission and feature entities raise that topic — so a right revoked or a feature deactivated while
    /// the view is open takes effect without a fresh circuit (the permission-scope is re-resolved first; see
    /// <see cref="EntityChangeRefresher"/>). Wrap a whole view to gate access, or wrap individual edit/delete
    /// controls (leaving <see cref="NotPermitted"/> empty) so they disappear the moment the right is withdrawn.
    /// </summary>
    public class SecureView : ComponentBase
    {
        [Inject] private IServiceProvider Services { get; set; } = default!;

        /// <summary>
        /// The permissions to check, comma-separated (e.g. <c>"Roles.Write,Roles.Read,Sysadmin"</c>). By
        /// default the user needs ANY of them; set <see cref="RequireAllPermissions"/> to demand all. Empty/null grants
        /// access (no restriction).
        /// </summary>
        [Parameter] public string? RequiredPermissions { get; set; }

        /// <summary>
        /// When true, the user must hold ALL listed permissions; when false (default) ANY one suffices.
        /// </summary>
        [Parameter] public bool RequireAllPermissions { get; set; }

        /// <summary>
        /// The features to check, comma-separated (e.g. <c>"FeatureA,FeatureB,FeatureC"</c>). By
        /// default the user needs ANY of them; set <see cref="RequireAllFeatures"/> to demand all. Empty/null grants
        /// access (no restriction).
        /// </summary>
        [Parameter] public string? RequiredFeatures { get; set; }

        /// <summary>
        /// When true, the user must hold ALL listed features; when false (default) ANY one suffices.
        /// </summary>
        [Parameter] public bool RequireAllFeatures { get; set; }

        /// <summary>Content rendered when the user holds the required permissions.</summary>
        [Parameter] public RenderFragment? Permitted { get; set; }

        /// <summary>Content rendered when the user lacks the required permissions. Optional.</summary>
        [Parameter] public RenderFragment? NotPermitted { get; set; }

        /// <summary>
        /// Convenience alias for <see cref="Permitted"/>: a single un-named child fragment is treated as the
        /// permitted content (so <c>&lt;SecureView&gt;…&lt;/SecureView&gt;</c> works for the common case).
        /// </summary>
        [Parameter] public RenderFragment? ChildContent { get; set; }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            // Re-use the change-driven refresher: it subscribes to the signal, eagerly re-resolves the
            // permission-scope (re-pushes the CookiePermissionRepo snapshot) and re-renders our child on a
            // relevant write — so the inline permission check below always sees fresh rights.
            builder.OpenComponent<EntityChangeRefresher>(0);
            // SecureView only ever cares about security-relevant writes: both permission entities and feature
            // entities (Feature / TenantFeatureActivation) raise the Security topic, so a fixed watch suffices.
            builder.AddComponentParameter(1, nameof(EntityChangeRefresher.Watch), EntityChangeTopics.Security);
            builder.AddComponentParameter(2, nameof(EntityChangeRefresher.RefreshPermissionScope), true);
            builder.AddComponentParameter(3, nameof(EntityChangeRefresher.ChildContent), (RenderFragment)(b =>
            {
                if (IsPermitted() && IsFeatureEnabled())
                {
                    b.AddContent(4, Permitted ?? ChildContent);
                }
                else if (NotPermitted != null)
                {
                    b.AddContent(5, NotPermitted);
                }
            }));
            builder.CloseComponent();
        }

        private bool IsPermitted()
        {
            var perms = (RequiredPermissions ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (perms.Length == 0)
            {
                return true;
            }

            return RequireAllPermissions
                ? perms.All(p => Services.VerifyUserPermissions(new[] { p }))
                : Services.VerifyUserPermissions(perms);
        }
        
        private bool IsFeatureEnabled()
        {
            var features = (RequiredFeatures ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (features.Length == 0)
            {
                return true;
            }

            return RequireAllFeatures
                ? features.All(p => Services.VerifyActivatedFeatures(new[] { p }, out _))
                : Services.VerifyActivatedFeatures(features, out _);
        }
    }
}
