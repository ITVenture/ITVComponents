using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.WebPlugins.Model
{
    internal class DbPluginBufferInfo
    {
        public WebPlugin Plugin { get; set; }
        public DateTime Created { get; set; }
    }
}
