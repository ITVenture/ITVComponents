using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Helpers
{
    public class PropertyRef<T>:LaxPropertyRef<T> where T : class, new()
    {
        private bool createDefaultValue;
        public PropertyRef(bool createDefaultValue)
        {
            this.createDefaultValue = createDefaultValue;
        }

        public override T Value
        {
            get => createDefaultValue ? base.Value ??= new T() : base.Value;
            set => base.Value=value;
        }
    }

    public class LaxPropertyRef<T> where T : class
    {
        public LaxPropertyRef()
        {
        }

        private T value;

        public virtual T Value
        {
            get => value;
            set => this.value = value;
        }
    }
}
