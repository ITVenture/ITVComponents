using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using Microsoft.AspNetCore.Authentication;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.Extensions
{
    public interface IAuthenticationConfigProvider: IPlugin
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
