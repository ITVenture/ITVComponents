using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext
{
    public class SecurityContext: SecurityContext<SecurityContext>
    {
        public SecurityContext(DbContextOptions<SecurityContext> options) : base(options)
        {
        }

        public SecurityContext(IPermissionScope tenantProvider, IContextUserProvider userProvider,
            ILogger<SecurityContext> logger, DbContextOptions<SecurityContext> options) : base(
            tenantProvider, userProvider, logger, options)
        {

        }

    }
}
