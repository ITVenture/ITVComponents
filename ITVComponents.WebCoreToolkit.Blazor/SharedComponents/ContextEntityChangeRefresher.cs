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
    /// Per-DbContext counterpart of <see cref="EntityChangeRefresher"/>: watches a topic on
    /// <see cref="IEntityChangeSignal{TContext}"/> for a specific context. Use when the relevant entities live
    /// in a non-security context: <c>&lt;ContextEntityChangeRefresher TContext="MyContext" Watch="MyTopic"&gt;…&lt;/&gt;</c>.
    /// A distinct name is required because Razor resolves component tags by simple name and would otherwise
    /// conflate the generic with the non-generic <see cref="EntityChangeRefresher"/> (RZ10009).
    /// </summary>
    /// <typeparam name="TContext">the DbContext whose changes to watch</typeparam>
    public class ContextEntityChangeRefresher<TContext> : ComponentBase, IDisposable
    {
        private IEntityChangeSignal<TContext>? signal;
        private IPermissionScope? permissionScope;
        private bool disposed;

        [Inject] private IServiceProvider Services { get; set; } = default!;

        /// <summary>The topic to watch (configured via <see cref="EntitySignalOptions{TContext}"/>).</summary>
        [Parameter] public string Watch { get; set; } = EntityChangeTopics.Navigation;

        /// <summary>When true, the permission-scope is re-resolved before re-rendering.</summary>
        [Parameter] public bool RefreshPermissionScope { get; set; } = true;

        /// <summary>Invoked on the renderer's sync-context after a relevant change, before the re-render.</summary>
        [Parameter] public EventCallback OnChanged { get; set; }

        /// <summary>The content that is re-rendered on a relevant change.</summary>
        [Parameter] public RenderFragment? ChildContent { get; set; }

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            signal = Services.GetService<IEntityChangeSignal<TContext>>();
            permissionScope = Services.GetService<IPermissionScope>();
            if (signal != null)
            {
                signal.Changed += OnSignalChanged;
            }
        }

        private void OnSignalChanged(string topic)
        {
            if (disposed || !string.Equals(topic, Watch, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // The signal fires on a thread-pool thread (possibly for another circuit) → marshal to ours.
            _ = InvokeAsync(async () =>
            {
                // The singleton signal can fire into a circuit that is being torn down between the raise and this
                // callback running; resolving/using its (now disposed) scope would throw. Guard + swallow so a dead
                // circuit can never break the dispatch for the live ones.
                if (disposed)
                {
                    return;
                }

                try
                {
                    if (RefreshPermissionScope)
                    {
                        permissionScope?.Refresh();
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
