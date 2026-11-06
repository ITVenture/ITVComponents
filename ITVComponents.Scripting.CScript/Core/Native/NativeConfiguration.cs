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
using ITVComponents.Logging;
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
            ForgetReplacedVersion(expressionLabelToHash, expressionBuilders, label, roslynHash);
            if (isDirty)
            {
                Reset(false);
            }

            return GetOrAddCompiled(expressionBuilders, roslynHash, lazy);
        }

        /// <summary>
        /// Drops the compiled version a label held before, if its text has changed.
        /// </summary>
        /// <remarks>
        /// Only a different hash counts as a change. Running the same text again must keep everything compiled,
        /// otherwise each repeated call costs a new compilation and leaves another assembly in memory: the
        /// assemblies are not collectible, so dropping an entry never frees its code.
        /// </remarks>
        private static void ForgetReplacedVersion<T>(ConcurrentDictionary<string, string> labelToHash,
            ConcurrentDictionary<string, Lazy<T>> compiled, string label, string roslynHash)
        {
            if (string.IsNullOrEmpty(label))
            {
                return;
            }

            string previousHash = null;
            labelToHash.AddOrUpdate(label, l => roslynHash, (l, o) =>
            {
                previousHash = o;
                return roslynHash;
            });

            if (previousHash != null && previousHash != roslynHash)
            {
                compiled.TryRemove(previousHash, out _);
            }
        }

        /// <summary>
        /// Gets the compiled entry for a hash, replacing an entry whose compilation has failed.
        /// </summary>
        /// <remarks>
        /// A <see cref="Lazy{T}"/> keeps the exception of its factory and reports <c>IsValueCreated == false</c>,
        /// so a failed compilation would otherwise stay cached for as long as the text does not change, even when
        /// its cause (an assembly that was not loaded yet, for instance) has gone away.
        /// </remarks>
        private static Lazy<T> GetOrAddCompiled<T>(ConcurrentDictionary<string, Lazy<T>> compiled, string roslynHash, Lazy<T> lazy)
        {
            var retVal = compiled.GetOrAdd(roslynHash, lazy);
            if (ReferenceEquals(retVal, lazy))
            {
                return retVal;
            }

            try
            {
                _ = retVal.Value;
                return retVal;
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"An earlier compilation of script {roslynHash} failed and is retried: {ex.OutlineException()}", LogSeverity.Warning);
                compiled.TryRemove(new KeyValuePair<string, Lazy<T>>(roslynHash, retVal));
                return compiled.GetOrAdd(roslynHash, lazy);
            }
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
            ForgetReplacedVersion(scriptLabelToHash, scripts, label, roslynHash);
            if (isDirty)
            {
                Reset(false);
            }

            containsCode = true;
            return GetOrAddCompiled(scripts, roslynHash, lazy);
        }
    }
}
