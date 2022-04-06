using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    public class FeatureTemplateMarkup
    {
        public string FeatureName { get; set; }

        public bool InfiniteDuration { get; set; }

        public string DurationExpression { get; set; }
    }
}
