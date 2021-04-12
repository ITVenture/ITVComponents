using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ScopedDependencyAttribute:Attribute
    {
        public string FriendlyName { get; set; }
    }
}
