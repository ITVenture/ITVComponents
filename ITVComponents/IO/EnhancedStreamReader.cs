using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;

namespace ITVComponents.IO
{
    public class EnhancedStreamReader:StreamReader
    {
        public EnhancedStreamReader(Stream stream) : base(stream)
        {
        }

        public EnhancedStreamReader(Stream stream, bool detectEncodingFromByteOrderMarks) : base(stream, detectEncodingFromByteOrderMarks)
        {
        }

        public EnhancedStreamReader(Stream stream, Encoding encoding) : base(stream, encoding)
        {
        }

        public EnhancedStreamReader(Stream stream, Encoding encoding, bool detectEncodingFromByteOrderMarks) : base(stream, encoding, detectEncodingFromByteOrderMarks)
        {
        }

        public EnhancedStreamReader(Stream stream, Encoding encoding, bool detectEncodingFromByteOrderMarks, int bufferSize) : base(stream, encoding, detectEncodingFromByteOrderMarks, bufferSize)
        {
        }

        public EnhancedStreamReader(Stream stream, Encoding encoding, bool detectEncodingFromByteOrderMarks, int bufferSize, bool leaveOpen) : base(stream, encoding, detectEncodingFromByteOrderMarks, bufferSize, leaveOpen)
        {
        }

        public EnhancedStreamReader(string path) : base(path)
        {
        }

        public EnhancedStreamReader(string path, bool detectEncodingFromByteOrderMarks) : base(path, detectEncodingFromByteOrderMarks)
        {
        }

        public EnhancedStreamReader(string path, Encoding encoding) : base(path, encoding)
        {
        }

        public EnhancedStreamReader(string path, Encoding encoding, bool detectEncodingFromByteOrderMarks) : base(path, encoding, detectEncodingFromByteOrderMarks)
        {
        }

        public EnhancedStreamReader(string path, Encoding encoding, bool detectEncodingFromByteOrderMarks, int bufferSize) : base(path, encoding, detectEncodingFromByteOrderMarks, bufferSize)
        {
        }

        /// <summary>
        /// Gets or sets a String that is used as explicit Linefeed for this StreamReader
        /// </summary>
        public string NewLine { get; set; }

        /// <summary>Reads a line of characters from the current stream and returns the data as a string.</summary>
        /// <returns>The next line from the input stream, or null if the end of the input stream is reached.</returns>
        /// <exception cref="T:System.OutOfMemoryException">There is insufficient memory to allocate a buffer for the returned string. </exception>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        /// <filterpriority>1</filterpriority>
        public override string ReadLine()
        {
            if (string.IsNullOrEmpty(NewLine))
            {
                return base.ReadLine();
            }

            StringBuilder bld = new StringBuilder();
            bool eof = false;
            while(true)
            {
                int tmp = Read();
                if (tmp == -1)
                {
                    eof = true;
                    break;
                }

                bld.Append((char)tmp);
                if (bld.EndsWith(NewLine))
                {
                    bld.Remove(bld.Length - NewLine.Length,NewLine.Length);
                    break;
                }
            }

            if (eof && bld.Length == 0)
            {
                return null;
            }

            return bld.ToString();
        }
    }
}
