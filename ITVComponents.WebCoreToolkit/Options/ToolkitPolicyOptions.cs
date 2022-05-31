using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Options
{
    public class ToolkitPolicyOptions
    {
        public bool CheckPermissions { get; set; } = true;

        public bool CheckFeatures { get; set; } = false;

        public List<string> SignInSchemes { get; set; } = new List<string>();
    }
}
