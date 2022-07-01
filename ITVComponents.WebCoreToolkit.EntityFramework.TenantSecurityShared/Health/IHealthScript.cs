using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Health
{
    public interface IHealthScript
    {
        Dictionary<string, object> Results { get; set; }
        string AssetKey { get; set; }
        Func<string,IPlugin> GetPlugin { get; set; }
    }
}
