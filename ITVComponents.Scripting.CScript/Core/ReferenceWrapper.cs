using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Scripting.CScript.Core
{
    public class ReferenceWrapper:TypedNull, ITransparentValue
    {
        public object WrappedValue { get; private set; }

        public void SetValue(object value)
        {
            WrappedValue = value;
        }
    }
}
