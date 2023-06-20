using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Threading;
using Microsoft.CodeAnalysis.Scripting;

namespace ITVComponents.Scripting.CScript.Core.Native
{
    internal class LambdaHolder
    {
        private readonly Delegate methodHandle;

        public LambdaHolder(Delegate methodHandle)
        {
            this.methodHandle = methodHandle;
        }

        public T Invoke<T>()
        {
            return AsyncHelpers.RunSync(() => ((ScriptRunner<T>)methodHandle)());
        }
    }
}
