using System;
using System.Collections.Generic;
using ITVComponents.Threading;

namespace ITVComponents.FileWrapping.Helpers
{
    internal class DeferredDisposalHelper:IResourceLock
    {
        /// <summary>
        /// a list of objects that need to be disposed with this object
        /// </summary>
        private List<IDisposable> targets = new List<IDisposable>();

        /// <summary>
        /// Initializes a new instance of the DeferredDisposalHelper
        /// </summary>
        /// <param name="targets"></param>
        public DeferredDisposalHelper(params IDisposable[] targets)
        {
            foreach (var target in targets)
            {
                AddTarget(target);
            }
        }

        /// <summary>
        /// Adds a target to the list of to-dispose objects
        /// </summary>
        /// <param name="target"></param>
        public void AddTarget(IDisposable target)
        {
            targets.Insert(0, target);
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            targets.ForEach(n => n.Dispose());
            targets.Clear();
        }

        /// <summary>
        /// Gets the inner lock of this Resource Lock instance
        /// </summary>
        public IResourceLock InnerLock { get; } = null;
    }
}
