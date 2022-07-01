using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Helpers
{
    public class PropertyRef<T>
    {
        private T value;

        public T Value { get; set; }
    }
}
