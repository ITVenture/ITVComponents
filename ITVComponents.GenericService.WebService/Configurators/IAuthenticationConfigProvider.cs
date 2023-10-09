using ITVComponents.Plugins;
using Microsoft.AspNetCore.Authentication;

namespace ITVComponents.GenericService.WebService.Configurators
{
    public interface IAuthenticationConfigProvider
    {
        /// <summary>
        /// Configures the shared default of the Authentication builder
        /// </summary>
        /// <param name="sharedOptions"></param>
        void ConfigureDefaults(AuthenticationOptions sharedOptions);

        /// <summary>
        /// Configures a specific authentication scheme on the provided builder
        /// </summary>
        /// <param name="authenticationBuilder">the used authentication builder</param>
        void ConfigureAuth(AuthenticationBuilder authenticationBuilder);
    }
}
