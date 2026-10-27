namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    /// <summary>
    /// A token-request parameter of an external service in the system configuration.
    /// </summary>
    public class ExternalOAuthServiceParameterTemplateMarkup
    {
        public string ParameterName { get; set; }

        /// <summary>The value; always null for a secret parameter - secrets do not travel with the configuration.</summary>
        public string ParameterValue { get; set; }

        public bool IsSecret { get; set; }
    }
}
