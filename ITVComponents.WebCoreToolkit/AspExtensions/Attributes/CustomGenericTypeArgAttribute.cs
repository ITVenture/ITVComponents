using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Attributes
{
    public class CustomGenericTypeArgAttribute:Attribute
    {
        public string Name { get; }

        public Type Type { get; }

        public CustomGenericTypeArgAttribute(string name, Type type)
        {
            this.Name=name;
            this.Type=type;
        }
    }
}
