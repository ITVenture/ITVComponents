using ITVComponents.WebCoreToolkit.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.UserScopes.CookieModels
{
    public class ScopeInfo:Models.ScopeInfo
    {
        public DateTime Created { get; set; } = DateTime.Now;

        public int[] Permissions { get; set; }

        public int[] Features { get; set; }
    }
}
