//-----------------------------------------------------------------------
// <copyright file="LogWriter.cs" company="IT-Venture GmbH">
//     2009 by IT-Venture GmbH
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Text;
using System.IO;
using ITVComponents.Plugins;

namespace ITVComponents.Logging.DefaultLoggers.ConsoleRedirection
{
    /// <summary>
    /// Writes Logs to the appropriate file
    /// </summary>
    public class ConsoleListener : TextWriter, IPlugin
    {
        /// <summary>
        /// the buffer used to write into the file
        /// </summary>
        private byte[] buffer = new byte[10000];

        /// <summary>
        /// The encoding used to translate the text into byte data
        /// </summary>
        private Encoding encoding;

        /// <summary>
        /// the buffer position that is written next
        /// </summary>
        private int bufferPos = 0;

        /// <summary>
        /// indicates whether the file should auto-flush its output into the targetfile
        /// </summary>
        private bool autoFlush;

        /// <summary>
        /// the decorated writer of this textwriter
        /// </summary>
        private TextWriter decoratedWriter;

        /// <summary>
        /// an object that is used to sync logging methods
        /// </summary>
        private object innerSync;

        /// <summary>
        /// the severity of console events
        /// </summary>
        private int severity;

        /// <summary>
        /// Initializes a new instance of the LogWriter class
        /// </summary>
        /// <param name="severity">the severity of console events</param>
        public ConsoleListener(int severity)
            : base()
        {
            this.severity = severity;
            this.autoFlush= true;
            encoding = System.Text.Encoding.Default;
            innerSync = new object();
            this.decoratedWriter = System.Console.Out;
            System.Console.SetOut(this);
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets the encoding of the inner writer
        /// </summary>
        public override Encoding Encoding
        {
            get { return this.encoding; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the stream has to be automatically flushed after a linefeed
        /// </summary>
        public bool AutoFlush
        {
            get { return this.autoFlush; }
            set { this.autoFlush = value; }
        }

        /// <summary>
        /// Writes a single character into the file
        /// </summary>
        /// <param name="value">the value that is to be written into the file</param>
        public override void Write(char value)
        {
            lock(innerSync)
            {
                if (decoratedWriter != null)
                {
                    decoratedWriter.Write(value);

                }

                string val = value.ToString();
                int len = encoding.GetBytes(val, 0, val.Length, buffer, bufferPos);
                bufferPos += len;
                if (autoFlush || bufferPos >= buffer.Length - 10)
                {
                    Flush();
                }
            }
        }

        /// <summary>
        /// Writes all buffered bytes into the base stream
        /// </summary>
        public override void Flush()
        {
            string line = null;
            while ((line = FindNextLine()) != null)
            {
                LogEnvironment.LogEvent(line, severity);
            }
        }

        /// <summary>
        /// Raises the disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            EventHandler handler = Disposed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>
        /// Extracts the next line from the stream-buffer
        /// </summary>
        /// <returns>a byte array representing the next log line including a timestamp</returns>
        private string FindNextLine()
        {
            string retVal = null;
            byte[] newLine = Encoding.GetBytes(NewLine);
            bool done = false;
            for (int i = 0; i < bufferPos; i++)
            {
                bool ok = true;
                for (int a = 0; a < newLine.Length && ok; a++)
                {
                    ok &= (buffer[i + a] == newLine[a]);
                }

                if (ok && bufferPos >= i+newLine.Length)
                {
                    //byte[] timeStamp = encoding.GetBytes(string.Format("{0:dd.MM.yyyy HH:mm:ss} -> ",DateTime.Now));
                    byte[] tmp = new byte[i];
                    Array.Copy(buffer, 0, tmp, 0, i);
                    Array.Copy(buffer, i + newLine.Length, buffer, 0, bufferPos - (i + newLine.Length));
                    bufferPos -= (i + newLine.Length);
                    retVal = encoding.GetString(tmp);
                    break;
                }
            }

            return retVal;
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}