using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using ITVComponents.Helpers;
using ITVComponents.Plugins.Management;

namespace ITVComponents.Plugins.ApplicationManagementServices
{
    public class ApplicationStatistics : IStatisticsProvider
    {
        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Instructs a IMetricsProvider Implementing object to start gathering System metrics
        /// </summary>
        /// <returns>a value indicating whether the starting of metrincs-gathering was successful</returns>
        public bool BeginCollectStatistics()
        {
            return true;
        }

        /// <summary>
        /// Instructs a IMetricsProvider implementing object to end the System metrics gathering
        /// </summary>
        /// <returns>indicates whether the stopping of the gathering process was successful</returns>
        public bool EndCollectStatistics()
        {
            return true;
        }

        /// <summary>
        /// Resets the previously collected statistic data
        /// </summary>
        public void ResetStats()
        {
        }

        /// <summary>
        /// Gets statistics that were collected since the call of BeginCollectStatistics
        /// </summary>
        /// <returns>a key-value set containing informations about the runtime-status of this object</returns>
        public Dictionary<string, object> GetStatistics()
        {
            Dictionary<string,object> retVal = new Dictionary<string, object>();
            Process currentProc = Process.GetCurrentProcess();
            retVal.Add("Process-Id",currentProc.Id);
            retVal.Add("Modules",(from t in currentProc.Modules.Cast<ProcessModule>()
             select
                 new ModuleInfo
                     {
                         FileName = t.FileName,
                         Version = t.FileVersionInfo.ToString(),
                         SizeInMemory = t.ModuleMemorySize
                     }.ToDictionary()).ToArray());
            retVal.Add("StartTime",currentProc.StartTime);
            retVal.Add("NumberOfThreads", currentProc.Threads.Count);
            retVal.Add("TotalProcessorTime",currentProc.TotalProcessorTime);
            retVal.Add("PrivateMemorySize64", currentProc.PrivateMemorySize64);
            retVal.Add("VirtualMemorySize64", currentProc.VirtualMemorySize64);
            retVal.Add("PagedMemorySize64", currentProc.PagedMemorySize64);
            retVal.Add("PagedSystemMemorySize64", currentProc.PagedSystemMemorySize64);
            retVal.Add("NonpagedSystemMemorySize64",currentProc.NonpagedSystemMemorySize64);
            retVal.Add("PeakPagedMemorySize64", currentProc.PeakPagedMemorySize64);
            retVal.Add("PeakVirtualMemorySize64", currentProc.PeakVirtualMemorySize64);
            return retVal;
        }

        /// <summary>
        /// Raises the Disposed event that is used by the factory
        /// </summary>
        protected virtual void OnDisposed()
        {
            EventHandler handler = Disposed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;

        /// <summary>
        /// Module Information for a loaded module
        /// </summary>
        [Serializable]
        public class ModuleInfo
        {
            /// <summary>
            /// Gets or sets the FileName and its location in the filesystem
            /// </summary>
            public string FileName { get; set; }

            /// <summary>
            /// Gets or sets the Version information for this file
            /// </summary>
            public string Version { get; set; }

            /// <summary>
            /// Gets or sets the size in bytes that is taken by this module
            /// </summary>
            public int SizeInMemory { get; set; }
        }
    }
}