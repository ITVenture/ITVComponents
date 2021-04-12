using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using ITVComponents.DataAccess;
using ITVComponents.DataExchange.Import;
using ITVComponents.DataExchange.KeyValueTableImport;
using ITVComponents.DataExchange.TextImport.Config;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Helpers;


namespace ITVComponents.DataExchange.TextImport.Consumers
{
    public class RegexTextConsumer:TextConsumerBase
    {
        /// <summary>
        /// The Consumer configuration for this consumer
        /// </summary>
        private RegexTextConsumerConfiguration config;

        /// <summary>
        /// Holds a list of initialized regexes that can be used to parse text fragments
        /// </summary>
        private List<RegexWrapper> regexes = new List<RegexWrapper>();

        /// <summary>
        /// indicates whether to ignore items that would result to a warning. When this value is true, items that lead to warnings are not added to the parsed results
        /// </summary>
        private bool ignoreWarnedResults = false;

        /// <summary>
        /// Initializes a new instance of the RegexTextConsumer class
        /// </summary>
        /// <param name="config">the configuration that is used to run this consumer</param>
        /// <param name="source">the TextSource that provides one or multiple lines of text for parsing</param>
        /// <param name="ignoreWarnedResults">indicates whether to ignore items that would result to a warning. When this value is true, items that lead to warnings are not added to the parsed results</param>
        public RegexTextConsumer(RegexTextConsumerConfiguration config, ITextSource source, bool ignoreWarnedResults):this(config,source)
        {
            this.ignoreWarnedResults = ignoreWarnedResults;
        }

        /// <summary>
        /// Initializes a new instance of the RegexTextConsumer class
        /// </summary>
        /// <param name="config">the configuration that is used to run this consumer</param>
        /// <param name="source">the TextSource that provides one or multiple lines of text for parsing</param>
        public RegexTextConsumer(RegexTextConsumerConfiguration config, ITextSource source):base(source, config.RequiredLines, config.VirtualColumns)
        {
            this.config = config;
            regexes.AddRange(from t in config.Regexes
                select
                    new RegexWrapper
                    {
                        Regex = new Regex(t.Expression, t.Options),
                        End = t.EndsCurrentItem,
                        Start = t.StartsNewItem
                    });
        }

        /// <summary>
        /// Consumes the provided text and returns the initial character position of recognition
        /// </summary>
        /// <param name="text">the text that was extracted from a TextSource</param>
        /// <param name="logCallback">a callback that can be used to log events on the parser-process</param>
        /// <param name="getNotification">a callback that will provide an acceptance parameter for the current parsing-unit</param>
        /// <returns>a value indicating whether the data was successfully processed</returns>
        protected override bool Consume(string text, LogParserEventCallback<string> logCallback, out Func<DynamicResult, TextAcceptanceCallbackParameter> getNotification)
        {
            getNotification = null;
            int acceptedLength = -1;
            int newLineAddition = 0;
            if (text.EndsWith(Environment.NewLine))
            {
                newLineAddition = Environment.NewLine.Length;
                text = text.Substring(0, text.Length - newLineAddition);
            }
            
            Dictionary<string, object> variables = new Dictionary<string, object>();
            foreach (RegexWrapper cfg in regexes)
            {
                Match m = cfg.Regex.Match(text);
                if (m.Success)
                {
                    if (cfg.Start)
                    {
                        if (HasOpenValues)
                        {
                            LogEnvironment.LogDebugEvent(null, "Entity may be incomplete! Please check Configuration (Maybe an inappropriate Starter-Regex is configured)", (int) LogSeverity.Warning, null);
                            if (!ignoreWarnedResults)
                            {
                                AddCurrentRecord();
                            }
                            else
                            {
                                DismissCurrentRecord(text);
                            }
                        }
                    }

                    acceptedLength = m.Length + newLineAddition;
                    foreach (string groupName in cfg.Regex.GetGroupNames())
                    {
                        ColumnConfiguration col = config.Columns[groupName];
                        if (col != null)
                        {
                            string val = m.Groups[groupName].Value;
                            object dbValue = val;
                            if (!string.IsNullOrEmpty(col.ConvertExpression))
                            {
                                variables.Clear();
                                variables.Add("value", val);
                                dbValue = ExpressionParser.Parse(col.ConvertExpression, variables, a => { DefaultCallbacks.PrepareDefaultCallbacks(a.Scope, a.ReplSession); });
                            }

                            SetValueOfColumn(col.TableColumn, dbValue);
                        }
                    }

                    bool ok = m.Index == 0 && acceptedLength >= text.Length;
                    if (cfg.End)
                    {
                        if (!HasOpenValues)
                        {
                            LogEnvironment.LogDebugEvent(null, "Preventing empty record from being registered! Please check Configuration.", (int) LogSeverity.Warning, null);
                            //callback(new TextAcceptanceCallbackParameter(false, 0, 0, "", null));
                            return false;
                        }

                        if (!ignoreWarnedResults || ok)
                        {
                            AddCurrentRecord();
                        }
                        else
                        {
                            DismissCurrentRecord(text);
                        }
                    }

                    getNotification =
                        t => new TextAcceptanceCallbackParameter(true, m.Index, acceptedLength, config.Name, t, text);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Holds a regex with additional information about starting new entities or ending current
        /// </summary>
        private class RegexWrapper
        {
            /// <summary>
            /// The Regex that is used to parse data
            /// </summary>
            public Regex Regex { get; set; }

            /// <summary>
            /// Indicates whether this regex marks the beginning of a new record
            /// </summary>
            public bool Start { get; set; }

            /// <summary>
            /// Indicates whether this regex marks the ending of the current record
            /// </summary>
            public bool End { get; set; }
        }
    }
}