using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ITVComponents.UpdateService.Abstractions.Feed
{
    /// <summary>
    /// The manifest of one channel of a product: every package the service may install.
    /// </summary>
    /// <remarks>
    /// Delivered as a static file or by an endpoint (<c>stable.json</c>, <c>beta.json</c>), optionally signed next to it
    /// (<c>stable.json.sig</c>). The base package is one entry of the list.
    /// </remarks>
    public sealed class UpdateFeed
    {
        /// <summary>
        /// Gets or sets the channel the manifest is for.
        /// </summary>
        [JsonPropertyName("channel")]
        public string? Channel { get; set; }

        /// <summary>
        /// Gets or sets when the manifest was generated.
        /// </summary>
        [JsonPropertyName("generated")]
        public DateTimeOffset? Generated { get; set; }

        /// <summary>
        /// Gets or sets the packages.
        /// </summary>
        [JsonPropertyName("packages")]
        public List<UpdatePackage> Packages { get; set; } = new List<UpdatePackage>();
    }

    /// <summary>
    /// One release of one package.
    /// </summary>
    public sealed class UpdatePackage
    {
        /// <summary>
        /// Gets or sets the id of the package (<c>MiniStore.PosAgent</c>).
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets how the package is installed: <see cref="PackageKinds.Base"/> or <see cref="PackageKinds.Module"/>.
        /// </summary>
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        /// <summary>
        /// Gets or sets the version (<c>1.4.2</c>).
        /// </summary>
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets where to download the package from, relative to the manifest or absolute (HTTPS only).
        /// </summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the SHA-256 of the package, hexadecimal. Mandatory: without a matching hash nothing is installed.
        /// </summary>
        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        /// <summary>
        /// Gets or sets the size of the package in bytes.
        /// </summary>
        [JsonPropertyName("size")]
        public long Size { get; set; }

        /// <summary>
        /// Gets or sets when the release was published. The longest deferral counts from here.
        /// </summary>
        [JsonPropertyName("published")]
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the release must be installed right away (short deadline for the application).
        /// </summary>
        [JsonPropertyName("urgent")]
        public bool Urgent { get; set; }

        /// <summary>
        /// Gets or sets the oldest version that may keep running. Who is older gets the release as urgent.
        /// </summary>
        [JsonPropertyName("minimumVersion")]
        public string? MinimumVersion { get; set; }

        /// <summary>
        /// Gets or sets the packages this one requires, with a version range in NuGet notation (<c>[1.4.0,2.0.0)</c>).
        /// </summary>
        [JsonPropertyName("requires")]
        public Dictionary<string, string>? Requires { get; set; }
    }

    /// <summary>
    /// The kinds of packages.
    /// </summary>
    public static class PackageKinds
    {
        /// <summary>
        /// The application itself (and the service), installed by an MSI.
        /// </summary>
        public const string Base = "base";

        /// <summary>
        /// An extension of the application (plugins), unpacked into its own folder.
        /// </summary>
        public const string Module = "module";
    }
}
