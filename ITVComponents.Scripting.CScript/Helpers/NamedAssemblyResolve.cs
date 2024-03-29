﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace ITVComponents.Scripting.CScript.Helpers
{
    [Obsolete("Use AssemblyResolver of ITVComponents.AssemblyResolving instead.", true)]
    public static class NamedAssemblyResolve
    {
        /// <summary>
        /// Holds a list of registered resolvers
        /// </summary>
        private static List<ResolveByName> resolvers = new List<ResolveByName>();

        /// <summary>
        /// Registers a resolver for finding assemblies
        /// </summary>
        /// <param name="resolver">the resolver that can be requested for finding assemblies</param>
        public static void RegisterResolver(ResolveByName resolver)
        {
            lock (resolvers)
            {
                resolvers.Add(resolver);
            }
        }

        /// <summary>
        /// Finds an assembly by its name
        /// </summary>
        /// <param name="name">the name of the requested assembly</param>
        /// <param name="assembly">the assembly that is searched for</param>
        /// <returns>a value indicating whether the assembyl was found</returns>
        public static bool ResolveByName(string name, out Assembly assembly)
        {
            lock (resolvers)
            {
                foreach (ResolveByName resolver in resolvers)
                {
                    if (resolver(name, out assembly))
                    {
                        return true;
                    }
                }
            }

            assembly = null;
            return false;
        }

        public static Assembly LoadAssembly(string assemblyName)
        {
            Assembly src;
            if (File.Exists(assemblyName))
            {
                src = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(assemblyName));
            }
            else
            {
                if (!NamedAssemblyResolve.ResolveByName(assemblyName, out src))
                {
                    src = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(assemblyName));
                }
            }

            return src;
        }
    }

    /// <summary>
    /// A Resolver callback for looking up assemblies
    /// </summary>
    /// <param name="name">the Assembly that is being looked up</param>
    /// <param name="assembly">the instance representing the requested assembly</param>
    /// <returns>a value indicating whether the assembly could be found</returns>
    public delegate bool ResolveByName(string name, out Assembly assembly);
}
