using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext
{
    public class SecurityContext: SecurityContext<SecurityContext>
    {
        public SecurityContext(DbContextModelBuilderOptions<SecurityContext> builderOptions, DbContextOptions<SecurityContext> options) : base(builderOptions,options)
        {
        }

        public SecurityContext(IPermissionScope tenantProvider, IContextUserProvider userProvider,
            ILogger<SecurityContext> logger, IOptions<DbContextModelBuilderOptions<SecurityContext>> builderOptions, DbContextOptions<SecurityContext> options) : base(
            tenantProvider, userProvider, logger, builderOptions, options)
        {
        }

    }
}
