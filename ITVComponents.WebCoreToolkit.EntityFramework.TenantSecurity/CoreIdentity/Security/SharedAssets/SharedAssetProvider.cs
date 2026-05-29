using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Security.SharedAssets
{
    public class SharedAssetProvider:SharedAssetProvider<AspNetSecurityContext>
    {
        public SharedAssetProvider(IUserNameMapper userNameMapper, ISecurityRepository securityRepo, AspNetSecurityContext database, ISecurityAccessProvider securityAccessProvider, IServiceProvider services) : base(userNameMapper, securityRepo, database, securityAccessProvider, services)
        {
        }
    }
}
