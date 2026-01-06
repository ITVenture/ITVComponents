using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.BlobStore.Helpers
{
    public static class FileHasher
    {
        public static byte[] ComputeHash(string fileName)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                using (var stream = System.IO.File.OpenRead(fileName))
                {
                    var hash = sha256.ComputeHash(stream);
                    return hash;
                }
            }
        }

        public static byte[] ComputeHash(Stream stream)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha256.ComputeHash(stream);
                return hash;
            }
        }
    }
}
