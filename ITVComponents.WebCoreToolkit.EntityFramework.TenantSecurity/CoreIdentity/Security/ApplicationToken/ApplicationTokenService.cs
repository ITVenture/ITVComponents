using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Security.ApplicationToken
{
    internal class ApplicationTokenService:ApplicationTokenService<AspNetSecurityContext>
    {
        public ApplicationTokenService(IToolkitContextFactory contextFactory) : base(contextFactory)
        {
        }
    }
}
