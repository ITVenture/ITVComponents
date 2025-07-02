using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    [JsonDerivedType(typeof(NavigationMenuTemplateMarkup), "base")]
    public class NavigationMenuTemplateMarkup
    {
        public string DisplayName { get; set; }

        public string Url { get; set; }

        public int? SortOrder { get; set; }

        public string PermissionName { get; set; }

        public string FeatureName { get; set; }

        public string SpanClass { get; set; }

        public string RefTag { get; set; }

        public string ParentRef { get; set; }
        public bool IsPublic { get; set; }
    }
}
