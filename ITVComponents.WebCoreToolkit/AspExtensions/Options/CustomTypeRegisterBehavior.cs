using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Options
{
    public class CustomTypeRegisterBehavior
    {
        private Type type;
        public TypeRegisterBehavior LoadBehavior { get; set; }

        public string TypeName { get; set; }

        public Type Type => type ??= Type.GetType(TypeName);
    }

    public enum TypeRegisterBehavior
    {
        Default,
        Use,
        Ignore
    }
}
