using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    [JsonDerivedType(typeof(AuthenticationTypeClaimTemplateMarkup), "base")]
    public class AuthenticationTypeTemplateMarkup
    {
        public string AuthenticationTypeName { get; set; }
    }
}
