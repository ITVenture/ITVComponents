using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options
{
    public class SecurityContextOptions
    {
        public string ContextType { get; set; }

        //public List<string> SignInSchemes { get; set; } = new();
    }
}
