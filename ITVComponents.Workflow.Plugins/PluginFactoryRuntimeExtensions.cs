using System;
using ITVComponents.Plugins;
using ITVComponents.Workflow.Runtime;

namespace ITVComponents.Workflow.Plugins
{
    /// <summary>
    /// Verbindet die geteilte Workflow-Laufzeit-Umgebung mit dem Lebenszyklus der PluginFactory.
    /// </summary>
    public static class PluginFactoryRuntimeExtensions
    {
        /// <summary>
        /// Sorgt dafuer, dass jedes von der Factory gebaute Plugin, das
        /// <see cref="IWorkflowRuntimeAware"/> implementiert, unmittelbar nach dem Zusammenbau den
        /// angegebenen Laufzeit-Kontext gesetzt bekommt (Engine, Worker, on-demand geladene
        /// Aktivitaeten). Genau das ist das "nach dem Zusammenbau reindruecken": der Besitzer des
        /// Kontexts haengt sich einmal in <see cref="PluginFactory.PluginInitialized"/> ein.
        /// </summary>
        /// <param name="factory">die Factory, deren Plugins den Kontext erhalten sollen</param>
        /// <param name="context">der zu verteilende Laufzeit-Kontext (z.B. <c>engine.Runtime</c>)</param>
        public static void PublishWorkflowRuntime(this PluginFactory factory, WorkflowRuntimeContext context)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            factory.PluginInitialized += (_, e) =>
            {
                if (e.Plugin is IWorkflowRuntimeAware aware)
                {
                    aware.Runtime = context;
                }
            };
        }
    }
}
