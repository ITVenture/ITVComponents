using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Helpers
{
    public static class StreamExtensions
    {
        /// <summary>
        /// Copies the content of a stream to an array
        /// </summary>
        /// <param name="stream">the target stream</param>
        /// <returns>the stream-content from the current position as byte array</returns>
        public static byte[] ToArray(this Stream stream)
        {
            MemoryStream mst;
            using (mst = new MemoryStream())
            {
                stream.CopyTo(mst);
            }

            return mst.ToArray();
        }

        /// <summary>
        /// Copies the content of a stream to an array
        /// </summary>
        /// <param name="stream">the target stream</param>
        /// <returns>the stream-content from the current position as byte array</returns>
        public static async Task<byte[]> ToArrayAsync(this Stream stream)
        {
            MemoryStream mst;
            using (mst = new MemoryStream())
            {
                await stream.CopyToAsync(mst);
            }

            return mst.ToArray();
        }
    }
}
