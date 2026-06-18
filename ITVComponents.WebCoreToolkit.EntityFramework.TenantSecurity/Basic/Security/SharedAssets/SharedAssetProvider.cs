using System;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Security.SharedAssets
{
    public class SharedAssetProvider:SharedAssetProvider<SecurityContext>
    {
        public SharedAssetProvider(IUserNameMapper userNameMapper, ISecurityRepository securityRepo, IToolkitContextFactory contextFactory, ISecurityAccessProvider securityAccessProvider, IServiceProvider services) : base(userNameMapper, securityRepo, contextFactory, securityAccessProvider, services)
        {
        }
    }
}
