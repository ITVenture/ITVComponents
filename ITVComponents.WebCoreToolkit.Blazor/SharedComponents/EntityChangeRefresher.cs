using System;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Caching;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents
{
    /// <summary>
    /// Wrapper component that re-renders its <see cref="ChildContent"/> when a watched
    /// <see cref="EntityChangeScope"/> changes (driven by the singleton <see cref="IEntityChangeSignal"/>), so
    /// an already rendered navigation menu / page reflects permission-, role- or menu-changes without a fresh
    /// circuit. Optionally re-resolves the <see cref="IPermissionScope"/> first, so the re-render evaluates
    /// against freshly selected permissions. Becomes a transparent pass-through when no change-signal is
    /// registered (i.e. ActivationSettings.UseEntityTracker is off) — preserving the previous behaviour.
    /// Use inside an interactive render-mode; for the navigation menu wrap it with <c>Watch="Navigation"</c>.
    /// </summary>
    public class EntityChangeRefresher : ComponentBase, IDisposable
    {
        private IEntityChangeSignal? signal;
        private IPermissionScope? permissionScope;
        private bool disposed;

        [Inject] private IServiceProvider Services { get; set; } = default!;

        /// <summary>
        /// The topic to watch (e.g. <see cref="EntityChangeTopics.Navigation"/> or a custom topic registered
        /// via <see cref="EntitySignalOptions"/>). The toolkit maps security changes onto the Navigation topic,
        /// so a Navigation watcher also reacts to them (menu visibility derives from permissions/features).
        /// </summary>
        [Parameter] public string Watch { get; set; } = EntityChangeTopics.Navigation;

        /// <summary>
        /// When true (default), the permission-scope is re-resolved before re-rendering, so freshly granted or
        /// revoked rights take effect in the re-rendered content.
        /// </summary>
        [Parameter] public bool RefreshPermissionScope { get; set; } = true;

        /// <summary>Invoked on the renderer's sync-context after a relevant change, before the re-render.</summary>
        [Parameter] public EventCallback OnChanged { get; set; }

        /// <summary>The content that is re-rendered on a relevant change.</summary>
        [Parameter] public RenderFragment? ChildContent { get; set; }

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            // Both optional: signal only exists when the EntityWriteTracker is active.
            signal = Services.GetService<IEntityChangeSignal>();
            permissionScope = Services.GetService<IPermissionScope>();
            if (signal != null)
            {
                signal.Changed += OnSignalChanged;
            }
        }

        private void OnSignalChanged(string topic)
        {
            // A security write raises both Security and Navigation, so a Navigation watcher reacts to it too.
            if (disposed || !string.Equals(topic, Watch, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // The signal fires on a thread-pool thread (possibly for a different circuit) → marshal to ours.
            _ = InvokeAsync(async () =>
            {
                // The singleton signal can fire into a circuit that is being torn down between the raise and
                // this callback running; resolving/using its (now disposed) scope would throw. Guard + swallow
                // so a dead circuit can never break the dispatch for the live ones.
                if (disposed)
                {
                    return;
                }

                try
                {
                    if (RefreshPermissionScope)
                    {
                        permissionScope?.Refresh();
                        // Force the re-resolution NOW: Refresh() only marks the memoized scope dirty (the
                        // CookiePermissionRepo snapshot is rebuilt lazily on the next prefix access). Without
                        // this, a VerifyUserPermissions(...) inside OnChanged reads permissions through the
                        // still-stale snapshot (its GetUserPermissions path never touches the prefix), so the
                        // callback evaluates against last render's rights and lags one step behind. Touching
                        // the prefix re-resolves and re-pushes the snapshot before OnChanged runs.
                        _ = permissionScope?.PermissionPrefix;
                    }

                    await OnChanged.InvokeAsync();
                    if (!disposed)
                    {
                        StateHasChanged();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // circuit/scope torn down underneath us — nothing left to refresh.
                }
            });
        }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (ChildContent != null)
            {
                builder.AddContent(0, ChildContent);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            disposed = true;
            if (signal != null)
            {
                signal.Changed -= OnSignalChanged;
            }
        }
    }
}
