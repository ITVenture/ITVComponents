using ITVComponents.Plugins;
using ITVComponents.Workflow.ValueHandles;

namespace ITVComponents.Workflow.Plugins
{
    /// <summary>
    /// Der Vertrag eines Wert-Handlers, der als Plugin geladen wird: zugleich ein
    /// <see cref="IWorkflowValueHandler"/> (fuer die Engine) und ein <see cref="IPlugin"/> (fuer die
    /// PluginFactory und deren Scope-gebundene Freigabe).
    /// </summary>
    /// <remarks>
    /// Derselbe Schnitt wie bei <see cref="IActivityPlugin"/>: der Kern
    /// (<c>ITVComponents.Workflow</c>) bleibt frei von einer Abhaengigkeit auf ITVComponents.Plugins -
    /// zusammengefuehrt werden die beiden Vertraege erst hier, im Plugin-Adapter.
    /// </remarks>
    public interface IValueHandlerPlugin : IWorkflowValueHandler, IPlugin
    {
    }
}
