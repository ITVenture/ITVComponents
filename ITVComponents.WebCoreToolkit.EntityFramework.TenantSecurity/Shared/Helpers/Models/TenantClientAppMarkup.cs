namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    /// <summary>
    /// Eine Anwendung, die ein Mandant aus der Vorlage bekommt.
    /// </summary>
    public class TenantClientAppMarkup
    {
        /// <summary>
        /// Der Name der Anwendung. <b>Der Schluessel innerhalb des Mandanten</b> - danach wird beim
        /// erneuten Anwenden wiedererkannt.
        /// </summary>
        public string ClientName { get; set; }

        /// <summary>
        /// Das globale Anwendungs-Template, aus dem sie entsteht. Es begrenzt, welche Rechtebuendel ihr
        /// ueberhaupt zugestanden werden koennen.
        /// </summary>
        public string TemplateName { get; set; }

        /// <summary>Ob die Anwendung eingeschaltet ist.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Die zugestandenen Rechtebuendel, als Namen. Sie muessen zum Template gehoeren.
        /// </summary>
        public string[] PermissionSets { get; set; }

        // Der ClientKey steht hier BEWUSST NICHT: er ist systemweit eindeutig. Eine Kopie kollidierte
        // beim zweiten Mandanten, der aus derselben Vorlage entsteht. Er wird deshalb je Mandant neu
        // erzeugt - und aus demselben Grund reist auch kein Zugang und kein Geheimnis mit.
    }
}
