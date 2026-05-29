using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Options;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity
{
    public class AspNetSecurityContext : AspNetSecurityContext<AspNetSecurityContext>
    {
        public AspNetSecurityContext(DbContextModelBuilderOptions<AspNetSecurityContext> modelBuilderOptions, DbContextOptions<AspNetSecurityContext> options) : base(modelBuilderOptions, options)
        {
        }

        public AspNetSecurityContext(IPermissionScope tenantProvider, IContextUserProvider userProvider,
            ILogger<AspNetSecurityContext> logger, IOptions<DbContextModelBuilderOptions<AspNetSecurityContext>> modelBuilderOptions, DbContextOptions<AspNetSecurityContext> options) : base(
            tenantProvider, userProvider, logger, modelBuilderOptions, options)
        {

        }

    }
}
