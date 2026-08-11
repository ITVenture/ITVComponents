using System;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.Extensions
{
    /// <summary>
    /// Registration of dashboard widget renderers.
    /// </summary>
    public static class WidgetRendererDependencyExtensions
    {
        /// <summary>
        /// Registers renderers for dashboard tiles. A widget names only the key; which component draws it
        /// is decided here.
        /// </summary>
        /// <example>
        /// <code>
        /// services.ConfigureWidgetRenderers(c => c.RegisterRenderer&lt;ChartWidgetRenderer&gt;());
        /// </code>
        /// Schluessel und Beschriftungen stehen am Typ (<see cref="WidgetRendererAttribute"/>); die
        /// Ueberladungen von <c>RegisterRenderer</c> uebersteuern sie, wenn derselbe Typ unter mehreren
        /// Schluesseln laufen soll.
        /// </example>
        public static IServiceCollection ConfigureWidgetRenderers(this IServiceCollection services,
            Action<WidgetRendererConfiguration> configure)
        {
            return services.Configure(configure);
        }
    }
}
