using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants
{
    public class AspNetSecurityContext : AspNetSecurityContext<AspNetSecurityContext>
    {
        public AspNetSecurityContext(DbContextOptions<AspNetSecurityContext> options) : base(options)
        {
        }

        public AspNetSecurityContext(IPermissionScope tenantProvider, IContextUserProvider userProvider,
            ILogger<AspNetSecurityContext> logger, DbContextOptions<AspNetSecurityContext> options) : base(
            tenantProvider, userProvider, logger, options)
        {

        }

    }
}
