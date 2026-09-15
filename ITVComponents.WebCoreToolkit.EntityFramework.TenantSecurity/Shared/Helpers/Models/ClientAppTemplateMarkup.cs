namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    /// <summary>
    /// Ein Anwendungs-Template im Konfigurations-Austausch.
    /// </summary>
    /// <remarks>
    /// Das Template ist die <b>Vokabelliste der Plattform</b>: es sagt, welche Rechtebuendel eine
    /// Anwendung dieser Art ueberhaupt bekommen kann. Es ist global und reist deshalb mit der
    /// Systemkonfiguration.
    /// <para>
    /// Die <b>Anwendungen selbst</b> (<c>ClientApp</c>) reisen NICHT hier mit - sie gehoeren einem
    /// Mandanten, und Mandantendaten sind Sache der Mandanten-Vorlage. Der Konfigurations-Austausch
    /// fuehrt durchgaengig nur globale Zeilen.
    /// </para>
    /// </remarks>
    public class ClientAppTemplateMarkup
    {
        /// <summary>Der systemweit eindeutige Name - der fachliche Schluessel dieser Sektion.</summary>
        public string Name { get; set; }

        /// <summary>Die Rechtebuendel, die dieses Template anbietet.</summary>
        public AppPermissionSetMarkup[] PermissionSets { get; set; }
    }

    /// <summary>
    /// Ein Rechtebuendel eines Templates.
    /// </summary>
    public class AppPermissionSetMarkup
    {
        /// <summary>
        /// Der Name des Buendels. <b>Nur je Template eindeutig</b> - beim Aufloesen muss deshalb immer
        /// das Template mitgegeben werden.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Die Rechte im Buendel, als <b>Namen</b>. Nur globale Rechte: ein Template wird in jeden
        /// Mandanten angewandt, ein mandantengebundenes Recht liesse sich dort nicht aufloesen.
        /// </summary>
        public string[] Permissions { get; set; }
    }
}
