using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ITVComponents.Plugins.Modules;

namespace ITVComponents.UpdateService.ModuleBuild
{
    /// <summary>
    /// Packs the publish output of a plugin project as a module of the update service, after checking it the way the
    /// service does when it installs it (<c>ModuleFiles.Stage</c>) - so a broken module fails the publish instead of the
    /// installation at the till.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The package is the publish folder itself at the root of a zip. Id and version are not part of it: they come from
    /// the release in the web, and the service writes <c>module.json</c> from there.
    /// </para>
    /// <para>
    /// Checked:
    /// </para>
    /// <list type="bullet">
    /// <item>exactly one main assembly - named by <c>module.json</c> (<c>mainAssembly</c>) or the only <c>.deps.json</c>;</item>
    /// <item>framework-dependent: a self-contained publish carries a runtime of its own;</item>
    /// <item>the <c>ModulePlugin</c> marks (<see cref="ModulePluginMarks.Check"/>): only on closed, buildable types
    /// that implement IPlugin, infrastructure with a valid unique name, templates without one. A module without any
    /// mark is a warning.</item>
    /// </list>
    /// <para>
    /// Contract assemblies (<c>[assembly: AssemblyMetadata("ITVComponents.PluginContract", "true")]</c>) are left out of
    /// the package, with their <c>.pdb</c>, <c>.xml</c>, <c>.dll.config</c> and satellite assemblies: the module shares them
    /// with its host (<c>ModuleLoadContext</c> always takes the host's), and the service refuses a module that brings its
    /// own copy. A publish copies them in anyway as soon as a referenced project depends on them - <c>Private=false</c>
    /// does not reach transitive project references. A contract the host does not have is kept by naming it in
    /// <c>allowedContracts</c>.
    /// </para>
    /// </remarks>
    public static class ModulePackager
    {
        /// <summary>
        /// The key of the <c>AssemblyMetadata</c> attribute that marks a contract assembly (see <c>PluginContract</c>).
        /// </summary>
        public const string ContractMetadataKey = "ITVComponents.PluginContract";

        private const string ManifestFileName = "module.json";

        private static readonly string[] RuntimeFiles = { "System.Private.CoreLib.dll", "coreclr.dll", "hostfxr.dll", "hostpolicy.dll" };

        private static readonly Regex MainAssemblyEntry = new Regex("\"mainAssembly\"\\s*:\\s*\"(?<name>[^\"]*)\"", RegexOptions.CultureInvariant);

        /// <summary>
        /// Checks a publish folder without packing it.
        /// </summary>
        /// <param name="publishDirectory">the publish output of the module</param>
        /// <param name="allowedContracts">file names of contract assemblies the module may bring nevertheless (the host
        /// does not have them); usually none</param>
        /// <param name="mainAssembly">the main assembly, if one was found</param>
        /// <returns>what is wrong; empty if the module can be packed</returns>
        public static IReadOnlyList<string> Validate(string publishDirectory, IEnumerable<string>? allowedContracts, out string? mainAssembly)
        {
            return Inspect(publishDirectory, allowedContracts, out mainAssembly, out _, out _, out _);
        }

        /// <summary>
        /// Checks a publish folder without packing it, and returns what could not be verified as well.
        /// </summary>
        /// <param name="publishDirectory">the publish output of the module</param>
        /// <param name="allowedContracts">see <see cref="Validate(string, IEnumerable{string}?, out string?)"/></param>
        /// <param name="mainAssembly">the main assembly, if one was found</param>
        /// <param name="warnings">what does not stop the publish but should be looked at, e.g. a module without any
        /// <c>ModulePlugin</c> mark</param>
        /// <returns>what is wrong; empty if the module can be packed</returns>
        public static IReadOnlyList<string> Validate(string publishDirectory, IEnumerable<string>? allowedContracts, out string? mainAssembly, out IReadOnlyList<string> warnings)
        {
            var problems = Inspect(publishDirectory, allowedContracts, out mainAssembly, out _, out var found, out _);
            warnings = found;
            return problems;
        }

