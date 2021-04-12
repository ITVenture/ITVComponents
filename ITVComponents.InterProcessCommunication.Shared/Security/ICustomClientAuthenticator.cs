using System.IdentityModel.Selectors;

namespace ITVComponents.InterProcessCommunication.Shared.Security
{
    public interface ICustomClientAuthenticator
    {
        /// <summary>
        /// Gets a value indicating whether this ClientAuthenticator is able to provide credentials or only a certificate validator
        /// </summary>
        bool HasCredentials { get; }

        /// <summary>
        /// Gets a custom X509Certificate Validator that can be used to verify client certificates
        /// </summary>
        X509CertificateValidator ServerCertValidator { get; }

        /// <summary>
        /// Requests the credentials for a specific connection
        /// </summary>
        /// <param name="remoteAddress">the remote address</param>
        /// <param name="userName">the userName</param>
        /// <param name="password">the password</param>
        void GetCredentials(string remoteAddress, out string userName, out string password);
    }
}
