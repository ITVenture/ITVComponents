using ITVComponents.Workflow.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft den ambienten <see cref="WorkflowExecutionScope"/>: Setzen, Verschachtelung,
    /// Wiederherstellung und die Unterscheidung „tenant-frei" vs. „kein Scope aktiv".
    /// </summary>
    [TestClass]
    public class WorkflowExecutionScopeTest
    {
        [TestCleanup]
        public void Cleanup()
        {
            // Sicherstellen, dass kein Scope aus einem Test in den naechsten leckt (AsyncLocal je Flow,
            // aber MSTest kann Tests auf demselben Thread ausfuehren).
            Assert.IsFalse(WorkflowExecutionScope.HasTenant, "a test leaked an open tenant scope.");
        }

        [TestMethod]
        public void NoScope_HasNoTenant()
        {
            Assert.IsFalse(WorkflowExecutionScope.HasTenant);
            Assert.IsNull(WorkflowExecutionScope.CurrentTenant);
        }

        [TestMethod]
        public void UseTenant_SetsAndRestores()
        {
            using (WorkflowExecutionScope.UseTenant("tenantA"))
            {
                Assert.IsTrue(WorkflowExecutionScope.HasTenant);
                Assert.AreEqual("tenantA", WorkflowExecutionScope.CurrentTenant);
            }

            Assert.IsFalse(WorkflowExecutionScope.HasTenant);
            Assert.IsNull(WorkflowExecutionScope.CurrentTenant);
        }

        [TestMethod]
        public void UseTenant_Nests_RestoresOuter()
        {
            using (WorkflowExecutionScope.UseTenant("outer"))
            {
                Assert.AreEqual("outer", WorkflowExecutionScope.CurrentTenant);
                using (WorkflowExecutionScope.UseTenant("inner"))
                {
                    Assert.AreEqual("inner", WorkflowExecutionScope.CurrentTenant);
                }

                Assert.AreEqual("outer", WorkflowExecutionScope.CurrentTenant);
            }
        }

        [TestMethod]
        public void UseTenant_Null_IsDeliberatelyTenantFree()
        {
            using (WorkflowExecutionScope.UseTenant(null))
            {
                // "gesetzt auf null" ist NICHT dasselbe wie "kein Scope": HasTenant ist true.
                Assert.IsTrue(WorkflowExecutionScope.HasTenant);
                Assert.IsNull(WorkflowExecutionScope.CurrentTenant);
            }
        }
    }
}
