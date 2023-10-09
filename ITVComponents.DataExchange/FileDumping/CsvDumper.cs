using System;
using System.Collections.Generic;
using System.IO;
//using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dynamitey.DynamicObjects;
using ITVComponents.DataAccess;
using ITVComponents.DataExchange.Configuration;
using ITVComponents.DataExchange.Configuration.Csv;
using ITVComponents.DataExchange.Interfaces;
using ITVComponents.Formatting.ScriptExtensions;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Helpers;

namespace ITVComponents.DataExchange.FileDumping
{
    public class CsvDumper:IDataDumper
    {
        static CsvDumper()
        {
            var dummy = new ScriptFormatInitializer();
        }

        /// <summary>
        /// Dumps collected data into the given file
        /// </summary>
        /// <param name="fileName">the name of the target filename for this dump-run</param>
        /// <param name="data">the data that must be dumped</param>
        /// <param name="configuration">the dumper-configuiration</param>
        /// <returns>a value indicating whether there was any data available for dumping</returns>
        public bool DumpData(string fileName, DynamicResult[] data, DumpConfiguration configuration)
        {
            using (FileStream fst = File.Create(fileName))
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
            bool retVal = true;
            if (configuration.Files.Count > 1)
            {
                LogEnvironment.LogDebugEvent(null, "The Dump-Configuration contains more than one file. All settings but the first one are being ignored.", (int)LogSeverity.Warning, null);
            }

            CsvSettings settings = null;
            if (configuration.Files.Count != 0)
            {
                settings = new DumpRuleFile(configuration.Files[0]).ParseJsonFile<CsvSettings>();
            }

            if (settings == null)
            {
                settings = new CsvSettings();
            }

            bool first = true;
            try
            {
                using (StreamWriter writer = new StreamWriter(outputStream, Encoding.GetEncoding(settings.Encoding)))
                {
                    List<string> fields = new List<string>();
                    string format = "";
                    var scope = new Scope();
                    scope["$$Format"] = new Func<string, string>(s =>
                    {
                        var rv = s;
                        foreach (var es in settings.EscapeSettings)
                        {
                            rv = Regex.Replace(rv, es.MatchRegex, es.RegexReplaceExpression, RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
                        }

                        return rv;
                    });
                    using (var scriptingContext = ExpressionParser.BeginRepl(scope, i => DefaultCallbacks.PrepareDefaultCallbacks(i.Scope, i.ReplSession)))
                    {
                        foreach (DynamicResult result in data)
                        {
                            if (first)
                            {
                                first = false;
                                fields.AddRange(result.Keys);
                                fields.AddRange(from t in configuration.Constants select t.Name);
                                Dictionary<string, object> firstRow = new Dictionary<string, object>();
                                fields.ForEach(n => firstRow.Add(n, n));
                                var tmpFields = (from t in fields
                                    join c in settings.Formattings on t equals c.ColumnName into cj
                                    from cn in cj.DefaultIfEmpty()
                                    join v in configuration.Constants on t equals v.Name into vj
                                    from vn in vj.DefaultIfEmpty()
                                    select new {ColumnName = t, CustomFormat = cn, ConstDefinition = vn}).ToArray();
                                format = string.Join(settings.CsvSeparator, from t in tmpFields select $"{settings.ValueStartCharacter}{GetColumnDefinition(t.ColumnName, t.CustomFormat, t.ConstDefinition, false)}{settings.ValueEndCharacter}");
                                var headFormat = string.Join(settings.CsvSeparator, from t in tmpFields select $"{settings.ValueStartCharacter}{GetColumnDefinition(t.ColumnName, t.CustomFormat, t.ConstDefinition, true)}{settings.ValueEndCharacter}");
                                scope["$$fmt"] = format;
                                scope["$$hdr"] = headFormat;
                                scope["$data"] = firstRow;
                                scope["$$RowNum"] = -1;
                                foreach (var cst in configuration.Constants.Where(n => n.ConstType == ConstType.ExpressionBlock))
                                {
                                    ExpressionParser.ParseBlock($"fx_{cst.Name}={cst.ValueExpression}", scriptingContext);
                                    ExpressionParser.ParseBlock($"fx_{cst.Name}.ParentScope=Scope()", scriptingContext);
                                }

                                if (settings.TableHeader)
                                {
                                    writer.WriteLine(ExpressionParser.Parse("$$($$hdr)", scriptingContext));
                                }
                            }

                            ExpressionParser.Parse("$$RowNum++", scriptingContext);
                            scope["$data"] = result;
                            foreach (var cst in configuration.Constants)
                            {
                                if (cst.ConstType == ConstType.SingleExpression)
                                {
                                    ExpressionParser.Parse($"{cst.Name}={cst.ValueExpression}", scriptingContext);
                                }
                                else
                                {
                                    ExpressionParser.Parse($"{cst.Name}=fx_{cst.Name}()", scriptingContext);
                                }
                            }

                            writer.WriteLine(ExpressionParser.Parse("$$($$fmt)", scriptingContext));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Error: {ex.OutlineException()}", LogSeverity.Error);
                retVal = false;
            }

            return retVal;
        }

        private string GetColumnDefinition(string columnName, CsvColumnFormat customFormat, ConstConfiguration constDefinition, bool header)
        {
            StringBuilder retVal = new StringBuilder();
            if (constDefinition == null || header)
            {
                retVal.Append("$data.");
            }

            retVal.Append(columnName);
            if (customFormat != null && !header)
            {
                retVal.Append(customFormat.Format);
            }

            return $"[$$Format($$(\"[{retVal}]\"))]";
        }
    }
}
