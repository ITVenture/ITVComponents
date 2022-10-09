using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class WebPartConfigAttribute:Attribute
    {
        public string ConfigurationKey { get; }

        public WebPartConfigAttribute(string configurationKey)
        {
            ConfigurationKey = configurationKey;
        }

        public WebPartConfigAttribute()
        {
        }
    }
}
