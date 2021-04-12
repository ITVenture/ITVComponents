using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using ITVComponents.DataExchange.Import;
using ITVComponents.Logging;

namespace ITVComponents.DataExchange.TextImport
{
    public abstract class TextSourceBase:ImportSourceBase<string, TextAcceptanceCallbackParameter>, ITextSource
    {
        /// <summary>
        /// Holds local string-builders of this instance
        /// </summary>
        [NonSerialized]
        private ThreadLocal<StringBuilder> builderLocal = new ThreadLocal<StringBuilder>();

        /// <summary>
        /// Reads consumption-compilant portions of Data from the underlaying data-source
        /// </summary>
        /// <returns>an IEnumerable of the base-set</returns>
        protected override IEnumerable<string> ReadData()
        {
            StringBuilder bld = new StringBuilder();
            builderLocal.Value = bld;
            try
            {
                foreach (string s in GetLines())
                {
                    bld.AppendLine(s);
                    string tmp = bld.ToString();
                    if (FragmentProcessable(tmp))
                    {
                        yield return tmp;
                    }
                }
            }
            finally
            {
                bld.Clear();
                builderLocal.Value = null;
            }
        }

        /// <summary>
        /// Processes the importer response
        /// </summary>
        /// <param name="notification">the response of the importer that has accepted the input</param>
        protected override void ProcessResult(TextAcceptanceCallbackParameter notification)
        {
            if (notification.Success)
            {
                StringBuilder bld = builderLocal.Value;
                if (notification.FirstAcceptedCharacter != 0 || notification.AcceptedLength < bld.Length)
                {
                    string text = string.Format(
                        "Possibly incomplete parse! Start-Symbol found at {0}, Length: {1}. Expected Length was: {2}",
                        notification.FirstAcceptedCharacter, notification.AcceptedLength, bld.Length);
                    LogEnvironment.LogDebugEvent(null,
                        text,
                        (int) LogSeverity.Warning, null);
                    LogParserEvent(notification.SourceData, text, ParserEventSeverity.Warning, notification.Data);
                }

                bld.Clear();
            }
        }

        /// <summary>
        /// Gets all lines from the underlaying text source
        /// </summary>
        /// <returns>an IEnumerable string source</returns>
        protected abstract IEnumerable<string> GetLines();

        /// <summary>
        /// Offers the possibility to perform Tasks after the deserialization of this instance
        /// </summary>
        /// <param name="context">the streaming-context</param>
        protected override void Deserialized(StreamingContext context)
        {
            base.Deserialized(context);
            builderLocal = new ThreadLocal<StringBuilder>();
        }
    }
}
