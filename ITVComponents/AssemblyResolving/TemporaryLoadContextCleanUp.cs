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

        public void Dispose()
        {
            context.Unload();
            AssemblyResolver.ReleaseLoadContext(context);
            context = null;
        }
    }
}
