using System;
using System.Collections.Generic;
using System.IO;

namespace ITVComponents.UpdateService
{
    /// <summary>
    /// The configuration of an update service. It comes from the service's own configuration, written by its installer -
    /// never from a caller.
    /// </summary>
    public class UpdateServiceOptions
    {
        /// <summary>
        /// Gets or sets the product the service keeps up to date: the id of its base package in the feed.
        /// </summary>
        public string ProductId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the address of the feed folder; the manifest is <c>&lt;FeedUrl&gt;/&lt;Channel&gt;.json</c>.
        /// </summary>
        public string FeedUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the channel. Default: <c>stable</c>.
        /// </summary>
        public string Channel { get; set; } = "stable";

        /// <summary>
        /// Gets or sets a value indicating whether a feed over plain HTTP is accepted. For development only.
        /// </summary>
        public bool AllowInsecureFeed { get; set; }

        /// <summary>
        /// Gets or sets the thumbprints of the certificates the feed may be signed with (two, to change the certificate
        /// without interruption). If set, a signature next to the manifest is checked whenever there is one.
        /// </summary>
        public List<string> FeedSigningThumbprints { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets a value indicating whether the manifest must be signed. Without a signature, the authenticity of
        /// modules rests on HTTPS and the web server alone (the MSI of the base package is always checked by Authenticode).
        /// On in the shop, off in development.
        /// </summary>
        public bool RequireFeedSignature { get; set; }

        /// <summary>
        /// Gets or sets how often to look for a new version, in minutes. Default: 60.
        /// </summary>
        public int CheckIntervalMinutes { get; set; } = 60;

        /// <summary>
        /// Gets or sets where the service keeps its state, downloads and installation logs. Only SYSTEM and the
        /// administrators may write there; the service sets that up.
        /// </summary>
        public string DataDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the thumbprints of the certificates the MSI must be signed with (two, to change the certificate
        /// without interruption). Either this or <see cref="PublisherSubject"/> must be set.
        /// </summary>
        public List<string> PublisherThumbprints { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the subject the signing certificate must have (<c>CN=IT-Venture GmbH, O=..., C=CH</c>), with a
        /// chain Windows trusts.
        /// </summary>
        public string? PublisherSubject { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the revocation of the signing certificate is checked. Needs access to
        /// the certificate authority; off by default for shops with a weak connection.
        /// </summary>
        public bool CheckRevocation { get; set; }

        /// <summary>
        /// Gets or sets the UpgradeCode the MSI must have. A package of another product is refused even if correctly signed.
        /// </summary>
        public string UpgradeCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether an older version than the installed one may be installed (to withdraw a
        /// release). Default: false.
        /// </summary>
        public bool AllowDowngrade { get; set; }

        /// <summary>
        /// Gets or sets the application: what the service starts again after an installation, and whose file version is the
        /// installed version.
        /// </summary>
        public string ApplicationPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the arguments the application is started with.
        /// </summary>
        public string? ApplicationArguments { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether an application may only announce itself if it runs from the folder of
        /// <see cref="ApplicationPath"/>. Default: true.
        /// </summary>
        public bool RequireApplicationPath { get; set; } = true;

        /// <summary>
        /// Gets or sets how long the application has to end for an urgent update, in minutes. Default: 5.
        /// </summary>
        public int UrgentDeadlineMinutes { get; set; } = 5;

        /// <summary>
        /// Gets or sets how long an update may be deferred, counted from its publication, in days. After that it is urgent.
        /// Default: 7.
        /// </summary>
        public int MaximumDeferralDays { get; set; } = 7;

        /// <summary>
        /// Gets or sets the longest single deferral, in minutes. Default: 120.
        /// </summary>
        public int MaximumDeferralMinutes { get; set; } = 120;

        /// <summary>
        /// Gets or sets how long to wait before trying again when another installation is running, in minutes. Default: 10.
        /// </summary>
        public int RetryMinutes { get; set; } = 10;

        /// <summary>
        /// Gets or sets the folder of the modules. Default: <c>modules</c> next to <see cref="ApplicationPath"/>.
        /// </summary>
        public string? ModulesDirectory { get; set; }

        /// <summary>
        /// Gets or sets how long after switching modules an end of the application without a report counts as a crash, in
        /// minutes. Default: 5.
        /// </summary>
        public int ModuleConfirmMinutes { get; set; } = 5;

        /// <summary>
        /// Gets or sets the name of the service at the hub. Default: <c>UpdateService</c>.
        /// </summary>
        public string HubServiceName { get; set; } = "UpdateService";

        /// <summary>
        /// Gets or sets the applications this service keeps up to date (issue ModulePlugins-Instances, D). Feed,
        /// certificates, package store, pipe and hub are shared; everything else is per target. Empty: the single values
        /// above (<see cref="ProductId"/>, <see cref="ApplicationPath"/>, ...) form the one target <c>default</c>.
        /// </summary>
        public List<UpdateTargetOptions> Targets { get; set; } = new List<UpdateTargetOptions>();

        /// <summary>
        /// Gets the target these options were resolved for (<see cref="ResolveTargets"/>). Not configurable.
        /// </summary>
        public string TargetId { get; internal set; } = DefaultTargetId;

        /// <summary>
        /// Gets the kind of the target these options were resolved for (<see cref="UpdateTargetKinds"/>). Not configurable
        /// here; the single values always form a <c>SessionApplication</c>.
        /// </summary>
        public string TargetKind { get; internal set; } = UpdateTargetKinds.SessionApplication;

        /// <summary>
        /// Gets the Windows service of a target of the kind <c>WindowsService</c>. Not configurable here; see
        /// <see cref="UpdateTargetOptions.ServiceName"/>.
        /// </summary>
        public string? ServiceName { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the target is a Windows service.
        /// </summary>
        public bool IsWindowsService => string.Equals(TargetKind, UpdateTargetKinds.WindowsService, StringComparison.Ordinal);

        /// <summary>
        /// The id of the target formed by the single values when <see cref="Targets"/> is empty.
        /// </summary>
        public const string DefaultTargetId = "default";

        /// <summary>
        /// Resolves the targets: one copy of these options per target, with the values of the target in place of the
        /// single values. Without <see cref="Targets"/>, these options themselves are the target <c>default</c>.
        /// </summary>
        /// <exception cref="InvalidOperationException">when the targets are not usable; the message says why</exception>
        public IReadOnlyList<UpdateServiceOptions> ResolveTargets()
        {
            if (Targets == null || Targets.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(ProductId) || string.IsNullOrWhiteSpace(ApplicationPath))
                {
                    throw new InvalidOperationException("The update service needs ProductId and ApplicationPath, or a list of Targets.");
                }

                var single = Copy();
                single.TargetId = DefaultTargetId;
                return new[] { single };
            }

            var resolved = new List<UpdateServiceOptions>();
            foreach (var target in Targets)
            {
                if (target == null || !IsValidTargetId(target.Id))
                {
                    throw new InvalidOperationException($"The target id '{target?.Id}' is not valid: a letter first, then letters, digits, '_', '.' or '-', at most 64 characters.");
                }

                string kind;
                if (string.Equals(target.Kind, UpdateTargetKinds.SessionApplication, StringComparison.OrdinalIgnoreCase))
                {
                    kind = UpdateTargetKinds.SessionApplication;
                }
                else if (string.Equals(target.Kind, UpdateTargetKinds.WindowsService, StringComparison.OrdinalIgnoreCase))
                {
                    kind = UpdateTargetKinds.WindowsService;
                    if (string.IsNullOrWhiteSpace(target.ServiceName))
                    {
                        throw new InvalidOperationException($"Target {target.Id} is a Windows service and needs ServiceName.");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Target {target.Id}: the kind '{target.Kind}' is unknown ({UpdateTargetKinds.SessionApplication} or {UpdateTargetKinds.WindowsService}).");
                }

                if (string.IsNullOrWhiteSpace(target.ProductId) || string.IsNullOrWhiteSpace(target.ApplicationPath) || string.IsNullOrWhiteSpace(target.UpgradeCode))
                {
                    throw new InvalidOperationException($"Target {target.Id} needs ProductId, ApplicationPath and UpgradeCode.");
                }

                var options = Copy();
                options.TargetId = target.Id;
                options.TargetKind = kind;
                options.ServiceName = kind == UpdateTargetKinds.WindowsService ? target.ServiceName!.Trim() : null;
                options.ProductId = target.ProductId;
                options.ApplicationPath = target.ApplicationPath;
                options.ApplicationArguments = target.ApplicationArguments;
                options.UpgradeCode = target.UpgradeCode;
                options.ModulesDirectory = target.ModulesDirectory;
                options.RequireApplicationPath = target.RequireApplicationPath ?? RequireApplicationPath;
                options.Targets = new List<UpdateTargetOptions>();
                foreach (var other in resolved)
                {
                    if (string.Equals(other.TargetId, options.TargetId, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"The target id {target.Id} is used twice.");
                    }

                    // One MSI for two targets would update the second one without asking it to end first.
                    if (Guid.TryParse(other.UpgradeCode, out var a) && Guid.TryParse(options.UpgradeCode, out var b) ? a == b : string.Equals(other.UpgradeCode, options.UpgradeCode, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Targets {other.TargetId} and {target.Id} have the same UpgradeCode; targets that share an MSI are not supported yet.");
                    }

                    // The folder decides which target an application belongs to.
                    if (string.Equals(FolderOf(other.ApplicationPath), FolderOf(options.ApplicationPath), StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Targets {other.TargetId} and {target.Id} run from the same folder; each target needs its own.");
                    }

                    if (options.ServiceName != null && string.Equals(other.ServiceName, options.ServiceName, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Targets {other.TargetId} and {target.Id} name the same service {options.ServiceName}.");
                    }
                }

                resolved.Add(options);
            }

            return resolved;
        }

        /// <summary>
        /// Indicates whether a target id can be used as a folder name and in logs.
        /// </summary>
        public static bool IsValidTargetId(string? id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 64 || !char.IsAsciiLetter(id[0]))
            {
                return false;
            }

            foreach (var c in id)
            {
                if (!char.IsAsciiLetterOrDigit(c) && c != '_' && c != '.' && c != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static string FolderOf(string applicationPath)
        {
            return Path.GetFullPath(Path.GetDirectoryName(applicationPath) ?? applicationPath).TrimEnd(Path.DirectorySeparatorChar);
        }

        private UpdateServiceOptions Copy() => (UpdateServiceOptions)MemberwiseClone();
    }

    /// <summary>
    /// The kinds of targets (issue ModulePlugins-Instances, D).
    /// </summary>
    public static class UpdateTargetKinds
    {
        /// <summary>An application that runs in the Windows session of a user.</summary>
        public const string SessionApplication = "SessionApplication";

        /// <summary>
        /// A Windows service: started and stopped with the service control manager, one instance, no sessions. It may only
        /// register from the process the service control manager names for <see cref="UpdateTargetOptions.ServiceName"/>.
        /// </summary>
        public const string WindowsService = "WindowsService";
    }

    /// <summary>
    /// One application the update service keeps up to date. Every value here replaces the single value of the same name
    /// in <see cref="UpdateServiceOptions"/>.
    /// </summary>
    public class UpdateTargetOptions
    {
        /// <summary>Gets or sets the id: the folder of the target below <c>&lt;DataDirectory&gt;\targets</c>.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the kind (<see cref="UpdateTargetKinds"/>). Default: <c>SessionApplication</c>.</summary>
        public string Kind { get; set; } = UpdateTargetKinds.SessionApplication;

        /// <summary>See <see cref="UpdateServiceOptions.ProductId"/>.</summary>
        public string ProductId { get; set; } = string.Empty;

        /// <summary>See <see cref="UpdateServiceOptions.ApplicationPath"/>.</summary>
        public string ApplicationPath { get; set; } = string.Empty;

        /// <summary>See <see cref="UpdateServiceOptions.ApplicationArguments"/>.</summary>
        public string? ApplicationArguments { get; set; }

        /// <summary>See <see cref="UpdateServiceOptions.UpgradeCode"/>.</summary>
        public string UpgradeCode { get; set; } = string.Empty;

        /// <summary>See <see cref="UpdateServiceOptions.ModulesDirectory"/>.</summary>
        public string? ModulesDirectory { get; set; }

        /// <summary>See <see cref="UpdateServiceOptions.RequireApplicationPath"/>; null takes the shared value.</summary>
        public bool? RequireApplicationPath { get; set; }

        /// <summary>Gets or sets the name of the Windows service, for the kind <c>WindowsService</c>.</summary>
        public string? ServiceName { get; set; }
    }
}