        /// <summary>
        /// Checks a publish folder, finds the contract assemblies that will be left out and reads the plugin marks.
        /// </summary>
        private static List<string> Inspect(string publishDirectory, IEnumerable<string>? allowedContracts, out string? mainAssembly, out List<string> contracts,
            out List<string> warnings, out List<string> plugins)
        {
            mainAssembly = null;
            contracts = new List<string>();
            warnings = new List<string>();
            plugins = new List<string>();
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(publishDirectory) || !Directory.Exists(publishDirectory))
            {
                problems.Add($"The publish folder '{publishDirectory}' does not exist.");
                return problems;
            }

            var root = Path.GetFullPath(publishDirectory);
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                problems.Add($"The publish folder '{root}' is empty.");
                return problems;
            }

            mainAssembly = MainAssemblyOf(root, problems);

            var selfContained = RuntimeFiles.Where(f => File.Exists(Path.Combine(root, f))).ToArray();
            if (selfContained.Length != 0)
            {
                problems.Add($"The module was published self-contained ({string.Join(", ", selfContained)}); a module runs in the runtime of its host. Publish it framework-dependent (SelfContained=false).");
            }

            var allowed = new HashSet<string>(allowedContracts ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var moduleAssemblies = new List<string>();
            foreach (var dll in files.Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var topLevel = string.Equals(Path.GetDirectoryName(dll), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
                if (allowed.Contains(Path.GetFileName(dll)))
                {
                    if (topLevel)
                    {
                        moduleAssemblies.Add(dll);
                    }

                    continue;
                }

                bool contract;
                try
                {
                    contract = IsContract(dll);
                }
                catch (BadImageFormatException ex)
                {
                    // Reported as a problem of the module - a damaged file is worth stopping the publish for.
                    problems.Add($"{RelativePath(root, dll)} is no readable DLL: {ex.Message}");
                    continue;
                }

                if (!contract)
                {
                    if (topLevel)
                    {
                        moduleAssemblies.Add(dll);
                    }

                    continue;
                }

                if (mainAssembly != null && string.Equals(RelativePath(root, dll), mainAssembly, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"The main assembly {mainAssembly} is marked as contract (ITVComponents.PluginContract) - a module is loaded in a context of its own, a contract never is. Build the module without the mark.");
                    continue;
                }

                contracts.Add(dll);
            }

            CheckPluginMarks(root, moduleAssemblies, problems, warnings, plugins);
            return problems;
        }

        /// <summary>
        /// Reads the <c>ModulePlugin</c> marks of the module's own assemblies (top level of the publish folder, the
        /// contracts excluded) and checks them like the service will. Base types and interfaces are followed into every
        /// assembly at the top level of the publish folder - the contracts are still there before packing.
        /// </summary>
        private static void CheckPluginMarks(string root, List<string> moduleAssemblies, List<string> problems, List<string> warnings, List<string> plugins)
        {
            var available = Directory.GetFiles(root, "*.dll", SearchOption.TopDirectoryOnly)
                .GroupBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            Func<string, string?> resolve = name => available.TryGetValue(name, out var path) ? path : null;

            var marks = new List<ModulePluginMark>();
            foreach (var dll in moduleAssemblies)
            {
                try
                {
                    marks.AddRange(ModulePluginMarks.Read(dll, resolve));
                }
                catch (BadImageFormatException ex)
                {
                    problems.Add($"{RelativePath(root, dll)} is no readable DLL: {ex.Message}");
                }
            }

            ModulePluginMarks.Check(marks, problems, warnings);
            if (marks.Count == 0)
            {
                warnings.Add("The module offers no plugin: none of its assemblies carries a ModulePlugin mark ([ModulePlugin(kind, loadType)] from ITVComponents.Plugins.Modules). Nothing of it can be registered as plugin.");
            }

            plugins.AddRange(marks.Select(m => m.ToString()));
        }

        /// <summary>
        /// The contract assemblies and what belongs to them: symbols, documentation, config, satellite assemblies.
        /// </summary>
        private static HashSet<string> LeftOutFiles(IEnumerable<string> files, IEnumerable<string> contracts)
        {
            var leftOut = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var contract in contracts)
            {
                var directory = Path.GetDirectoryName(contract)!;
                var stem = Path.GetFileNameWithoutExtension(contract);
                names.Add(stem + ".resources.dll");
                leftOut.Add(contract);
                foreach (var companion in new[] { ".pdb", ".xml", ".dll.config" })
                {
                    leftOut.Add(Path.Combine(directory, stem + companion));
                }
            }

            foreach (var file in files.Where(f => names.Contains(Path.GetFileName(f))))
            {
                leftOut.Add(file);
            }

            return leftOut;
        }

