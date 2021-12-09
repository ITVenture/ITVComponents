using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models
{
    internal class ConservedRequestData
    {
        public ClaimsPrincipal User { get; set; }
        public Dictionary<string, object> RouteData { get; set; }
        public string RequestPath { get; set; }
        public string CurrentScope { get; set; }
    }
}
