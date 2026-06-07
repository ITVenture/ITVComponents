using System;
using Microsoft.AspNetCore.Components;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers
{
    /// <summary>
    /// DI marker that names the strategy-specific Blazor component which renders the "Users" tab inside the
    /// <c>TenantDetailDialog</c>. The base TenantSecurityViews library is strategy-neutral, but the user object
    /// itself differs per strategy (ASP.NET Core Identity flat/tree vs. TenantSecurityContext); each UserView
    /// library registers its own grid via <c>AddTenantUsersGrid&lt;TComponent&gt;()</c>. The dialog uses
    /// <see cref="Microsoft.AspNetCore.Components.DynamicComponent"/> with the registered type and passes
    /// <c>TenantId</c> as a parameter. If no descriptor is registered the tab is omitted.
    /// </summary>
    public sealed class TenantUsersGridDescriptor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TenantUsersGridDescriptor"/> class.
        /// </summary>
        /// <param name="gridComponentType">a <see cref="IComponent"/> type that accepts an <c>int TenantId</c>
        /// parameter and renders the strategy-specific tenant-user list (with role-assignment sub-grid)</param>
        public TenantUsersGridDescriptor(Type gridComponentType)
        {
            if (gridComponentType is null)
            {
                throw new ArgumentNullException(nameof(gridComponentType));
            }

            if (!typeof(IComponent).IsAssignableFrom(gridComponentType))
            {
                throw new ArgumentException(
                    $"{gridComponentType.FullName} must implement {nameof(IComponent)}.",
                    nameof(gridComponentType));
            }

            GridComponentType = gridComponentType;
        }

        /// <summary>
        /// Gets the strategy-specific Blazor component type that renders the tenant users grid.
        /// </summary>
        public Type GridComponentType { get; }
    }
}
