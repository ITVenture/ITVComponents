using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.HelperModels
{
    public class WebPluginConstant
    {
        public string Name { get; set; }

        public string Value { get; set; }

        public bool IsGlobal { get; set; }

        public string DecryptKey { get; set; }
    }
}
