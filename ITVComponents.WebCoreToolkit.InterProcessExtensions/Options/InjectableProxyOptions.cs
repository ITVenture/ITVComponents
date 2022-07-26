using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Settings.Native;
using ITVComponents.SettingsExtensions;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.Options
{
    public class InjectableProxyOptions
    {
        [AutoResolveChildren]
        public List<InjectorConfig> Injectors { get; set; } = new();
    }
}
