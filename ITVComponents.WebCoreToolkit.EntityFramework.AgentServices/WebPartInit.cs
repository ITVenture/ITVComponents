using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AgentExt = ITVComponents.WebCoreToolkit.EntityFramework.AgentServices.Extensions.DependencyInjectionExtensions;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AgentServices
{
    /// <summary>
    /// Registriert die Agent-Dienste für den Kontext des Hosts — sofern dieser die Tabelle führt
    /// (<see cref="IAgentServiceBindingContext"/>).
    /// </summary>
    /// <remarks>
    /// Die Rollen (<c>AddAgentServiceSlot</c>) und den <see cref="IAgentClientFactory"/> stellt der Host im Code;
    /// sie sind Teil seiner Fachlichkeit und seiner Verbindung zum Dienst-Verteiler.
    /// </remarks>
    [WebPart]
    public static class WebPartInit
    {
        /// <summary>Lädt die Optionen dieses WebParts.</summary>
        [LoadWebPartConfig]
        public static object? LoadOptions(IConfiguration config, string key, string path)
        {
            if (key == "ContextSettings")
            {
                return config.GetSection<SecurityContextOptions>(path);
            }

            return null;
        }

        /// <summary>Registriert die Dienste.</summary>
        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services,
            [WebPartConfig("ContextSettings")] SecurityContextOptions? options)
        {
            if (options is not { ConfigureContext: true, ContextType: { Length: > 0 } contextTypeName })
            {
                return;
            }

            var contextType = (Type)ExpressionParser.Parse(contextTypeName, new Dictionary<string, object>());
            if (!typeof(IAgentServiceBindingContext).IsAssignableFrom(contextType))
            {
                // Kein Fehler: der Host nutzt die Agent-Dienste (noch) nicht. Aber gesagt wird es.
                LogEnvironment.LogEvent(
                    $"The AgentServices web part is loaded, but context '{contextType.FullName}' does not implement IAgentServiceBindingContext - nothing is registered.",
                    LogSeverity.Warning);
                return;
            }

            var method = typeof(AgentExt).GetMethod<Func<IServiceCollection, IServiceCollection>>(contextType,
                nameof(AgentExt.AddAgentServices));
            method?.Invoke(services);
        }
    }
}
