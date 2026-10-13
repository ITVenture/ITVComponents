using System;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Hub
{
    /// <summary>
    /// Meldet, wenn sich ein Dienst am Verteiler an- oder abmeldet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Eine eigene Schnittstelle und keine Erweiterung von <see cref="IEndPointBroker"/>: die Stellvertreter
    /// (<c>RemoteBrokerProxy</c>, <c>LocalBrokerProxy</c>) müssten sonst Ereignisse implementieren, die bei
    /// ihnen nie feuern. Wer zuhören will, fragt den Broker mit <c>as</c>.
    /// </para>
    /// <para>
    /// Gefeuert wird dort, wo der Dienst tatsächlich registriert ist. Eine an einen Unter-Verteiler
    /// weitergereichte Anmeldung meldet sich dort, nicht hier.
    /// </para>
    /// <para>
    /// Die Ereignisse laufen im Thread der Anmeldung. Wer daraufhin etwas Langsames tut (in eine Datenbank
    /// schreiben), tut es nebenläufig — sonst wartet der Dienst auf seine eigene Anmeldung.
    /// </para>
    /// </remarks>
    public interface IServiceRegistrationEvents
    {
        /// <summary>Ein Dienst hat sich angemeldet.</summary>
        event EventHandler<ServiceRegistrationEventArgs> ServiceRegistered;

        /// <summary>
        /// Ein Dienst ist weg — abgemeldet, wegen Zeitüberschreitung verworfen oder hart entfernt.
        /// </summary>
        event EventHandler<ServiceRegistrationEventArgs> ServiceUnregistered;
    }

    /// <summary>Welcher Dienst.</summary>
    public class ServiceRegistrationEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ServiceRegistrationEventArgs"/> class.</summary>
        public ServiceRegistrationEventArgs(string serviceName, string ownerScope)
        {
            ServiceName = serviceName;
            OwnerScope = ownerScope;
        }

        /// <summary>Der Dienstname.</summary>
        public string ServiceName { get; }

        /// <summary>Der Mandant, aus dem die Anmeldung kam, soweit bekannt.</summary>
        public string OwnerScope { get; }
    }
}
