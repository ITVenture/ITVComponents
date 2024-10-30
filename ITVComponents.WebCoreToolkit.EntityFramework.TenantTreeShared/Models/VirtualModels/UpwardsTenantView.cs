using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.VirtualModels
{
    public class UpwardsTenantView
    {
        public int OutermostLeafTenantId { get; set; }

        public string OutermostLeafTenantName { get; set; }

        public int ParentTenantId { get; set; }

        public string ParentTenantName { get; set; }

        public int ParentLevel { get; set; }
    }
}
