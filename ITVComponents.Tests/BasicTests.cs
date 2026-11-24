using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;
using ITVComponents.Json;
using ITVComponents.Json.Converters;
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
            s = JsonHelper.ToJson(fu, SerializationTypingMode.EnvelopePolymorphism, null);
            fu2 = JsonHelper.FromJsonString<Fubar>(s, SerializationTypingMode.EnvelopePolymorphism);
            Assert.AreEqual(fu.Fu, fu2.Fu);
            fu.Fu = "encrypt:Bar";
            string p = null;
            s = fu.EncryptJsonValues(p);
            fu2 = JsonHelper.FromJsonString<Fubar>(s, SerializationTypingMode.StaticTyping);
            Assert.AreEqual(fu.Fu, $"encrypt:{fu2.Fu.Decrypt()}");
        }

        /// <summary>
        /// Generische Listen und Arrays als eingehuellte Wurzel: sie kommen in ihrer genauen Form zurueck.
        /// </summary>
        [TestMethod]
        public void TestEnvelopePolymorphismWithCollections()
        {
            var list = new List<Fubar> { new Fubar { Fu = "one" }, new Fubar { Fu = "two" } };
            var listJson = JsonHelper.ToJson(list, SerializationTypingMode.EnvelopePolymorphism, null);
            var list2 = JsonHelper.FromJsonString<List<Fubar>>(listJson, SerializationTypingMode.EnvelopePolymorphism);
            Assert.IsNotNull(list2);
            Assert.AreEqual(2, list2.Count);
            Assert.AreEqual("one", list2[0].Fu);

                        var array = new[] { new Fubar { Fu = "a" }, new Fubar { Fu = "b" } };
            var arrayJson = JsonHelper.ToJson(array, SerializationTypingMode.EnvelopePolymorphism, null);
            var array2 = JsonHelper.FromJsonString<Fubar[]>(arrayJson, SerializationTypingMode.EnvelopePolymorphism);
            Assert.AreEqual(2, array2.Length);
            Assert.AreEqual("b", array2[1].Fu);
        }

        /// <summary>
        /// Registrierter Kurzname statt AssemblyQualifiedName: das Format haengt dann nicht mehr an
        /// Assembly und Version - der Sinn der Uebung fuer langlebig abgelegte Daten.
        /// </summary>
        [TestMethod]
        public void TestEnvelopePolymorphismWithRegisteredAlias()
        {
            JsonHelper.RegisterManualType<Aliased>("test-aliased");
            var value = new Aliased { Name = "x", Count = 3 };

            var json = JsonHelper.ToJson(value, SerializationTypingMode.EnvelopePolymorphism, null);
            Assert.IsTrue(json.Contains("test-aliased"), "the short name must be what lands in the payload.");
            Assert.IsFalse(json.Contains("ITVComponents.Tests, Version"),
                "no assembly-qualified name - that is the whole point.");

            var back = JsonHelper.FromJsonString<Aliased>(json, SerializationTypingMode.EnvelopePolymorphism);
            Assert.AreEqual("x", back.Name);
            Assert.AreEqual(3, back.Count);
        }

        /// <summary>
        /// Unter der Beschraenkung wird ein nicht angemeldeter Typname NICHT geladen - das Lesen in einen
        /// konkreten Typ scheitert dann laut, statt ein leeres oder fremdes Objekt zu liefern.
        /// </summary>
        [TestMethod]
        public void TestRestrictedManualTypesRefuseArbitraryTypes()
        {
            // Ohne Registrierung geschrieben => Typname im Datenstrom.
            var json = JsonHelper.ToJson(new Fubar { Fu = "Bar" }, SerializationTypingMode.EnvelopePolymorphism, null);
            Assert.IsTrue(json.Contains("ITVComponents.Tests"));

            using (JsonHelper.RestrictManualTypesToRegistered())
            {
                Assert.ThrowsExactly<System.Text.Json.JsonException>(
                    () => JsonHelper.FromJsonString<Fubar>(json, SerializationTypingMode.EnvelopePolymorphism),
                    "an unregistered type must not be loaded under the restriction.");
            }

            // Ohne Beschraenkung laeuft derselbe Datenstrom - die Prozesskommunikation haengt daran.
            var allowed = JsonHelper.FromJsonString<Fubar>(json, SerializationTypingMode.EnvelopePolymorphism);
            Assert.AreEqual("Bar", allowed.Fu);
        }

        [TestMethod]
        public void TestEncryptJsonValuesFromString()
        {
            PasswordSecurity.InitializeAes("So long and thanks for all the fish");
            var json = "{\"Fu\":\"encrypt:Bar\",\"Other\":\"plain\",\"Nested\":{\"Secret\":\"encrypt:Baz\"}}";
            var encrypted = json.EncryptJsonValues();

            // The encrypt: markers must be gone — the values were actually encrypted, not passed through.
            Assert.IsFalse(encrypted.Contains("encrypt:Bar"));
            Assert.IsFalse(encrypted.Contains("encrypt:Baz"));

            var node = System.Text.Json.Nodes.JsonNode.Parse(encrypted);
            Assert.AreEqual("Bar", node["Fu"].GetValue<string>().Decrypt());
            Assert.AreEqual("plain", node["Other"].GetValue<string>());
            Assert.AreEqual("Baz", node["Nested"]["Secret"].GetValue<string>().Decrypt());
        }

        [TestMethod]
        public void TestEncryptJsonValueAttribute()
        {
            PasswordSecurity.InitializeAes("So long and thanks for all the fish");
            var bag = new SecretBag { Secret = "encrypt:hunter2", Plain = "encrypt:nope" };
            var json = JsonHelper.ToJson(bag, SerializationTypingMode.StaticTyping, null);

            // [EncryptJsonValue] member is encrypted (marker gone); unmarked member is passed through.
            Assert.IsFalse(json.Contains("encrypt:hunter2"));
            Assert.IsTrue(json.Contains("encrypt:nope"));

            var node = System.Text.Json.Nodes.JsonNode.Parse(json);
            Assert.AreEqual("hunter2", node["Secret"].GetValue<string>().Decrypt());
            Assert.AreEqual("encrypt:nope", node["Plain"].GetValue<string>());
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

        /// <summary>Ein Datensatz, der unter einem stabilen Kurznamen angemeldet wird.</summary>
        public class Aliased
        {
            public string Name { get; set; }

            public int Count { get; set; }
        }

        public class SecretBag
        {
            [EncryptJsonValue]
            public string Secret { get; set; }

            public string Plain { get; set; }
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
