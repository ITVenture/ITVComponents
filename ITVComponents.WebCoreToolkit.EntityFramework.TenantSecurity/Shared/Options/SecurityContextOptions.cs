using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Options
{
    public class SecurityContextOptions
    {
        public bool ConfigureContext { get; set; } = true;

        public string ContextType { get; set; }

        //public List<string> SignInSchemes { get; set; } = new();
    }
}
