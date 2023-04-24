using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using ITVComponents.AssemblyResolving;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core.Native;
using ITVComponents.Scripting.CScript.Helpers;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace ITVComponents.DataAccess.Linq
{
    public static class LinqCommandCreator
    {
        /// <summary>
        /// The required references for executing a linq query
        /// </summary>
        private static readonly string[] Extensions = new string[] {"System.dll",
        "System.Data.dll",
        "System.Data.DataSetExtensions.dll",
        "System.Core.dll",
        "Microsoft.CSharp.dll",
        "ITVComponents.dll",
        "ITVComponents.DataAccess.dll",
        "ITVComponents.DataAccess.Linq.dll"};

        /// <summary>
        /// Holds dynamic extensions that are required for the linqCommands in order to work properly
        /// </summary>
        private static List<string> dynamicExtensions = new List<string>();

        /// <summary>
        /// A list of dynamic usings
        /// </summary>
        private static List<string> dynamicUsings = new List<string>();

        /// <summary>
        /// holds all roslyn generated scripts
        /// </summary>
        private static ConcurrentDictionary<string, Lazy<Script<object>>> roslynScripts = new ConcurrentDictionary<string, Lazy<Script<object>>>();

        /// <summary>
        /// Adds a Namespace that must be in the using-list for the created command class
        /// </summary>
        /// <param name="nameSpace">the namespace that must be in the using-list for the query-class</param>
        public static void AddUsingNamespace(string nameSpace)
        {
            lock (dynamicUsings)
            {
                if (!dynamicUsings.Contains(nameSpace,StringComparer.OrdinalIgnoreCase))
                {
                    dynamicUsings.Add(nameSpace);
                }
            }
        }

        /// <summary>
        /// Registers a Reference that needs to be added to querycommand-classes in order to work properly
        /// </summary>
        /// <param name="assemblyName">the required assembly</param>
        public static void AddDynamicExtension(string assemblyName)
        {
            lock (dynamicExtensions)
            {
                if (Extensions.All(n => n.ToLower() != assemblyName.ToLower()) &&
                    !dynamicExtensions.Contains(assemblyName, StringComparer.OrdinalIgnoreCase))
                {
                    dynamicExtensions.Add(assemblyName);
                }
            }
        }

        /// <summary>
        /// Creates a Command Executor object that is capable for executing the requested command
        /// </summary>
        /// <param name="command">the Query to execute on the offline tables</param>
        /// <returns>a procy object that is capable for executing the requested query</returns>
        public static Script<object> CreateCommand(string command)
        {
            var roslynHash = GetHashSha256(command);
            var roslynScript = roslynScripts.GetOrAdd(roslynHash, new Lazy<Script<object>>(() =>
            {
                var scriptoptions = ScriptOptions.Default.WithImports(dynamicUsings.Union(new[] {"System", "System.Linq", "System.Collections.Generic", "ITVComponents", "ITVComponents.DataAccess", "ITVComponents.DataAccess.Extensions", "ITVComponents.DataAccess.Linq"})).WithReferences(new[] {typeof(FileStyleUriParser).Assembly, typeof(Action).Assembly}.Union(from t in Extensions.Union(dynamicExtensions).Distinct() select AssemblyResolver.FindAssemblyByName(t)));
                return CSharpScript.Create(command, scriptoptions, typeof(NativeScriptObjectHelper));
            }));

            return roslynScript.Value;
        }

        /// <summary>
        /// Creates a hash of the given method-text
        /// </summary>
        /// <param name="text">the generated method code</param>
        /// <returns>a unique hash-code (sha256) identifying the generated method uniquely</returns>
        private static string GetHashSha256(string text)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(text);
            SHA256Managed hashstring = new SHA256Managed();
            byte[] hash = hashstring.ComputeHash(bytes);
            string hashString = string.Empty;
            foreach (byte x in hash)
            {
                hashString += String.Format("{0:x2}", x);
            }
            return hashString;
        }
    }
}
