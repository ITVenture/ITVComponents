using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.UpdateService.Feed;
using ITVComponents.UpdateService.Native;

namespace ITVComponents.UpdateService.Packages
{
    /// <summary>
    /// Decides whether a downloaded package may be installed.
    /// </summary>
    public interface IPackageVerifier
    {
        /// <summary>
        /// Checks the package. The reason of a refusal goes to the log and into the state the application shows.
        /// </summary>
        /// <returns>null when the package may be installed, else why not</returns>
        string? Verify(string path, UpdatePackage package);
    }

    /// <summary>
    /// Checks an MSI: a valid Authenticode signature by the configured publisher, the configured UpgradeCode, and the
    /// ProductVersion the manifest announces. A package signed correctly but for another product, or labelled with a
    /// wrong version, does not get through.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class MsiPackageVerifier : IPackageVerifier
    {
        private readonly UpdateServiceOptions options;

        public MsiPackageVerifier(UpdateServiceOptions options)
        {
            this.options = options;
            if (options.PublisherThumbprints.Count == 0 && string.IsNullOrWhiteSpace(options.PublisherSubject))
            {
                throw new InvalidOperationException("The update service needs the expected publisher: PublisherThumbprints or PublisherSubject.");
            }

            if (string.IsNullOrWhiteSpace(options.UpgradeCode))
            {
                throw new InvalidOperationException("The update service needs the UpgradeCode of its product.");
            }
        }

        /// <inheritdoc />
        public string? Verify(string path, UpdatePackage package)
        {
            var trust = UpdateNativeMethods.VerifyAuthenticode(path, options.CheckRevocation);
            if (trust != 0)
            {
                return $"The signature of {package.Id} {package.Version} is missing or not trusted (WinVerifyTrust 0x{trust:X8}).";
            }

            X509Certificate2 signer;
            try
            {
#pragma warning disable SYSLIB0057 // reads the signer of a file that WinVerifyTrust has just checked; no loader replaces it
                signer = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"The signer of {path} could not be read: {ex.OutlineException()}", LogSeverity.Error);
                return $"The signer of {package.Id} {package.Version} could not be read.";
            }

            using (signer)
            {
                var byThumbprint = options.PublisherThumbprints.Any(t => string.Equals(t.Replace(" ", string.Empty), signer.Thumbprint, StringComparison.OrdinalIgnoreCase));
                var bySubject = !string.IsNullOrWhiteSpace(options.PublisherSubject)
                                && string.Equals(Normalize(options.PublisherSubject), Normalize(signer.Subject), StringComparison.OrdinalIgnoreCase);
                if (!byThumbprint && !bySubject)
                {
                    return $"{package.Id} {package.Version} is signed by '{signer.Subject}' ({signer.Thumbprint}), not by the configured publisher.";
                }
            }

            string? upgradeCode;
            string? productVersion;
            try
            {
                upgradeCode = UpdateNativeMethods.ReadMsiProperty(path, "UpgradeCode");
                productVersion = UpdateNativeMethods.ReadMsiProperty(path, "ProductVersion");
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"The MSI properties of {path} could not be read: {ex.OutlineException()}", LogSeverity.Error);
                return $"{package.Id} {package.Version} is not a readable MSI.";
            }

            if (!Guid.TryParse(upgradeCode, out var actualCode) || !Guid.TryParse(options.UpgradeCode, out var expectedCode) || actualCode != expectedCode)
            {
                return $"{package.Id} {package.Version} has the UpgradeCode {upgradeCode ?? "(none)"}, not the one of this product.";
            }

            if (!VersionText.TryParse(productVersion, out var actualVersion) || !VersionText.TryParse(package.Version, out var announced)
                                                                            || VersionText.Compare(actualVersion, announced) != 0)
            {
                return $"{package.Id} contains version {productVersion ?? "(none)"}, but the manifest announces {package.Version}.";
            }

            return null;
        }

        private static string Normalize(string subject)
        {
            return string.Join(",", subject.Split(',').Select(p => p.Trim()));
        }
    }
}
