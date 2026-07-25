using ITVComponents.Workflow.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.EntityFramework.Test
{
    /// <summary>
    /// Prueft, dass <see cref="WorkflowAmbientUserContext"/> den Tenant aus dem ambienten
    /// <see cref="WorkflowExecutionScope"/> spiegelt (der Bridge fuer beliebige tenant-abhaengige
    /// Kontexte im Dienst).
    /// </summary>
    [TestClass]
    public class WorkflowAmbientUserContextTest
    {
        [TestMethod]
        public void ReflectsTheAmbientTenant()
        {
            var ctx = new WorkflowAmbientUserContext();
            Assert.IsNull(ctx.CurrentTenant, "no scope active -> no tenant.");
            Assert.IsNull(ctx.CurrentUserName, "there is no logged-in user in a service.");

            using (WorkflowExecutionScope.UseTenant("tenantX"))
            {
                Assert.AreEqual("tenantX", ctx.CurrentTenant);
            }

            Assert.IsNull(ctx.CurrentTenant, "scope restored -> tenant gone.");
        }
    }
}
