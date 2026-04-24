using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;

namespace ITVComponents.Scripting.CScript.Core.Native
{
    public class NativeConfiguration
    {
        private List<string> usings = new List<string>();
        private List<string> references = new List<string>();
        private List<string> stubbornReferences = new();
        private List<string> stubbornUsings = new ();

        private ConcurrentDictionary<string, string> scriptLabelToHash = new ConcurrentDictionary<string, string>();
        private ConcurrentDictionary<string, Lazy<ScriptRunner<object>>> scripts =
            new ConcurrentDictionary<string, Lazy<ScriptRunner<object>>>();

        private ConcurrentDictionary<string, string> expressionLabelToHash = new ConcurrentDictionary<string, string>();
        private ConcurrentDictionary<string, Lazy<LambdaHolder>> expressionBuilders =
            new ConcurrentDictionary<string, Lazy<LambdaHolder>>();

        private bool isDirty = false;

        private bool containsCode = false;


        public NativeConfiguration()
        {
            Usings = new ReadOnlyCollection<string>(usings);
            References = new ReadOnlyCollection<string>(references);
            Scripts = new ReadOnlyDictionary<string, Lazy<ScriptRunner<object>>>(scripts);
            ExpressionBuilders = new ReadOnlyDictionary<string, Lazy<LambdaHolder>>(expressionBuilders);
        }

        public IReadOnlyList<string> Usings { get; }

        public IReadOnlyList<string> References { get; }

        public bool AutoReferences { get; set; } = false;

        internal InteractiveAssemblyLoader AssemblyLoader { get; private set; } = new InteractiveAssemblyLoader();

        internal IReadOnlyDictionary<string, Lazy<ScriptRunner<object>>> Scripts { get; }

        internal IReadOnlyDictionary<string, Lazy<LambdaHolder>> ExpressionBuilders { get; }

    public void Reset(bool fullReset)
        {
            scripts.Clear();
            scriptLabelToHash.Clear();
            expressionBuilders.Clear();
            AssemblyLoader.Dispose();
            if (fullReset)
            {
                usings.Clear();
                references.Clear();
                usings.AddRange(stubbornUsings);
                references.AddRange(stubbornReferences);
            }

            AssemblyLoader = new InteractiveAssemblyLoader();
            isDirty = false;
            containsCode = false;
        }

        internal Lazy<LambdaHolder> GetOrAddExpressionBuilder(string label, string roslynHash, Lazy<LambdaHolder> lazy)
        {
            bool fullReset = false;
            if (!string.IsNullOrEmpty(label))
            {
                expressionLabelToHash.AddOrUpdate(label, l => roslynHash, (l, o) =>
                {
                    fullReset = true;
                    isDirty = true;
                    return roslynHash;
                });
            }
            if (isDirty)
            {
                Reset(fullReset);
            }

            var retVal= expressionBuilders.GetOrAdd(roslynHash, lazy);
            if (retVal.IsValueCreated)
            {
                var faulted = false;
                try
                {
                    var tmp = retVal.Value;
                }
                catch
                {
                    faulted = true;
                }

                if (faulted)
                {
                    expressionBuilders.TryRemove(roslynHash, out _);
                    return expressionBuilders.GetOrAdd(roslynHash, lazy);
                }
            }

            return retVal;
        }

        public void AddReference(string reference, bool stubborn = false)
        {
            if (stubborn)
            {
                stubbornReferences.AddIfMissing(reference);
            }
            
            if (references.AddIfMissing(reference) && containsCode)
            {
                isDirty = true;
            }
        }

        public void AddUsing(string usingParam, bool stubborn = false)
        {
            if (stubborn)
            {
                stubbornUsings.AddIfMissing(usingParam);
            }

            if (usings.AddIfMissing(usingParam) && containsCode)
            {
                isDirty = true;
            }
        }

        public Lazy<ScriptRunner<object>> GetOrAddScript(string label, string roslynHash, Lazy<ScriptRunner<object>> lazy)
        {
            bool fullReset = false;
            if (!string.IsNullOrEmpty(label))
            {
                scriptLabelToHash.AddOrUpdate(label, l => roslynHash, (l, o) =>
                {
                    //fullReset = true;
                    isDirty = true;
                    return roslynHash;
                });
            }

            if (isDirty)
            {
                Reset(fullReset);
            }

            containsCode = true;
            var retVal= scripts.GetOrAdd(roslynHash, lazy);
            if (retVal.IsValueCreated)
            {
                var faulted = false;
                try
                {
                    var tmp = retVal.Value;
                }
                catch
                {
                    faulted = true;
                }

                if (faulted)
                {
                    scripts.TryRemove(roslynHash, out _);
                    return scripts.GetOrAdd(roslynHash, lazy);
                }
            }

            return retVal;
        }
    }
}
