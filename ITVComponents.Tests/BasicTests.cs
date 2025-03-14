using System.IO;
using System.Text;
using System.Text.Json.Serialization;
using ITVComponents.Json;
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
            var s = JsonHelper.ToJson(fu, SerializationTypingMode.StaticTyping, null);
            var fu2 = JsonHelper.FromJsonString<Fubar>(s, SerializationTypingMode.StaticTyping);
            Assert.AreEqual(fu.Fu, fu2.Fu);
            s = JsonHelper.ToJson(fu, SerializationTypingMode.AssistedPolymorphism, null);
            fu2 = JsonHelper.FromJsonString<Fubar>(s, SerializationTypingMode.AssistedPolymorphism);
            Assert.AreEqual(fu.Fu, fu2.Fu);
            fu.Fu = "encrypt:Bar";
            string p = null;
            s = fu.EncryptJsonValues(p);
            fu2 = JsonHelper.FromJsonString<Fubar>(s, SerializationTypingMode.StaticTyping);
            Assert.AreEqual(fu.Fu, $"encrypt:{fu2.Fu.Decrypt()}");
        }

        [TestMethod]
        public void TestCustomJson()
        {
            var fu = new Fubar { Fu = "Bar" };
            using var mst = new MemoryStream();
            JsonHelper.WriteObject(fu, SerializationTypingMode.StaticTyping, mst);

            var arr = mst.ToArray();
            var tx = Encoding.UTF8.GetString(arr);
            //Assert.IsFalse(tx.Contains("\r\n"));
            Assert.IsTrue(tx.Contains("\n"));
        }

        [TestMethod]
        public void TestNativePolyJson()
        {
            JsonHelper.ExtendNativeProtocolType<IProto,ProtoImplB>("ProtoB");
            var protA = new ProtoImplA { Fubar = "Bier" };
            var protB = new ProtoImplB { Fibor = "Boar" };

            var strA = JsonHelper.ToJson<IProto>(protA, SerializationTypingMode.NativePolymorphism);
            var strB = JsonHelper.ToJson<IProto>(protB, SerializationTypingMode.NativePolymorphism);

            var outA = JsonHelper.FromJsonString<IProto>(strA, SerializationTypingMode.NativePolymorphism);
            var outB = JsonHelper.FromJsonString<IProto>(strB, SerializationTypingMode.NativePolymorphism);

            Assert.IsTrue(outA is ProtoImplA{Fubar:"Bier"});
            Assert.IsTrue(outB is ProtoImplB{Fibor:"Boar"});
        }


        public class Fubar
        {
            public string Fu { get; set; }
        }

        [JsonPolymorphic]
        [JsonDerivedType(typeof(ProtoImplA), "ProtoA")]
        public interface IProto
        {
        }

        public class ProtoImplA : IProto
        {
            public string Fubar { get; set; }
        }

        public class ProtoImplB : IProto
        {
            public string Fibor { get; set; }
        }
    }
}
