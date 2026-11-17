using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITVComponents.UpdateService.Abstractions.Feed
{
    /// <summary>
    /// The signature of a manifest, delivered next to it (<c>stable.json.sig</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// It signs the manifest byte for byte, as it is delivered - no canonical form, so the manifest can be a static file.
    /// It carries the signer's certificate (public part only): the service trusts it by its thumbprint, which is fixed in
    /// its configuration. No certificate chain is needed; a self-issued certificate is enough.
    /// </para>
    /// <para>
    /// RSA keys sign with PSS and SHA-256, EC keys with ECDSA and SHA-256.
    /// </para>
    /// </remarks>
    public sealed class FeedSignature
    {
        /// <summary>The algorithm of an RSA key.</summary>
        public const string RsaPssSha256 = "RSA-PSS-SHA256";

        /// <summary>The algorithm of an EC key.</summary>
        public const string EcdsaSha256 = "ECDSA-SHA256";

        /// <summary>Gets or sets the algorithm.</summary>
        [JsonPropertyName("algorithm")]
        public string? Algorithm { get; set; }

        /// <summary>Gets or sets the signer's certificate (DER, Base64), without private key.</summary>
        [JsonPropertyName("certificate")]
        public string? Certificate { get; set; }

        /// <summary>Gets or sets the signature (Base64).</summary>
        [JsonPropertyName("signature")]
        public string? Signature { get; set; }

        /// <summary>
        /// Signs a manifest.
        /// </summary>
        /// <param name="manifest">the manifest exactly as it is delivered</param>
        /// <param name="certificate">a certificate with private key (RSA or EC)</param>
        /// <returns>the content of the <c>.sig</c> file</returns>
        public static string Sign(byte[] manifest, X509Certificate2 certificate)
        {
            if (!certificate.HasPrivateKey)
            {
                throw new ArgumentException("The certificate to sign the feed with has no private key.", nameof(certificate));
            }

            var signature = new FeedSignature { Certificate = Convert.ToBase64String(certificate.Export(X509ContentType.Cert)) };
            using (var rsa = certificate.GetRSAPrivateKey())
            {
                if (rsa != null)
                {
                    signature.Algorithm = RsaPssSha256;
                    signature.Signature = Convert.ToBase64String(rsa.SignData(manifest, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
                    return JsonSerializer.Serialize(signature);
                }
            }

            using (var ecdsa = certificate.GetECDsaPrivateKey())
            {
                if (ecdsa != null)
                {
                    signature.Algorithm = EcdsaSha256;
                    signature.Signature = Convert.ToBase64String(ecdsa.SignData(manifest, HashAlgorithmName.SHA256));
                    return JsonSerializer.Serialize(signature);
                }
            }

            throw new ArgumentException("The certificate to sign the feed with has neither an RSA nor an EC key.", nameof(certificate));
        }

        /// <summary>
        /// Checks the signature of a manifest.
        /// </summary>
        /// <param name="manifest">the manifest exactly as it was received</param>
        /// <param name="signatureFile">the content of the <c>.sig</c> file</param>
        /// <param name="trustedThumbprints">the thumbprints of the certificates the signer may have</param>
        /// <param name="reason">why the signature is not accepted</param>
        /// <returns>whether the manifest is signed by a trusted certificate and unchanged</returns>
        public static bool Verify(byte[] manifest, string signatureFile, IEnumerable<string> trustedThumbprints, out string? reason)
        {
            reason = null;
            FeedSignature? signature;
            try
            {
                signature = JsonSerializer.Deserialize<FeedSignature>(signatureFile);
            }
            catch (JsonException ex)
            {
                reason = $"The signature file is not readable: {ex.Message}";
                return false;
            }

            if (signature?.Certificate == null || signature.Signature == null || signature.Algorithm == null)
            {
                reason = "The signature file is incomplete.";
                return false;
            }

            X509Certificate2 certificate;
            byte[] signed;
            try
            {
                certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(signature.Certificate));
                signed = Convert.FromBase64String(signature.Signature);
            }
            catch (Exception ex) when (ex is FormatException || ex is CryptographicException)
            {
                reason = $"The signature file carries no valid certificate or signature: {ex.Message}";
                return false;
            }

            using (certificate)
            {
                var trusted = trustedThumbprints.Select(t => t.Replace(" ", string.Empty)).ToArray();
                if (!trusted.Any(t => string.Equals(t, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase)))
                {
                    reason = $"The feed is signed by '{certificate.Subject}' ({certificate.Thumbprint}), which is not a trusted feed signer.";
                    return false;
                }

                bool valid;
                switch (signature.Algorithm)
                {
                    case RsaPssSha256:
                        using (var rsa = certificate.GetRSAPublicKey())
                        {
                            valid = rsa != null && rsa.VerifyData(manifest, signed, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
                        }

                        break;
                    case EcdsaSha256:
                        using (var ecdsa = certificate.GetECDsaPublicKey())
                        {
                            valid = ecdsa != null && ecdsa.VerifyData(manifest, signed, HashAlgorithmName.SHA256);
                        }

                        break;
                    default:
                        reason = $"The signature algorithm '{signature.Algorithm}' is not supported.";
                        return false;
                }

                if (!valid)
                {
                    reason = "The signature does not match the manifest: it was changed after signing.";
                }

                return valid;
            }
        }
    }

    /// <summary>
    /// Signs manifests where they are produced (the web).
    /// </summary>
    public interface IFeedSigner
    {
        /// <summary>
        /// Signs a manifest.
        /// </summary>
        /// <param name="manifest">the manifest exactly as it will be delivered</param>
        /// <returns>the content of the <c>.sig</c> file</returns>
        string Sign(byte[] manifest);
    }

    /// <summary>
    /// Signs with a certificate from a PFX file. The password belongs in the secrets of the host, not in its configuration;
    /// the certificate does not belong in the database, or every backup would carry it.
    /// </summary>
    public sealed class CertificateFeedSigner : IFeedSigner, IDisposable
    {
        private readonly X509Certificate2 certificate;

        /// <summary>
        /// Loads the certificate from a PFX file.
        /// </summary>
        public CertificateFeedSigner(string pfxPath, string? password)
            : this(X509CertificateLoader.LoadPkcs12FromFile(pfxPath, password, X509KeyStorageFlags.EphemeralKeySet))
        {
        }

        /// <summary>
        /// Uses a certificate that is already loaded (e.g. from a key vault).
        /// </summary>
        public CertificateFeedSigner(X509Certificate2 certificate)
        {
            if (!certificate.HasPrivateKey)
            {
                throw new ArgumentException("The certificate to sign the feed with has no private key.", nameof(certificate));
            }

            this.certificate = certificate;
        }

        /// <summary>
        /// Gets the thumbprint to put into the configuration of the update services.
        /// </summary>
        public string Thumbprint => certificate.Thumbprint;

        /// <inheritdoc />
        public string Sign(byte[] manifest) => FeedSignature.Sign(manifest, certificate);

        /// <inheritdoc />
        public void Dispose() => certificate.Dispose();
    }
}
