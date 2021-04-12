using ITVComponents.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Tests
{
    [TestClass]
    public class EncryptionTest
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
    }
}
