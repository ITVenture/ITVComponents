using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.DataAccess.Helpers
{
    public static class HashHelper
    {
        private static ConcurrentDictionary<string, HashAlgorithm> algos = new ConcurrentDictionary<string, HashAlgorithm>();
        public static string CalculateHash(string s, string hashAlgorithm)
        {
            var algo = algos.GetOrAdd(hashAlgorithm, a => HashAlgorithm.Create(a) ?? new SHA1CryptoServiceProvider());
            byte[] bytes = Encoding.Unicode.GetBytes(s);
            byte[] hash = algo.ComputeHash(bytes);
            string hashString = string.Empty;
            foreach (byte x in hash)
            {
                hashString += String.Format("{0:x2}", x);
            }

            return hashString;
        }
    }
}