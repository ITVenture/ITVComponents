using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.BlobStore.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.BlobStore.Test
{
    [TestClass]
    public class BlobTest
    {
        [TestMethod]
        public void GeneralTest()
        {
            DataBlob b = new DataBlob(@"c:\temp\fubaaar.blob", false);
            var files = Directory.GetFiles(@"C:\serviceroot", "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var f = Path.GetRelativePath(@"C:\serviceroot", file);
                var hash = FileHasher.ComputeHash(file);
                var fn = Path.GetFileName(f);
                var tmpOrig = b.FilterFiles(n =>
                    Path.GetFileName(n.Name).Equals(fn, StringComparison.OrdinalIgnoreCase) &&
                    n.Hash != null &&
                    hash.SequenceEqual(n.Hash)).FirstOrDefault();
                if (tmpOrig != null)
                {
                    b.LinkFile(tmpOrig, f);
                }
                else
                {
                    var tmp = b.NewFile(f);
                    using (var s = b.OpenFile(tmp, OpenFileMode.Write))
                    {
                        using (var r = File.OpenRead(file))
                        {
                            r.CopyTo(s);
                        }

                        s.Flush();
                    }
                }
            }

            b.Dispose();
            b = new DataBlob(@"c:\temp\fubaaar.blob");
            var outTest = @"c:\temp\lötescht999";
            foreach (var file in b.Files)
            {
                var finalName = Path.Combine(outTest, file.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(finalName));
                using (var s = b.OpenFile(file, OpenFileMode.Read))
                {
                    using (var r = File.OpenWrite(finalName))
                    {
                        s.CopyTo(r);
                        //s.CopyTo(r);
                    }
                }
            }

            b.Dispose();
        }
    }
}
