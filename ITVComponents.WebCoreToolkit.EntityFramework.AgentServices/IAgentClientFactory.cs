using ITVComponents.InterProcessCommunication.Shared.Base;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AgentServices
{
    /// <summary>
    /// Liefert den Client, über den die Web-Anwendung einen Dienst am Dienst-Verteiler erreicht.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Das stellt der Host.</b> Wie die Anwendung mit dem Verteiler verbunden ist (gRPC, im selben
    /// Prozess, mit welchen Zugangsdaten), weiss nur sie. <c>IInjectableProxy&lt;T&gt;</c> reicht hier nicht:
    /// der ist je Typ fest verdrahtet, ein Agent wird aber erst zur Laufzeit über seinen <c>ClientKey</c>
    /// gewählt.
    /// </para>
    /// <para>
    /// Der Client gehört der Fabrik — sie darf ihn je Dienstname puffern, der Aufrufer gibt ihn nicht frei.
    /// </para>
    /// </remarks>
    public interface IAgentClientFactory
    {
        /// <summary>Der Client für einen Dienst.</summary>
        /// <param name="serviceName">der Dienstname — der <c>ClientKey</c> der ClientApp</param>
        IBaseClient GetClient(string serviceName);
    }
}
