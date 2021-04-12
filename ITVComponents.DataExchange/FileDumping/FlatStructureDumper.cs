using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ITVComponents.DataAccess;
using ITVComponents.DataExchange.Configuration;
using ITVComponents.DataExchange.Interfaces;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Formatting;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Helpers;

namespace ITVComponents.DataExchange.FileDumping
{
    public class FlatStructureDumper:IDataDumper
    {
        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Dumps collected data into the given file
        /// </summary>
        /// <param name="fileName">the name of the target filename for this dump-run</param>
        /// <param name="data">the data that must be dumped</param>
        /// <param name="configuration">the dumper-configuiration</param>
        /// <returns>a value indicating whether there was any data available for dumping</returns>
        public bool DumpData(string fileName, DynamicResult[] data, DumpConfiguration configuration)
        {
            using (FileStream fst = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                return DumpData(fst, data, configuration);
            }
        }

        /// <summary>
        /// Dumps collected data into the given stream
        /// </summary>
        /// <param name="outputStream">the output-stream that will receive the dumped data</param>
        /// <param name="data">the data that must be dumped</param>
        /// <param name="configuration">the dumper-configuiration</param>
        /// <returns>a value indicating whether there was any data available for dumping</returns>
        public bool DumpData(Stream outputStream, DynamicResult[] data, DumpConfiguration configuration)
        {
            using (
                StreamWriter writer =
                    new StreamWriter(outputStream, Encoding.Default))
            {
                Scope scope = new Scope(true);
                return DumpSection(writer, data, configuration, scope);
            }
        }

        /// <summary>
        /// Dumps a single section of data
        /// </summary>
        /// <param name="writer">the target writer</param>
        /// <param name="data">the dumped data</param>
        /// <param name="configuration">the dumper configuration</param>
        /// <param name="scope">the scope that is used for variable exposition</param>
        private bool DumpSection(StreamWriter writer, DynamicResult[] data, DumpConfiguration configuration, Scope scope)
        {
            bool retVal = false;
            foreach (DynamicResult result in data)
            {
                foreach (string key in result.Keys)
                {
                    if (!(result[key] is DynamicResult[]))
                    {
                        scope[key] = result[key];
                    }
                }

                foreach (ConstConfiguration constant in configuration.Constants)
                {
                    scope[constant.Name] = constant.ConstType == ConstType.SingleExpression
                        ? ExpressionParser.Parse(constant.ValueExpression, scope, a => { DefaultCallbacks.PrepareDefaultCallbacks(a.Scope, a.ReplSession); })
                        : ExpressionParser.ParseBlock(constant.ValueExpression, scope, a => { DefaultCallbacks.PrepareDefaultCallbacks(a.Scope, a.ReplSession); });
                }

                foreach (DumpFormatFile file in configuration.Files)
                {
                    retVal = true;
                    writer.Write(scope.FormatText(GetFileText(file)));
                    foreach (DumpConfiguration child in file.Children)
                    {
                        scope.OpenInnerScope();
                        try
                        {
                            DumpSection(writer, result[child.Source] as DynamicResult[], child, scope);
                        }
                        finally
                        {
                            scope.CollapseScope();
                        }
                    }
                }
            }

            return retVal;
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets the fileText of a specific dumprule file
        /// </summary>
        /// <param name="fileSettings">the dumprule file for which to get the configuration</param>
        /// <returns>the filecontent of the provided file</returns>
        private string GetFileText(DumpFormatFile fileSettings)
        {
            DumpRuleFile file;
            file = new DumpRuleFile(fileSettings);
            return file.FileText;
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