        /// <summary>
        /// Checks a publish folder and packs it.
        /// </summary>
        /// <param name="publishDirectory">the publish output of the module</param>
        /// <param name="packagePath">the zip to write; it is replaced if it exists, and must not lie in the publish folder</param>
        /// <param name="allowedContracts">see <see cref="Validate(string, IEnumerable{string}?, out string?)"/></param>
        /// <returns>what was packed</returns>
        /// <exception cref="ModulePackageException">the module can not be packed; nothing is written</exception>
        public static ModulePackageResult Pack(string publishDirectory, string packagePath, IEnumerable<string>? allowedContracts = null)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                throw new ArgumentNullException(nameof(packagePath));
            }

            var problems = Inspect(publishDirectory, allowedContracts, out var mainAssembly, out var contracts, out var warnings, out var plugins);
            var target = Path.GetFullPath(packagePath);
            if (Directory.Exists(publishDirectory))
            {
                var root = Path.GetFullPath(publishDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"The package '{target}' would lie in the publish folder and pack itself; write it next to it.");
                }
            }

            if (problems.Count != 0)
            {
                throw new ModulePackageException(problems);
            }

            var source = Path.GetFullPath(publishDirectory);
            var all = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            var leftOut = LeftOutFiles(all, contracts);
            var files = all.Where(f => !leftOut.Contains(f)).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            // Written beside and then moved: a broken zip under the final name would look like a package.
            var temporary = target + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    foreach (var file in files)
                    {
                        // Forward slashes on every platform - the service unpacks with ZipFile, on Windows.
                        var entry = zip.CreateEntry(RelativePath(source, file).Replace('\\', '/'), CompressionLevel.Optimal);
                        entry.LastWriteTime = File.GetLastWriteTime(file);
                        using var input = File.OpenRead(file);
                        using var output = entry.Open();
                        input.CopyTo(output);
                    }
                }

                if (File.Exists(target))
                {
                    File.Delete(target);
                }

                File.Move(temporary, target);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }

            string hash;
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(target))
            {
                hash = string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
            }

            return new ModulePackageResult(target, mainAssembly!, hash, new FileInfo(target).Length, files.Length,
                contracts.Select(c => RelativePath(source, c)).ToArray(), warnings, plugins);
        }

        /// <summary>
        /// Whether the assembly in a file carries the contract mark. Read from the metadata, without loading it.
        /// </summary>
        /// <param name="path">the assembly file</param>
        /// <returns>true if it is a contract; false if not, or if the file is no managed assembly (e.g. a native DLL)</returns>
        public static bool IsContract(string path)
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata)
            {
                return false;
            }

            var metadata = pe.GetMetadataReader();
            if (!metadata.IsAssembly)
            {
                return false;
            }

            foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
            {
                var attribute = metadata.GetCustomAttribute(handle);
                if (!IsAssemblyMetadataAttribute(metadata, attribute))
                {
                    continue;
                }

                var value = attribute.DecodeValue(new StringOnlyDecoder());
                if (value.FixedArguments.Length == 2
                    && string.Equals(value.FixedArguments[0].Value as string, ContractMetadataKey, StringComparison.Ordinal)
                    && string.Equals(value.FixedArguments[1].Value as string, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Like the service: <c>module.json</c> with <c>mainAssembly</c> first, else the only <c>.deps.json</c> beside its assembly.
        /// </summary>
        private static string? MainAssemblyOf(string root, List<string> problems)
        {
            var manifest = Path.Combine(root, ManifestFileName);
            if (File.Exists(manifest))
            {
                var match = MainAssemblyEntry.Match(File.ReadAllText(manifest));
                if (match.Success && !string.IsNullOrWhiteSpace(match.Groups["name"].Value))
                {
                    var named = Regex.Unescape(match.Groups["name"].Value);
                    if (File.Exists(Path.Combine(root, named)))
                    {
                        return named;
                    }

                    problems.Add($"{ManifestFileName} names the main assembly '{named}', which is not in the publish folder.");
                    return null;
                }
            }

            var deps = Directory.GetFiles(root, "*.deps.json");
            if (deps.Length == 1)
            {
                var name = Path.GetFileName(deps[0]);
                var main = name.Substring(0, name.Length - ".deps.json".Length) + ".dll";
                if (File.Exists(Path.Combine(root, main)))
                {
                    return main;
                }

                problems.Add($"{name} has no assembly {main} beside it.");
                return null;
            }

            problems.Add(deps.Length == 0
                ? "The module names no main assembly: it needs exactly one .deps.json (publish a plugin project with EnableDynamicLoading=true) or a module.json with mainAssembly."
                : $"The module has {deps.Length} .deps.json files ({string.Join(", ", deps.Select(Path.GetFileName))}); name the main assembly in a module.json with mainAssembly.");
            return null;
        }

        private static string RelativePath(string root, string file)
        {
            var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? file.Substring(prefix.Length) : Path.GetFileName(file);
        }

        private static bool IsAssemblyMetadataAttribute(MetadataReader metadata, CustomAttribute attribute)
        {
            StringHandle name;
            StringHandle ns;
            switch (attribute.Constructor.Kind)
            {
                case HandleKind.MemberReference:
                    var member = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                    if (member.Parent.Kind != HandleKind.TypeReference)
                    {
                        return false;
                    }

                    var reference = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
                    name = reference.Name;
                    ns = reference.Namespace;
                    break;
                case HandleKind.MethodDefinition:
                    var method = metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
                    var definition = metadata.GetTypeDefinition(method.GetDeclaringType());
                    name = definition.Name;
                    ns = definition.Namespace;
                    break;
                default:
                    return false;
            }

            return metadata.StringComparer.Equals(name, "AssemblyMetadataAttribute") && metadata.StringComparer.Equals(ns, "System.Reflection");
        }

        /// <summary>
        /// Decodes the two string arguments of <c>AssemblyMetadataAttribute</c>; nothing else is needed.
        /// </summary>
        private sealed class StringOnlyDecoder : ICustomAttributeTypeProvider<object?>
        {
            public object? GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode;

            public object? GetSystemType() => typeof(Type);

            public object? GetSZArrayType(object? elementType) => null;

            public object? GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => null;

            public object? GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => null;

            public object? GetTypeFromSerializedName(string name) => null;

            public PrimitiveTypeCode GetUnderlyingEnumType(object? type) => PrimitiveTypeCode.Int32;

            public bool IsSystemType(object? type) => type is Type;
        }
    }

    /// <summary>
    /// What <see cref="ModulePackager.Pack"/> wrote.
    /// </summary>
    public sealed class ModulePackageResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModulePackageResult"/> class
        /// </summary>
        public ModulePackageResult(string packagePath, string mainAssembly, string sha256, long size, int fileCount, IReadOnlyList<string> leftOutContracts,
            IReadOnlyList<string>? warnings = null, IReadOnlyList<string>? plugins = null)
        {
            Warnings = warnings ?? Array.Empty<string>();
            Plugins = plugins ?? Array.Empty<string>();
            PackagePath = packagePath;
            MainAssembly = mainAssembly;
            Sha256 = sha256;
            Size = size;
            FileCount = fileCount;
            LeftOutContracts = leftOutContracts;
        }

        /// <summary>What did not stop the publish but should be looked at (e.g. no plugin mark at all).</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>The plugins the module offers (its ModulePlugin marks), one line each.</summary>
        public IReadOnlyList<string> Plugins { get; }

        /// <summary>Contract assemblies in the publish output that were left out (relative paths); the host provides them.</summary>
        public IReadOnlyList<string> LeftOutContracts { get; }

        /// <summary>The written zip.</summary>
        public string PackagePath { get; }

        /// <summary>The main assembly the service will load.</summary>
        public string MainAssembly { get; }

        /// <summary>SHA-256 of the zip, lower-case hex.</summary>
        public string Sha256 { get; }

        /// <summary>Size of the zip in bytes.</summary>
        public long Size { get; }

        /// <summary>Number of files packed.</summary>
        public int FileCount { get; }
    }

    /// <summary>
    /// The publish output can not be packed as a module.
    /// </summary>
    public sealed class ModulePackageException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModulePackageException"/> class
        /// </summary>
        public ModulePackageException(IReadOnlyList<string> problems)
            : base("The module can not be packed:" + Environment.NewLine + string.Join(Environment.NewLine, problems.Select(p => " - " + p)))
        {
            Problems = problems;
        }

        /// <summary>Each reason on its own.</summary>
        public IReadOnlyList<string> Problems { get; }
    }
}
