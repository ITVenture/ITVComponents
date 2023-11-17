using System.IO;
using System.Text;
using ITVComponents.Helpers;
using ITVComponents.Security;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Tests
{
    [TestClass]
    public class BasicTests
    {
        [TestMethod]
        public void TestEncryption()
        {
            PasswordSecurity.InitializeAes("So long and thanks for all the fish");
            var initial = "MeinSuperGutesPasswort";
            var encrypted = PasswordSecurity.Encrypt(initial);
            var decrypted = PasswordSecurity.Decrypt(encrypted);
            Assert.AreEqual(initial,decrypted);
        }

        [TestMethod]
        public void TestJson()
        {
            var fu = new Fubar { Fu = "Bar" };
            var s = JsonHelper.ToJson(fu);
            var fu2 = JsonHelper.FromJsonString<Fubar>(s);
            Assert.AreEqual(fu.Fu, fu2.Fu);
            s = JsonHelper.ToJsonStrongTyped(fu);
            fu2 = JsonHelper.FromJsonStringStrongTyped<Fubar>(s);
            Assert.AreEqual(fu.Fu, fu2.Fu);
            fu.Fu = "encrypt:Bar";
            string p = null;
            s = fu.EncryptJsonValues(p);
            fu2 = JsonHelper.FromJsonString<Fubar>(s);
            Assert.AreEqual(fu.Fu, $"encrypt:{fu2.Fu.Decrypt()}");
        }

        [TestMethod]
        public void TestCustomJson()
        {
            var fu = new Fubar { Fu = "Bar" };
            using var mst = new MemoryStream();
            using (TextWriter wr = new StreamWriter(mst,Encoding.UTF8,-1,false) { NewLine = "\n" })
            {
                JsonHelper.WriteObject(fu, wr);
            }

            var arr = mst.ToArray();
            var tx = Encoding.UTF8.GetString(arr);
            Assert.IsFalse(tx.Contains("\r\n"));
            Assert.IsTrue(tx.Contains("\n"));
        }


        public class Fubar
        {
            public string Fu { get; set; }
        }
    }
}
