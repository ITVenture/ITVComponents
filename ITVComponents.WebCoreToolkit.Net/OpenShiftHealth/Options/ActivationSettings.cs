using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Options
{
    public class ActivationSettings
    {
        public bool ExposeHealthEndPoints { get; set; }

        public bool UseBuiltInMiddleware { get; set; }

        public string HealthBasePath { get; set; }

        public bool UseDefaultAppInfoFormatter { get; set; }

        public bool UseDefaultAppReadyFormatter { get; set; }

        public bool UseDefaultAppLiveFormatter { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string AppInfoModel { get; set; }
        public string ReadyInfoModel { get; set; }
        public string LiveInfoModel { get; set; }
        public bool UseCamelCase { get; set; } = true;
    }
}
