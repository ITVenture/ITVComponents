using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Helpers
{
    public class PropertyRef<T> where T : class, new()
    {
        public PropertyRef(bool createDefaultValue)
        {
            if (createDefaultValue)
            {
                value = new T();
            }
        }

        private T value;

        public T Value
        {
            get => value;
            set => this.value = value;
        }
    }
}
