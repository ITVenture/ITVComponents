using ITVComponents.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Test
{
    /// <summary>
    /// Settings that must hold before the first test of this assembly.
    /// </summary>
    [TestClass]
    public static class TestAssemblySetup
    {
        /// <summary>
        /// AES for every test. <see cref="PasswordSecurity"/> can be initialized only once: the first test that encrypts
        /// without setup fixes it to DPAPI, and a later AES test (server cookie placeholders) fails depending on the order.
        /// The tests encrypt and decrypt themselves, so the method does not matter for them - only that it is the same.
        /// </summary>
        [AssemblyInitialize]
        public static void Initialize(TestContext context)
        {
            PasswordSecurity.InitializeAes("tenant security tests");
        }
    }
}
