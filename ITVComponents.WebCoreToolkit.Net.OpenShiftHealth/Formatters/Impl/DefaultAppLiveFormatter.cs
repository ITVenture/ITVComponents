using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Health.Model;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Model;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Formatters.Impl
{
    internal class DefaultAppLiveFormatter:IAppInfoFormatter<LivenessModel>
    {
        public LivenessModel FormatAppInfo(AppInfoOptions options, HealthReport healthInfo)
        {
            var retVal = new LivenessModel
            {
                Live = healthInfo.Status == HealthStatus.Healthy ? UpState.Up : UpState.Down
            };

            return retVal;
        }
    }
}
