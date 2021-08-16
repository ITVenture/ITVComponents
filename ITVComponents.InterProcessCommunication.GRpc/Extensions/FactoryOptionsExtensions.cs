using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Grpc.Security;
using ITVComponents.InterProcessCommunication.MessagingShared.Security;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.InterProcessCommunication.Grpc.Extensions
{
    public static class FactoryOptionsExtensions
    {
        public const string IdentityProviderKey = "IdentityProvider";

        public static FactoryOptions InjectIdentityProvider(this FactoryOptions options)
        {
            options.AddDependency(IdentityProviderKey, services => services.GetService<IIdentityProvider>());
            return options;
        }
    }
}
