using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Options
{
    public class ManagementNavPageDefinition
    {
        public string NavTag { get; set; }

        public string NavLinkId { get; set; }

        public string PageLink { get; set; }

        public string LocalizerType { get; set; }

        public string NavigationLinkText { get; set; }

        public string AddBefore { get; set; }
        public string AddAfter { get; set; }
    }
}
