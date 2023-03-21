using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using ITVComponents.Logging;
using ITVComponents.Settings;
using ITVComponents.Threading;

namespace ITVComponents.AssemblyResolving
{
    public static class AssemblyResolver
    {
        /// <summary>
        /// Indicates whether this resolver is currently active
        /// </summary>
        private static bool enabled = false;

        /// <summary>
        /// the configuration collection that is used by this resolver
        /// </summary>
        private static Dictionary<string, AssemblyResolverConfigurationItem> config = new Dictionary<string, AssemblyResolverConfigurationItem>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets a value indicating whether the Resovler is currently enabled
        /// </summary>
        public static bool Enabled
        {
            get
            {
                return enabled;
            }
            set
            {
                if (value && !enabled)
                {
                    EnableResolver();
                    enabled = true;
                }
                else if (!value && enabled)
                {
                    DisableResolver();
                    enabled = false;
                }
            }
        }

        /// <summary>
        /// Finds an assembly with the given name
        /// </summary>
        /// <param name="assemblyName">the fileName or part of it</param>
        /// <param name="targetContext">Enables the caller to specify a custom load-context. If none is provided, the current executioncontext is used.</param>
        /// <returns>the requested assembly</returns>
        public static Assembly FindAssemblyByFileName(string assemblyName, AssemblyLoadContext targetContext = null)
        {
            string location = ResolveAssemblyLocation(assemblyName, out _);
            var context = targetContext ?? AssemblyLoadContext.Default;
            Assembly existingAssembly = (from t in context.Assemblies
                where
                    !t.IsDynamic &&
                    (t.Location.Equals(location, StringComparison.OrdinalIgnoreCase) || t.FullName.Equals(assemblyName, StringComparison.OrdinalIgnoreCase))
                select t).FirstOrDefault();
            if (existingAssembly == null)
            {
                return TryLoadAssembly(location, context);
            }

            return existingAssembly;
        }

        /// <summary>
        /// Finds an assembly with the given name
        /// </summary>
        /// <param name="assemblyName">AssemblyName or a part of it</param>
        /// <param name="targetContext">Enables the caller to specify a custom load-context. If none is provided, the current executioncontext is used.</param>
        /// <returns>the requested assembly</returns>
        public static Assembly FindAssemblyByName(string assemblyName, AssemblyLoadContext targetContext = null)
        {
            try
            {
                var retVal  = FindAssemblyByFileName(assemblyName, targetContext);
                if (retVal != null)
                {
                    return retVal;
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogDebugEvent($"Loading of assembly failed. ({ex.Message}", LogSeverity.Warning);
            }

            var an = new AssemblyName(assemblyName);
            return (targetContext??AssemblyLoadContext.Default).LoadFromAssemblyName(an);
        }

        /// <summary>
        /// Finds the reflective type for a specific Type
        /// </summary>
        /// <param name="normalType">the target type for which to get the reflection-only type</param>
        /// <returns>the reflection-only appeareance of the requested type</returns>
        public static Type FindReflectionOnlyTypeFor(Type normalType)
        {
            return normalType;
        }

        /// <summary>
        /// Enables the resolver
        /// </summary>
        private static void EnableResolver()
        {
            config.Clear();
            var list = ResolverConfig.Helper.FixedAssemblies;
            foreach (var item in list)
            {
                config.Add(item.Name, item);
            }

            AssemblyLoadContext.Default.Resolving += ResolveConfiguredAssembly;
        }

        public static string ResolveAssemblyLocation(string assemblyName, out bool exists)
        {
            var path = assemblyName;
            exists = File.Exists(path);
            if (!exists)
            {
                if (!Path.IsPathFullyQualified(path))
                {
                    var p = Path.Combine(AppContext.BaseDirectory, path);
                    exists = File.Exists(p);
                    if (exists)
                        path = p;

                    if (!exists)
                    {
                        var rt = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location);
                        p = Path.Combine(rt, path);
                        exists = File.Exists(p);
                        if (exists)
                            path = p;
                    }

                    if (!exists)
                    {
                        var rt = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                        p = Path.Combine(rt, path);
                        exists = File.Exists(p);
                        if (exists)
                            path = p;
                    }
                }
            }

            return Path.GetFullPath(path);
        }

        public static IResourceLock AcquireTemporaryLoadContext(out AssemblyLoadContext context)
        {
            context = new AssemblyLoadContext($"@@TMP{DateTime.Now:yyyMMddHHmmssfff}", true);
            context.Resolving += ResolveConfiguredAssembly;
            return new TemporaryLoadContextCleanUp(context);
        }

        internal static void ReleaseLoadContext(AssemblyLoadContext context)
        {
            context.Resolving -= ResolveConfiguredAssembly;
        }

        private static Assembly TryLoadAssembly(string assemblyName, AssemblyLoadContext targetLoadContext)
        {
            var path = ResolveAssemblyLocation(assemblyName, out bool exists);
            Assembly retVal = null;
            if (exists)
            {
                retVal = targetLoadContext.Assemblies.Where(n => !n.ReflectionOnly && !n.IsDynamic && n.Location.Equals(path, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (retVal == null)
                {
                    retVal = targetLoadContext.LoadFromAssemblyPath(path);
                }
                /*if (!reflect)
                {
                    retVal = AssemblyLoadContext.All.Where(c => !c.IsCollectible).SelectMany(n => n.Assemblies).Where(n => !n.ReflectionOnly && n.Location.Equals(path, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (retVal == null)
                    {
                        retVal = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
                    }
                }
                else
                {
                    retVal = Assembly.ReflectionOnlyLoadFrom(path);
                }*/
            }
            else
            {
                try
                {
                    var n = new AssemblyName(assemblyName);
                    retVal = targetLoadContext.LoadFromAssemblyName(n);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogDebugEvent($"Error resolving Assembly by name: {ex.Message}", LogSeverity.Error);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Disables the resolver
        /// </summary>
        private static void DisableResolver()
        {
            AssemblyLoadContext.Default.Resolving -= ResolveConfiguredAssembly;
        }

        private static Assembly? ResolveConfiguredAssembly(AssemblyLoadContext loadContext, AssemblyName assemblyName)
        {
            LogEnvironment.LogDebugEvent($"Trying to resolve {assemblyName} ({assemblyName.Name})", LogSeverity.Report);
            AssemblyResolverConfigurationItem item = null;
            if (config.ContainsKey(assemblyName.Name))
            {
                item = config[assemblyName.Name];
            }
            if (item != null)
            {
                var pth = Path.GetFullPath(item.Path);
                LogEnvironment.LogDebugEvent($"Loading {pth}...", LogSeverity.Report);
                return loadContext.LoadFromAssemblyPath(pth);
            }

            var tmpS = assemblyName.Name;
            var subDir = "./";
            if (tmpS.EndsWith("resources", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(assemblyName.CultureName))
            {
                subDir = Path.Combine(subDir, assemblyName.CultureName);
            }

            var calcPath = Path.GetFullPath(Path.Combine(subDir, $"{tmpS}.dll"));
            if (File.Exists(calcPath))
            {
                return loadContext.LoadFromAssemblyPath(calcPath);
            }

            return null;
        }
    }
}
