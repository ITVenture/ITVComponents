using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ExternalOAuthServices;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Test
{
    /// <summary>
    /// Ob eine Dienst-Definition weitergegeben werden kann, folgt aus ihrem Typ: nur die Baum-Definition traegt
    /// <c>Inheritable</c> (Leitfaden §94).
    /// </summary>
    [TestClass]
    public class ExternalServiceDefinitionInheritanceTest
    {
        [TestMethod]
        public void OnlyTheTreeDefinition_SupportsInheritance()
        {
            Assert.IsTrue(ExternalServiceCredentialAdministration.SupportsInheritance<HierarchyExternalOAuthService>());
            Assert.IsFalse(ExternalServiceCredentialAdministration.SupportsInheritance<FlatExternalOAuthService>());
        }

        [TestMethod]
        public void TreeDefinition_OfATenant_IsPassedOn()
        {
            var service = new HierarchyExternalOAuthService();

            Assert.IsTrue(ExternalServiceCredentialAdministration.SetDefinitionInheritable(service, true, 7));

            Assert.IsTrue(service.Inheritable);
            Assert.AreEqual(true, ExternalServiceCredentialAdministration.GetDefinitionInheritable(service));
        }

        [TestMethod]
        public void GlobalDefinition_IsNeverPassedOn()
        {
            var service = new HierarchyExternalOAuthService { Inheritable = true };

            Assert.IsTrue(ExternalServiceCredentialAdministration.SetDefinitionInheritable(service, true, null));

            Assert.IsFalse(service.Inheritable, "A global service is there for everyone - passing it on means nothing.");
        }

        [TestMethod]
        public void FlatDefinition_HasNothingToSet()
        {
            var service = new FlatExternalOAuthService();

            Assert.IsFalse(ExternalServiceCredentialAdministration.SetDefinitionInheritable(service, true, 7));
            Assert.IsNull(ExternalServiceCredentialAdministration.GetDefinitionInheritable(service));
        }
    }
}
