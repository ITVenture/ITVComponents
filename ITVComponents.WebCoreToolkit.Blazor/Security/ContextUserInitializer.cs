using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace ITVComponents.WebCoreToolkit.Blazor.Security
{
    /// <summary>
    /// Invisible root component that seeds the circuit's <see cref="BlazorContextUserProvider"/> with the current
    /// principal. Place it once near the application root (e.g. in the main layout or App), so the synchronous
    /// <see cref="IContextUserProvider.User"/> getter is populated before the rest of the UI consumes it.
    /// Renders no markup.
    /// </summary>
    public sealed class ContextUserInitializer : ComponentBase
    {
        [Inject] private AuthenticationStateProvider AuthState { get; set; } = default!;

        [Inject] private IContextUserProvider ContextUser { get; set; } = default!;

        /// <inheritdoc/>
        protected override async Task OnInitializedAsync()
        {
            if (ContextUser is BlazorContextUserProvider provider)
            {
                var state = await AuthState.GetAuthenticationStateAsync();
                provider.Seed(state.User);
            }
        }
    }
}
