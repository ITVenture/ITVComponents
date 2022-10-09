using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.IO
{   public static class StreamExtensions
    {
        /// <summary>
        /// Reads all Bytes from a stream
        /// </summary>
        /// <param name="s">the stream to read from</param>
        /// <returns>a byte array containing all bytes of the stream</returns>
        public static byte[] ReadAllBytes(this Stream s)
        {
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }

    }
}
