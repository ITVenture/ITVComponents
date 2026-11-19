#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using ITVComponents.Logging;
using ITVComponents.Plugins.Initialization;

namespace ITVComponents.Plugins.Modules
{
    /// <summary>
    /// One plugin instance of a module in <c>plugins.json</c>: data, never a construction string. The loader of the
    /// application builds the construction string from it after checking every field.
    /// </summary>
    /// <remarks>
    /// The enumerations are kept as text: a value this version does not know makes only this instance unusable, not
    /// the whole file.
    /// </remarks>
    public sealed class ModulePluginInstance
    {
        private static readonly Regex TypeNamePattern = new Regex(@"^[A-Za-z_][A-Za-z0-9_`]*([.+][A-Za-z_][A-Za-z0-9_`]*)*$", RegexOptions.CultureInvariant);

        /// <summary>The name of the instance: the unique name of the plugin.</summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>The id of the module, i.e. its folder below the modules folder.</summary>
        [JsonPropertyName("module")]
        public string? Module { get; set; }

        /// <summary>The file name of the assembly in the module folder.</summary>
        [JsonPropertyName("assembly")]
        public string? Assembly { get; set; }

        /// <summary>The full name of the type; nested types with '+'.</summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /// <summary><c>infrastructure</c> or <c>template</c> (<see cref="ModulePluginKind"/>).</summary>
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        /// <summary>The <see cref="PluginLoadType"/>, as taken from the mark.</summary>
        [JsonPropertyName("loadType")]
        public string? LoadType { get; set; }

        /// <summary>
        /// Creates an instance from a mark.
        /// </summary>
        /// <param name="name">the instance name; for infrastructure the name of the mark</param>
        /// <param name="moduleId">the module</param>
        /// <param name="mark">the mark of the type</param>
        public static ModulePluginInstance FromMark(string name, string moduleId, ModulePluginMark mark)
        {
            return new ModulePluginInstance
            {
                Name = name,
                Module = moduleId,
                Assembly = mark.AssemblyFile,
                Type = mark.TypeName,
                Kind = KindText(mark.Kind),
                LoadType = mark.LoadType.ToString()
            };
        }

        /// <summary>
        /// The text of a kind in the file.
        /// </summary>
        public static string KindText(ModulePluginKind kind) => kind == ModulePluginKind.Infrastructure ? "infrastructure" : "template";

        /// <summary>
        /// Reads the kind.
        /// </summary>
        public bool TryGetKind(out ModulePluginKind kind)
        {
            switch (Kind)
            {
                case "infrastructure":
                    kind = ModulePluginKind.Infrastructure;
                    return true;
                case "template":
                    kind = ModulePluginKind.Template;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }

        /// <summary>
        /// Reads the load type.
        /// </summary>
        public bool TryGetLoadType(out PluginLoadType loadType)
        {
            return Enum.TryParse(LoadType, false, out loadType) && Enum.IsDefined(typeof(PluginLoadType), loadType) && !int.TryParse(LoadType, out _);
        }

        /// <summary>
        /// Checks every field for what may end up in a path or a construction string. Says nothing about whether the
        /// module, the assembly or the type exist.
        /// </summary>
        /// <param name="problem">what is wrong</param>
        /// <returns>whether the instance is well-formed</returns>
        public bool IsWellFormed(out string? problem)
        {
            if (!ModulePluginMarks.IsValidInstanceName(Name))
            {
                problem = $"the name '{Name}' is not valid";
            }
            else if (!IsSafeModuleId(Module))
            {
                problem = $"the module id '{Module}' is not valid";
            }
            else if (string.IsNullOrEmpty(Assembly) || Assembly != Path.GetFileName(Assembly) || Assembly.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                     || Assembly.IndexOfAny(new[] { '[', ']' }) >= 0 || !Assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                problem = $"the assembly '{Assembly}' is not a file name of a DLL";
            }
            else if (Type == null || !TypeNamePattern.IsMatch(Type))
            {
                problem = $"the type '{Type}' is not a type name";
            }
            else if (!TryGetKind(out _))
            {
                problem = $"the kind '{Kind}' is unknown";
            }
            else if (!TryGetLoadType(out _))
            {
                problem = $"the load type '{LoadType}' is unknown";
            }
            else
            {
                problem = null;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Whether a module id can be used as folder name and inside a construction string.
        /// </summary>
        public static bool IsSafeModuleId(string? id)
        {
            return !string.IsNullOrWhiteSpace(id)
                   && id.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                   && id.IndexOfAny(new[] { '[', ']' }) < 0
                   && !id.Contains("..")
                   && !id.EndsWith(".new", StringComparison.OrdinalIgnoreCase)
                   && !id.EndsWith(".old", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether two entries describe the same plugin in the same way.
        /// </summary>
        public bool SameAs(ModulePluginInstance other)
        {
            return string.Equals(Name, other.Name, StringComparison.Ordinal)
                   && string.Equals(Module, other.Module, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(Assembly, other.Assembly, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(Type, other.Type, StringComparison.Ordinal)
                   && string.Equals(Kind, other.Kind, StringComparison.Ordinal)
                   && string.Equals(LoadType, other.LoadType, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override string ToString() => $"{Name} ({Kind} {Type} of module {Module})";
    }

    /// <summary>
    /// The file <c>plugins.json</c>: the plugin instances of the modules of one application. Only the update service
    /// writes it; it lies where only SYSTEM and the administrators may write (issue ModulePlugins-Instances, B).
    /// </summary>
    public sealed class ModulePluginInstanceFile
    {
        /// <summary>The usual file name.</summary>
        public const string FileName = "plugins.json";

        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions { WriteIndented = true };

        /// <summary>The instances.</summary>
        [JsonPropertyName("instances")]
        public List<ModulePluginInstance> Instances { get; set; } = new List<ModulePluginInstance>();

        /// <summary>
        /// Reads the file; an absent file has no instances.
        /// </summary>
        /// <exception cref="JsonException">the file is not readable</exception>
        /// <exception cref="IOException">the file could not be opened</exception>
        public static ModulePluginInstanceFile Read(string path)
        {
            if (!File.Exists(path))
            {
                return new ModulePluginInstanceFile();
            }

            // Shared with a writer that replaces the file: the reader must not block the replacement.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var file = JsonSerializer.Deserialize<ModulePluginInstanceFile>(stream) ?? new ModulePluginInstanceFile();
            file.Instances ??= new List<ModulePluginInstance>();
            file.Instances.RemoveAll(i => i == null);
            return file;
        }

        /// <summary>
        /// Writes the file beside and replaces it, so that a reader never sees half a file.
        /// </summary>
        /// <exception cref="IOException">the file could not be replaced, even after a few tries</exception>
        public void Write(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, WriteOptions));
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    File.Move(temporary, path, true);
                    return;
                }
                catch (IOException ex) when (attempt < 5)
                {
                    // A reader that opened the file without FILE_SHARE_DELETE holds it for a moment; try again. The
                    // last attempt throws to the caller, which reports it.
                    LogEnvironment.LogEvent($"{path} could not be replaced (attempt {attempt}), trying again: {ex.Message}", LogSeverity.Warning);
                    Thread.Sleep(100 * attempt);
                }
            }
        }

        /// <summary>
        /// Gets the instances of a module.
        /// </summary>
        public IEnumerable<ModulePluginInstance> OfModule(string moduleId) => Instances.Where(i => string.Equals(i.Module, moduleId, StringComparison.OrdinalIgnoreCase));
    }
}
