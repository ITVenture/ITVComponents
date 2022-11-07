using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Models.RequestConservation
{
    internal class ConservedRequestData
    {
        public ClaimsPrincipal User { get; set; }
        public Dictionary<string, object> RouteData { get; set; }
        public string RequestPath { get; set; }
        public string CurrentScope { get; set; }
        public HttpContext HttpContext { get; set; }
    }
}
