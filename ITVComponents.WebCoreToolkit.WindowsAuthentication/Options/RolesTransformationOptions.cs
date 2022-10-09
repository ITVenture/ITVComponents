using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.WindowsAuthentication.Options
{
    public class RolesTransformationOptions
    {
        public Func<string,bool> GroupFilter{get; set; }
        public bool NormalizeGroupName { get; set; }
        public string? TargetClaimIssuer { get; set; }
        public string TargetClaimType { get; set; } = System.Security.Claims.ClaimTypes.Role;
    }
}
