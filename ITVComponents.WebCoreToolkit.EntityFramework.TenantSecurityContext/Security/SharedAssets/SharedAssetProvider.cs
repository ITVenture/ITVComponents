using System;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Security.SharedAssets
{
    public class SharedAssetProvider:SharedAssetProvider<SecurityContext>
    {
        public SharedAssetProvider(IUserNameMapper userNameMapper, ISecurityRepository securityRepo, SecurityContext database, IServiceProvider services) : base(userNameMapper, securityRepo, database, services)
        {
        }
    }
}
