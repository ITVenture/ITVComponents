using System.Collections.Generic;
using System.Linq;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ConfigurationHandler;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Test
{
    /// <summary>
    /// Die Token-Parameter eines externen Dienstes im Konfigurations-Export: offene Werte reisen mit, geheime nie.
    /// </summary>
    [TestClass]
    public class ExternalServiceTokenParameterSyncTest
    {
        [TestMethod]
        public void Describe_CarriesNoSecretValue()
        {
            var text = ExternalServiceTokenParameterSync.Describe(new[]
            {
                new ExternalOAuthServiceParameterTemplateMarkup { ParameterName = "client_secret", ParameterValue = "s3cret", IsSecret = true },
                new ExternalOAuthServiceParameterTemplateMarkup { ParameterName = "audience", ParameterValue = "api" }
            });

            Assert.IsFalse(text.Contains("s3cret"), "A secret value must never be part of the configuration.");
            StringAssert.Contains(text, "api");
        }

        [TestMethod]
        public void Apply_AddsUpdatesAndRemoves_ButNeverOverwritesASecret()
        {
            var target = new List<ExternalOAuthServiceParameter>
            {
                new() { ParameterName = "audience", ParameterValue = "old" },
                new() { ParameterName = "client_secret", ParameterValue = "cipher-on-target", IsSecret = true },
                new() { ParameterName = "obsolete", ParameterValue = "x" }
            };
            var json = ExternalServiceTokenParameterSync.Describe(new[]
            {
                new ExternalOAuthServiceParameterTemplateMarkup { ParameterName = "audience", ParameterValue = "new" },
                new ExternalOAuthServiceParameterTemplateMarkup { ParameterName = "client_secret", IsSecret = true },
                new ExternalOAuthServiceParameterTemplateMarkup { ParameterName = "password", IsSecret = true }
            });

            new ExternalServiceTokenParameterSync().Apply(target, json);

            Assert.AreEqual("new", target.Single(n => n.ParameterName == "audience").ParameterValue);
            Assert.AreEqual("cipher-on-target", target.Single(n => n.ParameterName == "client_secret").ParameterValue,
                "The secret of the target system must survive the import.");
            var created = target.Single(n => n.ParameterName == "password");
            Assert.IsTrue(created.IsSecret);
            Assert.IsNull(created.ParameterValue, "A new secret is created empty and set on the target.");
            Assert.IsFalse(target.Any(n => n.ParameterName == "obsolete"));
        }

        [TestMethod]
        public void Apply_EncryptsAPlainValueTheSourceMarkedSecret()
        {
            var target = new List<ExternalOAuthServiceParameter> { new() { ParameterName = "api_key", ParameterValue = "plain" } };
            var json = ExternalServiceTokenParameterSync.Describe(new[]
            {
                new ExternalOAuthServiceParameterTemplateMarkup { ParameterName = "api_key", IsSecret = true }
            });

            new ExternalServiceTokenParameterSync().Apply(target, json);

            var parameter = target.Single();
            Assert.IsTrue(parameter.IsSecret);
            Assert.AreNotEqual("plain", parameter.ParameterValue);
            Assert.AreEqual("plain", parameter.ParameterValue.Decrypt(), "Encrypted with the system key - only global services are exported.");
        }
    }
}
