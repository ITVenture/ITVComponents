using System;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Security.SharedAssets
{
    public class SharedAssetProvider:SharedAssetProvider<AspNetTreeSecurityContext>
    {
        public SharedAssetProvider(IUserNameMapper userNameMapper, ISecurityRepository securityRepo, AspNetTreeSecurityContext database, ISecurityAccessProvider securityAccessProvider, IServiceProvider services) : base(userNameMapper, securityRepo, database, securityAccessProvider, services)
        {
        }
    }
}
