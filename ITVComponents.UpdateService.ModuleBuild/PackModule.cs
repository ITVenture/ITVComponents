using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace ITVComponents.UpdateService.ModuleBuild
{
    /// <summary>
    /// MSBuild task: packs a publish folder as a module of the update service (<see cref="ModulePackager"/>). Every
    /// problem becomes a build error of its own, so the publish fails where the module is built - not at the till.
    /// </summary>
    public sealed class PackModule : Task
    {
        /// <summary>The publish output of the module.</summary>
        [Required]
        public string PublishDirectory { get; set; } = string.Empty;

        /// <summary>The zip to write.</summary>
        [Required]
        public string PackagePath { get; set; } = string.Empty;

        /// <summary>
        /// Semicolon-separated file names of contract assemblies the module may bring nevertheless, because its host
        /// does not have them. Usually empty.
        /// </summary>
        public string? AllowedContracts { get; set; }

        /// <summary>SHA-256 of the written zip, lower-case hex.</summary>
        [Output]
        public string? Sha256 { get; set; }

        /// <summary>The main assembly the service will load.</summary>
        [Output]
        public string? MainAssembly { get; set; }

        /// <inheritdoc />
        public override bool Execute()
        {
            var allowed = (AllowedContracts ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(n => n.Trim())
                .Where(n => n.Length != 0)
                .ToArray();
            try
            {
                var result = ModulePackager.Pack(PublishDirectory, PackagePath, allowed);
                Sha256 = result.Sha256;
                MainAssembly = result.MainAssembly;
                if (result.LeftOutContracts.Count != 0)
                {
                    // Expected whenever a referenced project depends on ITVComponents - informational, not a warning.
                    Log.LogMessage(MessageImportance.High, $"Module package: left out the contract assemblies {string.Join(", ", result.LeftOutContracts)}; the host provides them. List one in ItvModuleAllowedContracts if the host does not have it.");
                }

                Log.LogMessage(MessageImportance.High, $"Module package: {result.PackagePath} ({result.FileCount} files, {result.Size:N0} bytes, main assembly {result.MainAssembly}, SHA-256 {result.Sha256})");
                return true;
            }
            catch (ModulePackageException ex)
            {
                foreach (var problem in ex.Problems)
                {
                    Log.LogError(null, "ITVMOD001", null, PublishDirectory, 0, 0, 0, 0, problem);
                }

                return false;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.LogError(null, "ITVMOD002", null, PackagePath, 0, 0, 0, 0, $"The module package could not be written: {ex}");
                return false;
            }
        }
    }
}
