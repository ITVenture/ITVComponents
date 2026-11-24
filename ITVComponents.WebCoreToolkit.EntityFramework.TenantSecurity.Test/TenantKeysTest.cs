using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ITVComponents.Logging;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tenant = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Tenant;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Test
{
    /// <summary>
    /// Tenant keys protected with the application key (issue Security 01 part C, guide §127).
    /// </summary>
    [TestClass]
    public class TenantKeysTest
    {
        [TestMethod]
        public void A_New_Key_Is_Protected_Random_And_Fits_The_Column()
        {
            var first = TenantKeys.CreateProtected();
            var second = TenantKeys.CreateProtected();

            Assert.IsTrue(TenantKeys.IsProtected(first));
            Assert.AreEqual(32, TenantKeys.KeyBytes(first).Length);
            Assert.AreNotEqual(TenantKeys.Unprotect(first), TenantKeys.Unprotect(second));
            Assert.IsTrue(first.Length <= 512, $"{first.Length} characters");
            Assert.IsFalse(first.Contains(TenantKeys.Unprotect(first)), "the key itself is not in the stored value");
        }

        [TestMethod]
        public void Protect_Is_Idempotent_And_Leaves_Empty_Values()
        {
            var plain = Convert.ToBase64String(AesEncryptor.CreateKey());
            var protectedKey = TenantKeys.Protect(plain);

            Assert.AreEqual(protectedKey, TenantKeys.Protect(protectedKey));
            Assert.AreEqual(plain, TenantKeys.Unprotect(protectedKey));
            Assert.IsNull(TenantKeys.Protect(null));
            Assert.AreEqual("", TenantKeys.Protect(""));
            Assert.IsNull(TenantKeys.KeyBytes(null));
        }

        [TestMethod]
        public void A_Plain_Key_Is_Still_Read_With_One_Warning()
        {
            var plain = Convert.ToBase64String(AesEncryptor.CreateKey());
            var warnings = new List<string>();
            using (LogEnvironment.SetTemporaryLogHook((debug, text, severity, context) =>
                   {
                       if (text.Contains("stored unprotected"))
                       {
                           warnings.Add(text);
                       }
                   }))
            {
                Assert.AreEqual(plain, TenantKeys.Unprotect(plain));
                CollectionAssert.AreEqual(Convert.FromBase64String(plain), TenantKeys.KeyBytes(plain));
            }

            Assert.AreEqual(1, warnings.Count);
        }

        [TestMethod]
        public void The_Start_Protects_Plain_Keys_Once_And_Leaves_The_Others()
        {
            var options = new DbContextOptionsBuilder<ClientAppMachineAccessTest.TestSecurityContext>()
                .UseInMemoryDatabase($"tenant-keys-{Guid.NewGuid():N}").Options;
            var plain = Convert.ToBase64String(AesEncryptor.CreateKey());
            var alreadyProtected = TenantKeys.CreateProtected();
            using (var ctx = ClientAppMachineAccessTest.TestContextFactory.Build(options, null))
            {
                ctx.Tenants.Add(NewTenant("plain", plain));
                ctx.Tenants.Add(NewTenant("protected", alreadyProtected));
                ctx.Tenants.Add(NewTenant("none", null));
                ctx.SaveChanges();
            }

            using (var ctx = ClientAppMachineAccessTest.TestContextFactory.Build(options, null))
            {
                Assert.AreEqual(1, TenantKeyProtection.Run(ctx, NullLogger.Instance, CancellationToken.None));
            }

            using (var ctx = ClientAppMachineAccessTest.TestContextFactory.Build(options, null))
            {
                var tenants = ctx.Tenants.IgnoreQueryFilters().ToDictionary(t => t.TenantName);
                Assert.IsTrue(TenantKeys.IsProtected(tenants["plain"].TenantPassword));
                Assert.AreEqual(plain, TenantKeys.Unprotect(tenants["plain"].TenantPassword), "the key itself stays the same");
                Assert.AreEqual(alreadyProtected, tenants["protected"].TenantPassword);
                Assert.IsNull(tenants["none"].TenantPassword);
                Assert.AreEqual(0, TenantKeyProtection.Run(ctx, NullLogger.Instance, CancellationToken.None), "idempotent");
            }
        }

        private static Tenant NewTenant(string name, string password)
        {
            // TenantNameLower is a computed column in the database; the InMemory provider does not compute it.
            return new Tenant { TenantName = name, DisplayName = name, TenantNameLower = name, TenantPassword = password };
        }
    }
}
