using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Formatters
{
    public  interface IAppInfoFormatter<T>
    {
        T FormatAppInfo(AppInfoOptions options, HealthReport healthInfo);
    }
}
