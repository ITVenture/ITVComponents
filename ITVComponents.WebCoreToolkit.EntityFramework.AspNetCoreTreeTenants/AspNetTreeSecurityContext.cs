using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants
{
    public class AspNetTreeSecurityContext:AspNetTreeSecurityContext<AspNetTreeSecurityContext>
    {
        public AspNetTreeSecurityContext(DbContextModelBuilderOptions<AspNetTreeSecurityContext> modelBuilderOptions, DbContextOptions<AspNetTreeSecurityContext> options) : base(modelBuilderOptions, options)
        {
        }

        public AspNetTreeSecurityContext(IPermissionScope tenantProvider, IContextUserProvider userProvider, ILogger<AspNetTreeSecurityContext> logger, IOptions<DbContextModelBuilderOptions<AspNetTreeSecurityContext>> modelBuilderOptions, DbContextOptions<AspNetTreeSecurityContext> options) : base(tenantProvider, userProvider, logger, modelBuilderOptions, options)
        {
        }
    }
}
