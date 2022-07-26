using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.SettingsExtensions;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options
{
    public  class HealthCheckDefinition
    {
        public string Label { get; set; }

        [AutoResolveChildren] public Dictionary<string, object> ConditionVariables { get; set; } = new();

        public string UseExpression { get; set; }
    }
}
