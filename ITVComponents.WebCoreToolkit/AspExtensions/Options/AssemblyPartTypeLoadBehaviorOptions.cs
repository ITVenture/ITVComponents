using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Options
{
    public class AssemblyPartTypeLoadBehaviorOptions
    {
        public TypeRegisterBehavior DefaultBehavior { get; set; } = TypeRegisterBehavior.Use;
        public CustomTypeRegisterBehavior[] CustomLoadings { get; set; } = null;
    }
}
