using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Options.Logging
{
    [SettingName("LogOptions")]
    public class DbLoggingOptions
    {
        public bool LogEnabled { get; set; } = true;
        public bool LogTrace { get; set; }
        public string[] TraceFilters { get; set; }
        public bool LogDebug { get; set; }
        public string[] DebugFilters { get; set; }
        public bool LogInformation { get; set; } = true;
        public string[] InformationFilters { get; set; }
        public bool LogWarning{get;set;}= true;
        public string[] WarningFilters { get; set; }
        public bool LogError { get; set; }= true;
        public string[] ErrorFilters { get; set; }
        public bool LogCritical{get;set;}= true;
        public string[] CriticalFilters { get; set; }
        public bool LogNone { get; set; }
    }
}
