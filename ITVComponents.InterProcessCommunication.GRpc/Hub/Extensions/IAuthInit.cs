using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.Extensions
{
    public interface IAuthInit
    {
        /// <summary>
        /// Registers an authentication service on this init instance
        /// </summary>
        /// <param name="provider">the config-provider that will inject configs at the appropriate point in time</param>
        void RegisterAuthenticationService(IAuthenticationConfigProvider provider);
    }
}
