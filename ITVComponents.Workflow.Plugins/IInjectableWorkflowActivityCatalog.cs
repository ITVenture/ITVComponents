using ITVComponents.Plugins;
using ITVComponents.Workflow.Activities;

namespace ITVComponents.Workflow.Plugins
{
    /// <summary>
    /// Plugin-faehige Fassung des <see cref="IWorkflowActivityCatalog"/>: kombiniert den (bewusst
    /// plugin-neutralen, IPC-tauglichen) Katalog-Vertrag mit <see cref="IPlugin"/>. So laesst sich ein
    /// Katalog ueber die Injectable-Plugin-Schiene bedienen - namentlich geleast
    /// (<c>IFreshInjectablePlugin&lt;IInjectableWorkflowActivityCatalog&gt;.Lease(name)</c>) fuer den Katalog
    /// einer bestimmten Worker-Instanz, oder ueber den per DI registrierten Default (der Fallback).
    /// </summary>
    /// <remarks>
    /// Der Kern-Vertrag <see cref="IWorkflowActivityCatalog"/> bleibt neutral (ohne <c>IPlugin</c>-Kopplung);
    /// diese Kopplung lebt bewusst nur in der Plugin-Schicht. Die vorhandenen Katalog-Implementierungen
    /// (<c>PluginActivityCatalog</c>, <c>WebPluginActivityCatalog</c>) implementieren ohnehin beide Vertraege.
    /// </remarks>
    public interface IInjectableWorkflowActivityCatalog : IWorkflowActivityCatalog, IPlugin
    {
    }
}
