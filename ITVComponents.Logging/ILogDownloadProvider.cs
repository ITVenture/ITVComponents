using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Logging
{
    public interface ILogDownloadProvider
    {
        /// <summary>
        /// Downloads the current log
        /// </summary>
        /// <returns>a byte-array containing the current log</returns>
        byte[] DownloadCurrentLog();

        /// <summary>
        /// Downloads a hist-file
        /// </summary>
        /// <param name="name">the name of the hist-file</param>
        /// <returns>a byte-array containing the requested log</returns>
        byte[] DownloadHistory(string name);

        /// <summary>
        /// Gets the hist-names that are currently available
        /// </summary>
        /// <returns>a list of log-names that are available for download</returns>
        string[] GetHistNames();
    }
}
