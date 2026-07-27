using ITVComponents.Plugins;
using ITVComponents.Workflow.Activities;

namespace ITVComponents.Workflow.Plugins
{
    /// <summary>
    /// Der Vertrag einer Aktivitaet, die als Plugin geladen wird: zugleich eine
    /// <see cref="IWorkflowActivity"/> (fuer die Engine) und ein <see cref="IPlugin"/> (fuer die
    /// PluginFactory und deren Scope-gebundene Freigabe).
    /// </summary>
    /// <remarks>
    /// Der Kern (<c>ITVComponents.Workflow</c>) bleibt bewusst frei von einer Abhaengigkeit auf
    /// ITVComponents.Plugins - deshalb erbt <see cref="IWorkflowActivity"/> dort nicht von
    /// <see cref="IPlugin"/>. Erst hier, im Plugin-Adapter, werden beide Vertraege
    /// zusammengefuehrt.
    /// </remarks>
    public interface IActivityPlugin : IWorkflowActivity, IPlugin
    {
    }
}
