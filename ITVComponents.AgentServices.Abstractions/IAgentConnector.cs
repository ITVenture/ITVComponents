namespace ITVComponents.AgentServices.Abstractions
{
    /// <summary>
    /// Der Weg der Web-Anwendung zu einem Agenten: zu seinem Katalog und zu seinen Geräten.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Der Mandant ist Teil der Frage, nicht eine Prüfung daneben.</b> Aufgelöst wird nur ein Agent,
    /// dessen ClientApp diesem Mandanten gehört; ein fremder ist nicht „verboten", sondern nicht vorhanden.
    /// So gibt es keine Stelle, an der die Prüfung vergessen werden kann — der Dienst-Verteiler selbst kennt
    /// keinen Mandanten.
    /// </para>
    /// <para>
    /// Umgesetzt im AgentServices-Webpaket; den Transport (welcher Client für welchen Dienstnamen) steuert
    /// der Host bei.
    /// </para>
    /// </remarks>
    public interface IAgentConnector
    {
        /// <summary>Der Katalog eines Agenten.</summary>
        /// <param name="tenantId">der Mandant, dem die ClientApp gehören muss</param>
        /// <param name="clientKey">der <c>ClientKey</c> der ClientApp — zugleich ihr Dienstname</param>
        /// <param name="cancellationToken">bricht die Auflösung ab</param>
        /// <exception cref="AgentConnectionException">wenn der Agent nicht aufgelöst werden kann</exception>
        Task<IAgentObjectCatalog> GetCatalogAsync(int tenantId, string clientKey,
            CancellationToken cancellationToken = default);

        /// <summary>Ein Objekt auf einem Agenten, als Proxy.</summary>
        /// <typeparam name="T">der Vertrag des Objekts</typeparam>
        /// <param name="tenantId">der Mandant, dem die ClientApp gehören muss</param>
        /// <param name="clientKey">der <c>ClientKey</c> der ClientApp</param>
        /// <param name="objectName">der Name des Objekts am Agenten</param>
        /// <param name="cancellationToken">bricht die Auflösung ab</param>
        /// <exception cref="AgentConnectionException">wenn der Agent nicht aufgelöst werden kann</exception>
        Task<T> GetObjectAsync<T>(int tenantId, string clientKey, string objectName,
            CancellationToken cancellationToken = default) where T : class;
    }

    /// <summary>Warum ein Agent nicht erreicht werden konnte.</summary>
    public enum AgentConnectionFailure
    {
        /// <summary>Es gibt keine ClientApp mit diesem Schlüssel in diesem Mandanten.</summary>
        UnknownAgent,

        /// <summary>Die ClientApp ist abgeschaltet.</summary>
        Disabled,

        /// <summary>Der Agent ist nicht am Dienst-Verteiler angemeldet oder antwortet nicht.</summary>
        Unreachable,

        /// <summary>Der Host hat keinen Transport zu Agenten eingerichtet.</summary>
        NotConfigured
    }

    /// <summary>Ein Agent konnte nicht erreicht werden.</summary>
    public class AgentConnectionException : Exception
    {
        /// <summary>Initialisiert die Ausnahme.</summary>
        public AgentConnectionException(AgentConnectionFailure failure, string message, Exception? inner = null)
            : base(message, inner)
        {
            Failure = failure;
        }

        /// <summary>Warum.</summary>
        public AgentConnectionFailure Failure { get; }
    }
}
