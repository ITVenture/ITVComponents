using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Options;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Helpers
{
    /// <summary>
    /// Beantwortet <see cref="ISelfRegistrationPolicy"/> aus den Onboarding-Einstellungen
    /// (<c>TenantSetupOptions.AllowSelfRegistration</c>).
    /// </summary>
    /// <remarks>
    /// Die Einstellung liegt in den GlobalSettings und damit in der Datenbank: sie laesst sich im Betrieb
    /// umlegen, ohne die Anwendung neu auszurollen - was fuer einen Schalter, der die Registrierung oeffnet
    /// und schliesst, der Punkt ist.
    /// </remarks>
    public class SelfRegistrationPolicy : ISelfRegistrationPolicy
    {
        private readonly IGlobalSettings<TenantSetupOptions> options;

        public SelfRegistrationPolicy(IGlobalSettings<TenantSetupOptions> options)
        {
            this.options = options;
        }

        public bool AllowsSelfRegistration => options.ValueOrDefault?.AllowSelfRegistration == true;
    }
}
