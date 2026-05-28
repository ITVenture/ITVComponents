using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;

namespace ITVComponents.DataExchange.TextImport
{
    public class DefaultTextSource:TextSourceBase
    {
        /// <summary>
        /// the filename that is being used to consume the target file
        /// </summary>
        private string fileName;

        /// <summary>
        /// The preferred encoding for the file-content
        /// </summary>
        private Encoding textEncoding;

        /// <summary>
        /// Initializes a new instance of the DefaultTextSource class
        /// </summary>
        /// <param name="fileName">the fileName that contains data that can be processed by the attached textconsumers</param>
        /// <param name="textEncoding">the preferred encoding for the file-content</param>
        public DefaultTextSource(string fileName, Encoding textEncoding)
        {
            this.fileName = fileName;
            this.textEncoding = textEncoding;
        }

        /// <summary>
        /// Initializes a new instance of the DefaultTextSource class
        /// </summary>
        /// <param name="fileName">the fileName that contains data that can be processed by the attached textconsumers</param>
        public DefaultTextSource(string fileName) : this(fileName, Encoding.Default)
        {
        }

        #region Overrides of ImportSourceBase<string,TextAcceptanceCallbackParameter,IAcceptanceConstraint<string>>

        /// <summary>
        /// Notifies an inherited class that no consumer was able to process a specific portion of data that is provided by this DataSource
        /// </summary>
        /// <param name="data">the portion of Data that is not being recognized</param>
        protected override void NoMatchFor(string data)
        {
        }

        #endregion

        /// <summary>
        /// Gets all lines from the underlaying text source
        /// </summary>
        /// <returns>an IEnumerable string source</returns>
        protected override IEnumerable<string> GetLines()
        {
            using (StreamReader reader = new StreamReader(fileName, textEncoding))
            {
                string s;
                while ((s = reader.ReadLine()) != null)
                {
                    yield return s;
                }
            }
        }
    }
}
