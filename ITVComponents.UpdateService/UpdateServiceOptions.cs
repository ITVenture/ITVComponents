using System.Collections.Generic;

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
        /// Gets or sets the name of the service at the hub. Default: <c>UpdateService</c>.
        /// </summary>
        public string HubServiceName { get; set; } = "UpdateService";
    }
}
