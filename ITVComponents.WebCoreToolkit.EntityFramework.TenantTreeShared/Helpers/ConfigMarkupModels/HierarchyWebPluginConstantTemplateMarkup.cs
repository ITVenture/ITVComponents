using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.ConfigMarkupModels
{
    public class HierarchyWebPluginConstantTemplateMarkup:ConstTemplateMarkup
    {
        public bool Inheritable { get; set; }
    }
}
