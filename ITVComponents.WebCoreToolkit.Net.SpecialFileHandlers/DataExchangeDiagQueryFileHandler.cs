using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess;
using ITVComponents.DataExchange.Configuration;
using ITVComponents.DataExchange.Interfaces;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.Net.FileHandling;
using ITVComponents.WebCoreToolkit.Net.FileHandling.Special;
using ITVComponents.WebCoreToolkit.Net.SpecialFileHandlers.Config;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.SpecialFileHandlers
{
    public class DataExchangeDiagQueryFileHandler : DiagnosticsQueryFileHandler
    {
        private readonly IDataDumper dumper;

        private IGlobalSettingsProvider globalSettigs;

        private IScopedSettingsProvider scopeSettings;

        private IGlobalSettingsProvider GlobalSettings =>
            globalSettigs ??= Services.GetService<IGlobalSettingsProvider>();

        private IScopedSettingsProvider ScopedSettings =>
            scopeSettings ??= Services.GetService<IScopedSettingsProvider>();

        /// <summary>
        /// Initializes a new instance of the DataExchangeDiagQueryFileHandler class
        /// </summary>
        /// <param name="services">the serviceprovider that can be used to resolve further dependencies</param>
        /// <param name="dumper">the dumper instance that is used to materialize the querydata to a file</param>
        public DataExchangeDiagQueryFileHandler(IServiceProvider services, IDataDumper dumper) : base(services)
        {
            this.dumper = dumper;
        }

        /// <summary>
        /// Materializes the QueryData of the requested DiagnoseQuery into the capable file format
        /// </summary>
        /// <param name="data">the result of the diagnose query</param>
        /// <param name="queryName">the name of the query that was executed</param>
        /// <param name="downloadIdentity">the download-identity that was used to request the data</param>
        /// <returns>a file-read result that describes the retrieved data</returns>
        protected override async Task<AsyncReadFileResult> MaterializeQueryData(object[] data, string queryName,
            IIdentity downloadIdentity)
        {
            var settingsName = $"{UniqueName}CfgFor{queryName}";
            var setting = ScopedSettings.GetJsonSetting(settingsName);
            if (string.IsNullOrEmpty(setting))
            {
                setting = GlobalSettings.GetJsonSetting(settingsName);
            }

            DiagQueryDumpConfig cfg = null;
            if (!string.IsNullOrEmpty(setting))
            {
                cfg = JsonHelper.FromJsonString<DiagQueryDumpConfig>(setting);
            }
            else
            {
                cfg = new DiagQueryDumpConfig();
            }

            if (cfg.DumpConfig == null)
            {
                cfg.DumpConfig = new DumpConfiguration
                {
                    Name = "--Default-CFG-",
                    Source = ".",
                    Files = new DumpFormatFileCollection
                    {
                        new()
                        {
                            Children = new DumpConfigurationCollection
                            {
                                new()
                                {
                                    Name = "Default",
                                    Source = "Default"
                                }
                            }
                        }
                    }
                };
            }

            var tmp = new MemoryStream();
            var rawData = (from t in data select ToDynResult(t)).ToArray();
            dumper.DumpData(tmp, new[] { new DynamicResult(new Dictionary<string, object> { { queryName, rawData } }) },
                cfg.DumpConfig);
            await tmp.FlushAsync();
            await tmp.DisposeAsync();
            var dumpData = tmp.ToArray();
            tmp = new MemoryStream(dumpData);
            //tmp.Seek(0, SeekOrigin.Begin);
            var retVal = new AsyncReadFileResult
            {
                DownloadName = cfg.DownloadName,
                FileDownload = cfg.FileDownload,
                ContentType = cfg.ContentType,
                FileContent = tmp,
                Success = true
            };
            return retVal;
        }

        /// <summary>
        /// Converts an object to a DynamicResult object that can be dumped using the DataExchange-DataDumper objects
        /// </summary>
        /// <param name="obj">an object that needs to be converted to a DynamicResult</param>
        /// <returns>the DynamicResult that represents the provided object</returns>
        private static DynamicResult ToDynResult(object obj)
        {
            if (obj is IDictionary<string, object> sodic)
            {
                return new DynamicResult(new Dictionary<string, object>(sodic));
            }
            else if (obj is IBasicKeyValueProvider bvp)
            {
                if (bvp is DynamicResult dr)
                {
                    return dr;
                }

                var dic = new Dictionary<string, object>();
                foreach (var item in bvp.Keys)
                {
                    dic.Add(item,bvp[item]);
                }

                return new DynamicResult(dic);
            }
            else
            {
                return new DynamicResult(obj.ToDictionary(true));
            }
        }
    }
}
