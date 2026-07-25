using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Stores;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft das optimistische Commit-Primitiv des <see cref="InMemoryWorkflowStore"/>: ein
    /// <see cref="IWorkflowStore.TryCommitInstance"/> gelingt nur, wenn der Stand noch der erwarteten
    /// Version entspricht - so serialisieren sich gleichzeitige Zweig-Merges derselben Instanz.
    /// </summary>
    [TestClass]
    public class WorkflowOptimisticCommitTest
    {
        [TestMethod]
        public void TryCommit_SucceedsOnMatchingVersion_ConflictsOnStale()
        {
            var store = new InMemoryWorkflowStore();
            var inst = new WorkflowInstance { DefinitionId = "d", DefinitionVersion = 1 };
            store.SaveInstance(inst);

            Assert.AreEqual(0, inst.Version, "a fresh instance starts at version 0.");

            Assert.IsTrue(store.TryCommitInstance(inst, 0), "committing at the current version succeeds.");
            Assert.AreEqual(1, inst.Version);

            Assert.IsFalse(store.TryCommitInstance(inst, 0), "a stale base version must conflict.");

            Assert.IsTrue(store.TryCommitInstance(inst, 1), "committing at the fresh version succeeds again.");
            Assert.AreEqual(2, inst.Version);
        }
    }
}
