using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.Options
{
    public class InjectorConfig
    {
        public string TypeExpression { get; set; }

        public string ProxyName { get; set; }

        public List<string> ObjectPatterns { get; set; } = new();
    }
}
