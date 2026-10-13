using ITVComponents.AgentServices.Abstractions;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AgentServices
{
    /// <summary>Eine Rolle an einer Kasse, wie die Maske sie zeigt.</summary>
    /// <param name="Slot">die Rolle</param>
    /// <param name="DisplayName">ihr Anzeigename</param>
    /// <param name="ObjectName">das gewählte Gerät; leer = nicht besetzt</param>
    /// <param name="Enabled">ob die Rolle bedient wird</param>
    public sealed record AgentServiceBindingInfo(string Slot, string DisplayName, string? ObjectName, bool Enabled);

    /// <summary>Wie es um den Agenten einer Kasse steht.</summary>
    /// <param name="Reachable">ob sein Katalog gerade antwortet</param>
    /// <param name="LastSeenUtc">wann er sich zuletzt an- oder abgemeldet hat</param>
    /// <param name="Message">warum nicht erreichbar, falls nicht</param>
    public sealed record AgentStatusInfo(bool Reachable, DateTime? LastSeenUtc, string? Message);

    /// <summary>Eine Pflege-Operation wurde abgelehnt — fehlendes Recht, fremde Kasse, unbekannte Rolle.</summary>
    public class AgentServiceAdministrationException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="AgentServiceAdministrationException"/> class.</summary>
        public AgentServiceAdministrationException(string message, Exception? inner = null) : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Die Pflege der Rollen einer Kasse: welches Gerät, an oder aus, und seine Einstellungen am Agenten.
    /// </summary>
    /// <remarks>
    /// Prüft in jeder Methode selbst — Leserecht <c>Apps.View</c>, Schreibrecht <c>Apps.Write</c>, dieselben wie
    /// die ClientApp-Verwaltung. Der Mandant kommt aus dem Sicherheitsbereich; eine Kasse eines anderen Mandanten
    /// gibt es hier nicht.
    /// </remarks>
    public interface IAgentServiceAdministration
    {
        /// <summary>Darf die Rollen sehen.</summary>
        bool CanView();

        /// <summary>Darf Rollen besetzen und Geräte einrichten.</summary>
        bool CanManage();

        /// <summary>Die Rollen einer Kasse mit ihrer Besetzung — alle deklarierten, auch unbesetzte.</summary>
        Task<IReadOnlyList<AgentServiceBindingInfo>> GetBindingsAsync(int clientAppId, CancellationToken cancellationToken = default);

        /// <summary>Wie es um den Agenten steht.</summary>
        Task<AgentStatusInfo> GetStatusAsync(int clientAppId, CancellationToken cancellationToken = default);

        /// <summary>Die Geräte am Agenten, die den Vertrag der Rolle erfüllen.</summary>
        Task<IReadOnlyList<AgentObjectInfo>> GetOffersAsync(int clientAppId, string slot, CancellationToken cancellationToken = default);

        /// <summary>Die Einstellungen, die ein Gerät zulässt.</summary>
        Task<IReadOnlyList<SettingDescriptor>> DescribeSettingsAsync(int clientAppId, string objectName, CancellationToken cancellationToken = default);

        /// <summary>Was am Agenten für ein Gerät abgelegt ist; <c>null</c> = nicht eingerichtet.</summary>
        Task<string?> GetSettingsAsync(int clientAppId, string objectName, CancellationToken cancellationToken = default);

        /// <summary>Eine Auswahl, die nur der Rechner vor Ort kennt.</summary>
        Task<IReadOnlyList<SettingChoice>> GetChoicesAsync(int clientAppId, string objectName, string settingName,
            string? draftJson, CancellationToken cancellationToken = default);

        /// <summary>Gibt Einstellungen an den Agenten, der sie prüft und ablegt.</summary>
        Task<ApplySettingsResult> ApplySettingsAsync(int clientAppId, string objectName, string settingsJson,
            CancellationToken cancellationToken = default);

        /// <summary>Besetzt eine Rolle (oder ändert die Besetzung).</summary>
        Task SaveBindingAsync(int clientAppId, string slot, string objectName, bool enabled,
            CancellationToken cancellationToken = default);

        /// <summary>Gibt eine Rolle frei.</summary>
        Task RemoveBindingAsync(int clientAppId, string slot, CancellationToken cancellationToken = default);
    }
}
