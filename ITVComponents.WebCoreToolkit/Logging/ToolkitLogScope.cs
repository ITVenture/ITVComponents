using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Logging
{
    internal class ToolkitLogScope<TState> : IDisposable
    {
        private ILogger parent;
        private readonly TState state;
        private IDisposable[] children;

        public ToolkitLogScope(ILogger parent, TState state)
        {
            this.parent = parent;
            this.state = state;
            //parent.Log(LogLevel.Debug, new EventId(0, $"Begin Scope for: {state}"), state, null, (state1, exception) => "");
        }

        public ToolkitLogScope(IEnumerable<IDisposable> children)
        {
            this.children = children.ToArray();
        }

        public void Dispose()
        {
            if (children != null)
            {
                children.ForEach(c => c.Dispose());
            }
        }
    }
}
