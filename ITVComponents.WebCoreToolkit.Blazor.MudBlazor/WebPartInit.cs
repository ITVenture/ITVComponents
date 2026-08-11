using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazorLib.Config;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Diagnostics;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets;
using ITVComponents.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazorLib
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static object? LoadOptions(IConfiguration config, string path)
        {
            return config.GetSection<WebPartConfig>(path);
        }

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services, [WebPartConfig]WebPartConfig config)
        {
            services.AddToolkitForeignKeyCache();

            // Bewusst NICHT an UseViews gebunden: die Regeln darin betreffen Mud-Dialoge ueberhaupt,
            // also auch die, die ein Konsument selbst baut. Ohne sie scrollt kein Dialog-Inhalt, und
            // bei einem langen Formular liegt die Aktionsleiste ausserhalb des Bildes.
            services.AddToolkitClientStyleSheet(
                "_content/ITVComponents.WebCoreToolkit.Blazor.MudBlazor/itv-mudblazor.css");

            // Bewusst NICHT an UseViews gebunden: die Dashboard-Flaeche gehoert zur Basis, ohne die
            // Registrierung wuerde jede Kachel als "Renderer nicht registriert" enden.
            services.ConfigureWidgetRenderers(c =>
            {
                c.RegisterRenderer<ScribanWidgetRenderer>();
                // Zweiter Eintrag mit LEEREM Schluessel: Widgets aus der Zeit vor der Spalte tragen dort
                // nichts, und die sollen sich weiter verhalten wie bisher.
                c.RegisterRenderer<ScribanWidgetRenderer>(string.Empty);
                RegisterConfiguredRenderers(c, config.WidgetRenderers);
            });

            if (config.UseViews)
            {
                services.AddMudBlazorDiagnostics();
            }
        }

        /// <summary>
        /// Adds the renderers named in the part configuration.
        /// </summary>
        /// <remarks>
        /// Jeder Eintrag wird einzeln geprueft und ein fehlerhafter uebersprungen - mit Schluessel UND
        /// Typname im Log. Den ganzen Start daran scheitern zu lassen waere unverhaeltnismaessig; still
        /// weglassen aber auch: dann zeigte nur die betroffene Kachel spaeter, dass etwas fehlt.
        /// </remarks>
        private static void RegisterConfiguredRenderers(WidgetRendererConfiguration configuration,
            WidgetRendererConfig[]? configured)
        {
            if (configured == null)
            {
                return;
            }

            foreach (var item in configured)
            {
                if (string.IsNullOrWhiteSpace(item?.Type))
                {
                    LogEnvironment.LogEvent(
                        "Ein Widget-Renderer in der WebPart-Konfiguration hat keinen Typ und wird uebergangen.",
                        LogSeverity.Error);
                    continue;
                }

                try
                {
                    // Dieselbe Schreibweise wie bei ContextType in den SecurityContextOptions - ein Autor
                    // benutzt die Form, die er aus der Teile-Konfiguration ohnehin kennt.
                    var dic = new Dictionary<string, object>();
                    var type = (Type)ExpressionParser.Parse(item.Type, dic);
                    configuration.RegisterRenderer(type, item.Key, item.DisplayName, item.EditorLanguage);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(
                        $"Der Widget-Renderer '{item.Key ?? "(Schluessel aus dem Typ)"}' ({item.Type}) konnte nicht "
                        + $"registriert werden und wird uebergangen: {ex.OutlineException()}",
                        LogSeverity.Error);
                }
            }
        }
    }
}
