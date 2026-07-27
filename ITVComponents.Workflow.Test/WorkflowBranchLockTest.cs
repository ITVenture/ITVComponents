using ITVComponents.Workflow.Stores;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft den Zweig-Lock des <see cref="InMemoryWorkflowStore"/>: prozessuebergreifender Ausschluss
    /// pro (Instanz, Token), Owner nach Namen, keine TTL, Reset ueber den Owner-Namen.
    /// </summary>
    [TestClass]
    public class WorkflowBranchLockTest
    {
        private InMemoryWorkflowStore store;

        [TestInitialize]
        public void Setup() => store = new InMemoryWorkflowStore();

        [TestMethod]
        public void Acquire_Contended_Release_ReAcquire()
        {
            IWorkflowBranchLock a = store.TryAcquireBranchLock("inst", "tok", "runner-A");
            Assert.IsNotNull(a);

            Assert.IsNull(store.TryAcquireBranchLock("inst", "tok", "runner-B"),
                "a held branch must not be lockable by another owner.");

            a.Dispose();

            using IWorkflowBranchLock b = store.TryAcquireBranchLock("inst", "tok", "runner-B");
            Assert.IsNotNull(b, "after release the branch is free again.");
        }

        [TestMethod]
        public void DifferentBranchesOfSameInstance_AreIndependent()
        {
            using IWorkflowBranchLock t1 = store.TryAcquireBranchLock("inst", "tok1", "r");
            using IWorkflowBranchLock t2 = store.TryAcquireBranchLock("inst", "tok2", "r");
            Assert.IsNotNull(t1);
            Assert.IsNotNull(t2, "parallel branches of one instance lock independently.");
        }

        [TestMethod]
        public void Dispose_IsIdempotent()
        {
            IWorkflowBranchLock l = store.TryAcquireBranchLock("inst", "tok", "r");
            l.Dispose();
            l.Dispose(); // darf nicht doppelt freigeben / werfen

            using IWorkflowBranchLock again = store.TryAcquireBranchLock("inst", "tok", "r2");
            Assert.IsNotNull(again);
        }

        [TestMethod]
        public void ReleaseLocksOfOwner_ResetsOnlyThatOwner()
        {
            Assert.IsNotNull(store.TryAcquireBranchLock("i1", "t", "runner-A"));
            Assert.IsNotNull(store.TryAcquireBranchLock("i2", "t", "runner-A"));
            Assert.IsNotNull(store.TryAcquireBranchLock("i3", "t", "runner-B"));

            // Runner-A startet neu -> raeumt seine eigenen Sperren ab (keine Wartefrist).
            store.ReleaseLocksOfOwner("runner-A");

            Assert.IsNotNull(store.TryAcquireBranchLock("i1", "t", "runner-C"), "A's lock was reset.");
            Assert.IsNotNull(store.TryAcquireBranchLock("i2", "t", "runner-C"), "A's lock was reset.");
            Assert.IsNull(store.TryAcquireBranchLock("i3", "t", "runner-C"), "B's lock must be untouched.");
        }
    }
}
