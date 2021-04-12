using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITVComponents.DataExchange.KeyValueImport.Config;
using ITVComponents.DataExchange.KeyValueImport.Data;
using ITVComponents.DataExchange.KeyValueImport.TextSource.Escaping;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Helpers;

namespace ITVComponents.DataExchange.KeyValueImport.TextSource
{
    internal class CsvParser
    {
        private readonly string fileName;
        private readonly CsvImportConfiguration importConfiguration;
        private Regex fieldRegex;

        public CsvParser(string fileName, CsvImportConfiguration importConfiguration)
        {
            this.fileName = fileName;
            this.importConfiguration = importConfiguration;
            BuildRecognition();
        }

        internal IEnumerable<IDictionary<string, CsvDataRecord>> ReadData()
        {
            int line = 0;
            string fn = Path.GetFileNameWithoutExtension(fileName);
            using (var reader = new StreamReader(fileName, Encoding.GetEncoding(importConfiguration.Encoding)))
            {
                yield return new Dictionary<string, CsvDataRecord>
                {
                    {
                        "FileName",
                        new CsvDataRecord
                        {
                            RawText = fn,
                            Converted = fn
                        }
                    }
                };
                string s;
                while ((s = reader.ReadLine()) != null)
                {
                    line++;
                    string recordAddr = $"{fn}::{line}";
                    Dictionary<string, CsvDataRecord> row = new Dictionary<string, CsvDataRecord>();
                    row.Add("$origin", new CsvDataRecord {Converted = recordAddr, RawText = recordAddr});
                    ParseLine(s, row);
                    yield return row;
                }
            }
        }

        /// <summary>
        /// Parses a single line
        /// </summary>
        /// <param name="s">the string that was read from the csv-file</param>
        /// <param name="row">the current row</param>
        private void ParseLine(string s, IDictionary<string, CsvDataRecord> row)
        {
            var matches = fieldRegex.Matches(s);
            for (int i = 0; i < matches.Count; i++)
            {
                var name = $"col{i}";
                CsvDataRecord currentField = new CsvDataRecord();
                var rawValue = matches[i].Groups["value"].Value;
                if (!string.IsNullOrEmpty(importConfiguration.EscapeStrategyName))
                {
                    rawValue = EscapeStrategy.UnescapeString(importConfiguration.EscapeStrategyName, rawValue);
                }

                currentField.RawText = rawValue;
                TryParseRaw(currentField);
                row.Add(name, currentField);
            }
        }

        /// <summary>
        /// Parses a value when it matches a specified pattern
        /// </summary>
        /// <param name="currentField">the current field that was read from a csv</param>
        private void TryParseRaw(CsvDataRecord currentField)
        {
            try
            {
                if (!string.IsNullOrEmpty(currentField.RawText))
                {
                    var conversion = importConfiguration.TypeConversions.FirstOrDefault(n => Regex.IsMatch(currentField.RawText, n.RawValuePattern, RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline));
                    if (conversion != null)
                    {
                        currentField.Converted = ExpressionParser.Parse(conversion.ParseExpression, currentField, i => DefaultCallbacks.PrepareDefaultCallbacks(i.Scope, i.ReplSession));
                    }
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Unable to parse the value {currentField.RawText}. {ex.Message}", LogSeverity.Warning);
            }
        }

        /// <summary>
        /// Builds the recognition regex for this csvSource
        /// </summary>
        private void BuildRecognition()
        {
            string rx = importConfiguration.ValuesWrapped?
                $"{Regex.Escape(importConfiguration.CsvSeparator)}?{Regex.Escape(importConfiguration.ValueStartCharacter)}(?<value>({importConfiguration.ValueWrapperEscapes}|[^{string.Concat(new[] {Regex.Escape(importConfiguration.ValueStartCharacter), Regex.Escape(importConfiguration.ValueEndCharacter)}.Distinct())}])*){Regex.Escape(importConfiguration.ValueEndCharacter)}":
                $"{Regex.Escape(importConfiguration.CsvSeparator)}?(?<value>({importConfiguration.ValueWrapperEscapes}|[^{Regex.Escape(importConfiguration.CsvSeparator)}])*)";
            fieldRegex = new Regex(rx,RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
        }
    }
}
