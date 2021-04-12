using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ITVComponents.DataExchange.Configuration;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using Newtonsoft.Json;

namespace ITVComponents.DataExchange.FileDumping
{
    public class DumpRuleFile
    {
        /// <summary>
        /// Initializes a new instance of the DumpRuleFile class
        /// </summary>
        /// <param name="fileName">the fileName for which to alway have the newest content</param>
        public DumpRuleFile(DumpFormatFile formatFile)
        {
            FormatFile = formatFile;
        }

        /// <summary>
        /// Gets the FileName for the current file
        /// </summary>
        public DumpFormatFile FormatFile { get; }

        /// <summary>
        /// Gets the FileText for the given file
        /// </summary>
        public string FileText => GetFileText();

        /// <summary>
        /// Parses the content of the given File into a json object
        /// </summary>
        /// <typeparam name="T">the desired object-type</typeparam>
        /// <returns>the parsed value</returns>
        public T ParseJsonFile<T>()
        {
            T retVal = default(T);
            try
            {
                retVal = JsonConvert.DeserializeObject<T>(FileText);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Failed to deserialize Dump-Configuration. {ex.OutlineException()}",LogSeverity.Error);
            }

            return retVal;
        }

        /// <summary>
        /// Reads the filetext for the given file
        /// </summary>
        /// <returns></returns>
        private string GetFileText()
        {
            var retVal = FormatFile.FormatFile;
            if (FormatFile.FileMode == DumpFormatFileMode.File)
            {
                retVal= File.ReadAllText(retVal, Encoding.Default);
            }

            return retVal;
        }
    }
}
