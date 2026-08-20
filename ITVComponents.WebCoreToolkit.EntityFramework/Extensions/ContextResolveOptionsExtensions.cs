using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.EntityFramework.DataSources;
using ITVComponents.WebCoreToolkit.EntityFramework.Options;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Extensions
{
    public static class ContextResolveOptionsExtensions
    {
        /// <summary>
        /// Registers a Factory that resolves Db-Contexts through the Plugin-Environment
        /// </summary>
        /// <param name="options">the foreign-key options object</param>
        /// <param name="usePermissionScope">indicates whether to use the permission-scope</param>
        /// <param name="perOperation">
        /// ob die Datenquelle in einem FRISCHEN Lade-Scope aufgeloest wird, der am Ende der Abfrage wieder
        /// zugeht. Vorbelegt <c>true</c>; <c>false</c> stellt das fruehere Verhalten wieder her.
        /// </param>
        /// <returns>the provided options-object for method-chaining</returns>
        /// <remarks>
        /// <para>
        /// <b>Warum pro Vorgang.</b> Frueher kam die Datenquelle aus <c>GetFactory()</c>, also aus dem Scope des
        /// uebergebenen Service-Providers. Bei einer HTTP-Anfrage ist das der Anfrage-Scope und damit kurzlebig -
        /// unauffaellig. Unter Blazor ist es der <b>Circuit</b>, und der lebt, solange der Benutzer die Seite
        /// offen haelt: das Dashboard reicht seinen injizierten <c>IServiceProvider</c> weiter, und die einmal
        /// aufgeloeste Datenquelle samt ihrem DbContext bleibt daran haengen - ueber einen Mandantenwechsel
        /// hinweg. Die Kachel zeigt dann die Daten des Mandanten, der beim Aufbau des Circuits galt.
        /// </para>
        /// <para>
        /// <c>CreateOperationScope()</c> loest die scope-eigenen Abhaengigkeiten (z.B. den DbContext) neu auf und
        /// gibt sie mit dem Scope wieder frei. Der Rueckgabewert ist deshalb ein <see cref="IScopedDataSource"/>:
        /// <c>ContextForDiagnosticsQuery</c> und <c>ContextForFkQuery</c> uebernehmen den Scope und schliessen
        /// ihn, wenn die umhuellte Quelle freigegeben wird - bei den Diagnose-Abfragen ausdruecklich erst nach dem
        /// Ende der Aufzaehlung, damit ein verzoegert gelesenes Ergebnis nicht ins Leere greift.
        /// </para>
        /// <para>
        /// <b>Der Name aendert sich nicht.</b> Gesucht wird weiterhin ueber
        /// <c>{PermissionPrefix}{area}{name}</c> mit denselben zwei Rueckfallstufen; nur die Lebensdauer der
        /// gefundenen Instanz ist eine andere. Das ist wichtig, weil die dritte Stufe absichtlich auf das
        /// <b>globale</b> Plugin zurueckfaellt - wer diesen Weg nimmt, bekommt eine Quelle ohne Mandanten-Praefix,
        /// und dass die dann die Daten des obersten Mandanten liefert, ist keine Frage der Lebensdauer.
        /// </para>
        /// </remarks>
        public static T UsePlugins<T>(this T options, bool usePermissionScope = true, bool perOperation = true) where T:ContextResolveOptions
        {
            return (T)options.RegisterService("*", (provider, s, area) =>
            {
                IWebPluginHelper plugins = provider.GetService<IWebPluginHelper>();
                IPermissionScope scope = null;
                if (usePermissionScope)
                {
                    scope = provider.GetService<IPermissionScope>();
                }

                // Den Praefix VOR dem Oeffnen des Scopes lesen: er kommt aus dem Provider des Aufrufers und
                // beantwortet die Frage "welcher Mandant gilt jetzt", nicht "welcher galt beim Laden".
                var prefix = scope?.PermissionPrefix;

                if (!perOperation)
                {
                    return ResolveSource(plugins.GetFactory(), prefix, area, s);
                }

                var operation = plugins.CreateOperationScope();
                try
                {
                    var retVal = ResolveSource(operation, prefix, area, s);
                    if (retVal == null)
                    {
                        // Nichts gefunden - den frisch geoeffneten Scope sofort wieder schliessen, sonst
                        // bliebe er bis zum Ende des umgebenden Scopes stehen, ohne je benutzt zu werden.
                        operation.Dispose();
                        return null;
                    }

                    return new ScopedDataSource(retVal, operation);
                }
                catch
                {
                    operation.Dispose();
                    throw;
                }
            });
        }

        /// <summary>
        /// Die Namensaufloesung, unveraendert aus der urspruenglichen Fassung: erst mit Bereich, dann ohne, zuletzt
        /// ganz ohne Mandanten-Praefix.
        /// </summary>
        private static object ResolveSource(IPluginFactory factory, string prefix, string area, string name)
        {
            var retVal = factory[$"{prefix}{area}{name}", true];
            if (retVal == null)
            {
                retVal = factory[$"{prefix}{name}", true];
            }

            if (retVal == null)
            {
                retVal = factory[name, true];
            }

            return retVal;
        }
    }
}
