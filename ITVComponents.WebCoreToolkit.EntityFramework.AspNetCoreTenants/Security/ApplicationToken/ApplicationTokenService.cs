using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Security.ApplicationToken
{
    internal class ApplicationTokenService:ApplicationTokenService<AspNetSecurityContext>
    {
        public ApplicationTokenService(AspNetSecurityContext context) : base(context)
        {
        }
    }
}
