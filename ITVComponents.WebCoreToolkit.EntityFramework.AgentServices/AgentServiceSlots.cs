namespace ITVComponents.WebCoreToolkit.EntityFramework.AgentServices
{
    /// <summary>
    /// Eine Rolle, die eine Kasse mit einem Gerät besetzen kann — vom Host deklariert.
    /// </summary>
    /// <param name="Name">der Schlüssel, unter dem die Bindung gespeichert wird (<c>receiptPrinter</c>)</param>
    /// <param name="DisplayName">wie die Rolle in der Maske heisst (<c>Bondrucker</c>)</param>
    /// <param name="Contract">der Vertrag, den ein Gerät für diese Rolle erfüllen muss</param>
    public sealed record AgentServiceSlot(string Name, string DisplayName, Type Contract);

    /// <summary>Warum eine Rolle nicht bedient werden kann — oder dass sie es kann.</summary>
    public enum AgentServiceStatus
    {
        /// <summary>Das Gerät ist da.</summary>
        Ok,

        /// <summary>Die Rolle ist nicht deklariert.</summary>
        UnknownSlot,

        /// <summary>Der verlangte Vertrag passt nicht zu dem der Rolle.</summary>
        ContractMismatch,

        /// <summary>Es ist kein Mandant aktiv.</summary>
        NoTenant,

        /// <summary>Die Kasse gibt es in diesem Mandanten nicht.</summary>
        UnknownClientApp,

        /// <summary>Die Rolle ist an dieser Kasse nicht besetzt.</summary>
        NotBound,

        /// <summary>Die Rolle ist besetzt, aber abgeschaltet.</summary>
        Disabled,

        /// <summary>Die Kasse ist abgeschaltet oder ihr Agent nicht erreichbar.</summary>
        Unreachable
    }

    /// <summary>Das Ergebnis der Auflösung einer Rolle.</summary>
    /// <typeparam name="T">der Vertrag</typeparam>
    public sealed class AgentServiceHandle<T> where T : class
    {
        /// <summary>Warum — oder <see cref="AgentServiceStatus.Ok"/>.</summary>
        public AgentServiceStatus Status { get; init; }

        /// <summary>Das Gerät; nur bei <see cref="AgentServiceStatus.Ok"/> gesetzt.</summary>
        public T? Service { get; init; }

        /// <summary>Der Name des Geräts am Agenten, soweit bekannt.</summary>
        public string? ObjectName { get; init; }

        /// <summary>Eine Erklärung für die Oberfläche oder das Protokoll.</summary>
        public string? Message { get; init; }
    }

    /// <summary>
    /// Löst eine Rolle einer Kasse zu ihrem Gerät auf.
    /// </summary>
    /// <remarks>
    /// Jeder Abbruchgrund hat einen eigenen <see cref="AgentServiceStatus"/> und einen eigenen Protokolleintrag
    /// — „kein Drucker eingerichtet" und „Drucker eingerichtet, aber Kasse aus" sind verschiedene Fehler.
    /// </remarks>
    public interface IAgentServiceResolver
    {
        /// <summary>Das Gerät, das eine Kasse in einer Rolle benutzt.</summary>
        /// <typeparam name="T">der Vertrag; muss der der Rolle sein (oder ein Basisvertrag davon)</typeparam>
        /// <param name="clientAppId">die Kasse</param>
        /// <param name="slot">die Rolle</param>
        /// <param name="cancellationToken">bricht die Auflösung ab</param>
        Task<AgentServiceHandle<T>> GetAsync<T>(int clientAppId, string slot, CancellationToken cancellationToken = default)
            where T : class;
    }
}
