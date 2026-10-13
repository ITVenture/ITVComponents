using System;
using Microsoft.AspNetCore.Components;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers
{
    /// <summary>
    /// Ein zusätzlicher Reiter in der Detailzeile einer ClientApp — für Pakete, die AdminViews nicht kennt
    /// (etwa die Agent-Dienste: welche Geräte diese Kasse in welcher Rolle benutzt).
    /// </summary>
    /// <remarks>
    /// Mehrere dürfen registriert sein; jeder wird als eigener Reiter nach den eingebauten gezeigt. Die
    /// Komponente bekommt den Parameter <c>int ClientAppId</c> und prüft ihre Rechte selbst.
    /// </remarks>
    public sealed class ClientAppDetailTabDescriptor
    {
        /// <summary>Initializes a new instance of the <see cref="ClientAppDetailTabDescriptor"/> class.</summary>
        /// <param name="title">die Beschriftung des Reiters, bzw. ihr Schlüssel in <paramref name="titleResource"/></param>
        /// <param name="componentType">eine Komponente mit dem Parameter <c>int ClientAppId</c></param>
        /// <param name="titleResource">die Ressource, aus der <paramref name="title"/> übersetzt wird; leer = wörtlich</param>
        public ClientAppDetailTabDescriptor(string title, Type componentType, Type? titleResource = null)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentNullException(nameof(title));
            }

            if (componentType is null || !typeof(IComponent).IsAssignableFrom(componentType))
            {
                throw new ArgumentException($"{componentType?.FullName} must implement {nameof(IComponent)}.",
                    nameof(componentType));
            }

            Title = title;
            ComponentType = componentType;
            TitleResource = titleResource;
        }

        /// <summary>Die Beschriftung oder ihr Ressourcen-Schlüssel.</summary>
        public string Title { get; }

        /// <summary>Die Komponente.</summary>
        public Type ComponentType { get; }

        /// <summary>Die Ressource für die Beschriftung; leer = <see cref="Title"/> wörtlich.</summary>
        public Type? TitleResource { get; }
    }
}
