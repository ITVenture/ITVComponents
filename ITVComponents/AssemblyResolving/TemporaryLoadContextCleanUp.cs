using ITVComponents.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.AssemblyResolving
{
    internal class TemporaryLoadContextCleanUp : IResourceLock
    {
        private AssemblyLoadContext context;

        public TemporaryLoadContextCleanUp(AssemblyLoadContext context)
        {
            this.context = context;
        }
        public IResourceLock InnerLock => null;
        public void Exclusive(bool autoLock, Action action)
        {
            action();
        }

        public T Exclusive<T>(bool autoLock, Func<T> action)
        {
            return action();
        }

        public void SynchronizeContext()
        {
        }

        public void LeaveSynchronizeContext()
        {
        }

        public IDisposable PauseExclusive()
        {
            return new ExclusivePauseHelper(() => InnerLock?.PauseExclusive());
        }

        public void Dispose()
        {
            context.Unload();
            AssemblyResolver.ReleaseLoadContext(context);
            context = null;
        }
    }
}
