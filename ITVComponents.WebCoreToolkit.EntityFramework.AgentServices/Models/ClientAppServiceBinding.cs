using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AgentServices.Models
{
    /// <summary>
    /// Welches Gerät eine Kasse (ClientApp) in einer Rolle benutzt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Keine Einstellungen hier.</b> Die liegen beim Gerät am Agenten; diese Zeile hält nur fest,
    /// <i>welches</i> Objekt für <i>welche</i> Rolle angesprochen wird.
    /// </para>
    /// <para>
    /// Beide Fremdschlüssel sind echt, aber ohne Navigation: eine Navigation auf die ClientApp zöge deren
    /// Typparameter in dieses Paket, und die ClientApp bekommt bewusst keine Sammlung hiervon — das
    /// Sicherheitspaket kennt dieses Paket nicht. Konfiguriert über
    /// <c>modelBuilder.ConfigureAgentServices&lt;TTenant, TClientApp&gt;()</c>.
    /// </para>
    /// <para>
    /// Kein Config-Export, keine Mandanten-Vorlage: <see cref="ObjectName"/> beschreibt einen bestimmten
    /// Laden-PC.
    /// </para>
    /// </remarks>
    public class ClientAppServiceBinding
    {
        /// <summary>Der Schlüssel.</summary>
        [Key]
        public int ClientAppServiceBindingId { get; set; }

        /// <summary>Der Mandant. Muss der der ClientApp sein — geprüft beim Schreiben.</summary>
        public int TenantId { get; set; }

        /// <summary>Die Kasse. Wird sie gelöscht, gehen ihre Bindungen mit.</summary>
        public int ClientAppId { get; set; }

        /// <summary>Die Rolle, wie der Host sie deklariert hat (<c>receiptPrinter</c>, <c>cashDrawer</c>).</summary>
        [Required, MaxLength(64)]
        public string Slot { get; set; } = string.Empty;

        /// <summary>Das gewählte Gerät am Agenten — unter diesem Namen entsteht der Proxy.</summary>
        [Required, MaxLength(128)]
        public string ObjectName { get; set; } = string.Empty;

        /// <summary>Ob die Rolle bedient wird. Abgeschaltet statt gelöscht.</summary>
        public bool Enabled { get; set; } = true;
    }
}
