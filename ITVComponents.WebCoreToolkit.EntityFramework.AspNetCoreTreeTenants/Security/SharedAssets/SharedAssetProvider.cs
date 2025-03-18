using System;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Security.SharedAssets
{
    public class SharedAssetProvider:SharedAssetProvider<AspNetTreeSecurityContext>
    {
        public SharedAssetProvider(IUserNameMapper userNameMapper, ISecurityRepository securityRepo, AspNetTreeSecurityContext database, IServiceProvider services) : base(userNameMapper, securityRepo, database, services)
        {
        }
    }
}
