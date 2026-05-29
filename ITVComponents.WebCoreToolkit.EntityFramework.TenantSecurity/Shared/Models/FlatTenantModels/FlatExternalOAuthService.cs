using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels
{
    public class FlatExternalOAuthService: ExternalOAuthService<Tenant,FlatExternalOAuthService, FlatExternalOAuthServiceState, FlatExternalOAuthServiceTenantLogin>
    {
    }
}
