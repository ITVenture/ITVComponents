using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ITVComponents.DataAccess;
using ITVComponents.Formatting;
using ITVComponents.Plugins;

namespace ITVComponents.MailMerge
{
    public abstract class MergerStrategy:IPlugin
    {
        /// <summary>
        /// Holds a list of mergers that were initialized in the current AppDomain
        /// </summary>
        private static List<MergerStrategy> mergers = new List<MergerStrategy>();

        /// <summary>
        /// Initializes a new instance of the MergerStrategy class
        /// </summary>
        protected MergerStrategy()
        {
            lock (mergers)
            {
                mergers.Add(this);
            }
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets the merger with the given name from the list of initialized mergers
        /// </summary>
        /// <param name="mergerName">the name of the demanded merger</param>
        /// <returns>the demanded merger</returns>
        public static MergerStrategy GetMerger(string mergerName)
        {
            lock (mergers)
            {
                var retVal = mergers.FirstOrDefault(n => n.UniqueName == mergerName);
                if (retVal == null)
                {
                    throw new IndexOutOfRangeException("The demanded merger was not found!");
                }

                return retVal;
            }
        }

        /// <summary>
        /// Gets a list of available mergers
        /// </summary>
        /// <returns>a sorted list of available mergers</returns>
        public static string[] GetAvailableMergers()
        {
            lock (mergers)
            {
                return (from t in mergers orderby t.UniqueName select t.UniqueName).ToArray();
            }
        }

        /// <summary>
        /// Merges data using the explicit merger implementation of this class
        /// </summary>
        /// <param name="templateFile">the template-file that is contains the layout</param>
        /// <param name="data">the data that was read from a specific data-source</param>
        /// <param name="outputFolder">the output-folder into which the data is merged</param>
        /// <param name="nameFormat">the format of the target-name</param>
        public IEnumerable<string> Merge(string templateFile, DynamicResult[] data, string outputFolder, string nameFormat, Dictionary<string,object> additionalSettings = null)
        {
            return Merge(templateFile, data, o => string.Format(@"{0}\{1}", outputFolder, o.FormatText(nameFormat)), additionalSettings);
        }

        /// <summary>
        /// Merges data using the explicit merger implementation of this class
        /// </summary>
        /// <param name="templateFile">the template file</param>
        /// <param name="data">the data that is used to fill a list or a single page</param>
        /// <param name="outputFileName">the name of the resulting pdf</param>
        /// <param name="tableName">the tableName of the initial table to fill</param>
        public void MergeFlat(string templateFile, DynamicResult[] data, string outputFileName, string tableName = null, Dictionary<string,object> additionalSettings = null)
        {
            MergeFlatInternal(templateFile, data, outputFileName, tableName, additionalSettings);
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            OnDisposed();
            lock (mergers)
            {
                mergers.Remove(this);
            }
        }

        /// <summary>
        /// Implements the MailMerge for the specific strategy
        /// </summary>
        /// <param name="templateFile">the template-file that is used to perform the mail-merge</param>
        /// <param name="data">the data that was read from a specific datasource</param>
        /// <param name="fileCallback">a callback providing the name for the target-file that is used for the output</param>
        /// <param name="additionalSettings">additional settings that can be passed to the merger-implementation</param>
        protected abstract IEnumerable<string> Merge(string templateFile, DynamicResult[] data, GetOutputFileNameCallback fileCallback, Dictionary<string,object> additionalSettings);

        /// <summary>
        /// Merges data using the explicit merger implementation of this class
        /// </summary>
        /// <param name="templateFile">the template file</param>
        /// <param name="data">the data that is used to fill a list or a single page</param>
        /// <param name="outputFileName">the name of the resulting pdf</param>
        /// <param name="tableName">the tableName of the provided data-set</param>
        protected virtual void MergeFlatInternal(string templateFile, DynamicResult[] data, string outputFileName, string tableName, Dictionary<string,object> additionalSettings)
        {
            throw new NotImplementedException("The method is not supported by this merger!");
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }

    /// <summary>
    /// Delegate that is used to estimate the file-names of each generated file
    /// </summary>
    /// <param name="record">the data-record for which to create a new output file</param>
    /// <returns>a stream that points to the configured location</returns>
    public delegate string GetOutputFileNameCallback(DynamicResult record);
}