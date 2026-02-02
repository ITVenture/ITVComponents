using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.ExternalOAuthServices.Model
{
    public class ExternalOAuthServiceBufferInfo
    {
        public int? ExternalOAuthServiceId { get; set; }
        public ExternalOAuthConnection Service { get; set; }
        public DateTime Created { get; set; }
        public int? TenantId { get; set; }
        public string TenantName { get; set; }
    }
}
