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
    internal class DefaultAppInfoFormatter:IAppInfoFormatter<AppInfoModel>
    {
        public AppInfoModel FormatAppInfo(AppInfoOptions options, HealthReport healthInfo)
        {
            var retVal = new AppInfoModel
            {
                Version= options.Version ,
                Description= options.Description ,
                Name= options.Name ,
                TimeStamp= DateTime.Now ,
                State= healthInfo.Status == HealthStatus.Healthy ? UpState.Up : UpState.Down
            };

            FormatHealthInfo(healthInfo).ForEach(retVal.Checks.Add);

            return retVal;
        }

        private List<Check> FormatHealthInfo(HealthReport report)
        {
            var retVal = new List<Check>();
            foreach (var entry in report.Entries)
            {
                var check = new Check
                {
                    Name = entry.Key,
                    Status = entry.Value.Status == HealthStatus.Healthy ? UpState.Up : UpState.Down
                };

                retVal.Add(check);
                foreach (var item in entry.Value.Data)
                {
                    if (item.Value is HealthScriptResult hsr)
                    {
                        var ck = new Check
                        {
                            Name = item.Key,
                            Status = hsr.Status == HealthStatus.Healthy?UpState.Up:UpState.Down
                        };

                        retVal.Add(ck);
                    }
                }
            }

            return retVal;
        }
    }
}
