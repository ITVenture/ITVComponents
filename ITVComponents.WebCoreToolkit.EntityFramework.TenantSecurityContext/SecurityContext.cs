using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.SyntaxHelper;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext
{
    public class SecurityContext: SecurityContext<SecurityContext>
    {
        public SecurityContext(ICalculatedColumnsSyntaxProvider syntaxProvider,DbContextOptions<SecurityContext> options) : base(syntaxProvider, options)
        {
        }

        public SecurityContext(IPermissionScope tenantProvider, IContextUserProvider userProvider,
            ILogger<SecurityContext> logger, DbContextOptions<SecurityContext> options) : base(
            tenantProvider, userProvider, logger, options)
        {

        }

    }
}
