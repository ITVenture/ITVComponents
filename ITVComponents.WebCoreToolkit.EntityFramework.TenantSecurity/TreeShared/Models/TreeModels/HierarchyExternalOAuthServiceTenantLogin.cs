using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels
{
    public class HierarchyExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin> : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthService : HierarchyExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceState : HierarchyExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceTenantLogin: HierarchyExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TTenant : HierarchyTenant
    {
    }

    public class HierarchyExternalOAuthServiceTenantLogin : HierarchyExternalOAuthServiceTenantLogin<HierarchyTenant,
        HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState, HierarchyExternalOAuthServiceTenantLogin>
    {

    }
}
