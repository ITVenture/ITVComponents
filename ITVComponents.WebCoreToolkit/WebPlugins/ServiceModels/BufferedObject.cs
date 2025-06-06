using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.WebPlugins.ServiceModels
{
    internal class BufferedObject
    {
        public BufferedObject(object value, DateTime validThrough)
        {
            Value = value;
            ValidThrough = validThrough;
        }

        public object Value { get; }

        public DateTime ValidThrough { get; }
    }
}
