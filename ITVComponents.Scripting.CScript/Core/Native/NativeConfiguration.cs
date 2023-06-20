using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;

namespace ITVComponents.Scripting.CScript.Core.Native
{
    public class NativeConfiguration
    {
        public List<string> Usings { get; } = new List<string>();

        public List<string> References { get; } = new List<string>();

        public bool AutoReferences { get; set; } = false;

        internal InteractiveAssemblyLoader AssemblyLoader { get; private set; } = new InteractiveAssemblyLoader();

        internal ConcurrentDictionary<string, Lazy<ScriptRunner<object>>> Scripts { get; } =
            new ConcurrentDictionary<string, Lazy<ScriptRunner<object>>>();

        internal ConcurrentDictionary<string, Lazy<LambdaHolder>> ExpressionBuilders { get; } =
            new ConcurrentDictionary<string, Lazy<LambdaHolder>>();

        public void Reset()
        {
            Scripts.Clear();
            AssemblyLoader.Dispose();
            AssemblyLoader = new InteractiveAssemblyLoader();
        }
    }
}
