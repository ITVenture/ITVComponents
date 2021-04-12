//#if (NET45 || NET46)
using System;
using System.Diagnostics;
using System.IdentityModel.Selectors;
using System.Security.Cryptography.X509Certificates;
using ITVComponents.Plugins;
#if (NET47 || NET461)
using ITVComponents.Security;
#endif

namespace ITVComponents.InterProcessCommunication.Shared.Security
{
    public class SimpleClientAuthentication : ICustomClientAuthenticator, IPlugin
    {
        /// <summary>
        /// The username and password for all attached connections
        /// </summary>
        private string userName, password;

        private bool hasCredentials;

        /// <summary>
        /// indicates whether to use security on the password
        /// </summary>
        private bool useSecurity;

        /// <summary>
        /// Initializes a new instance of tge SimpleClientAuthentication class
        /// </summary>
        /// <param name="userName">the default username</param>
        /// <param name="password">the default password</param>
        /// <param name="useSecurity">indicates whether to use security</param>
        public SimpleClientAuthentication(string userName, string password, bool useSecurity)
        {
            this.userName = userName;
            this.password = password;
            this.useSecurity = useSecurity;
            hasCredentials = true;
        }

        /// <summary>
        /// Initializes a new instance of tge SimpleClientAuthentication class
        /// </summary>
        /// <param name="userName">the default username</param>
        /// <param name="password">the default password</param>
        /// <param name="useSecurity">indicates whether to use security</param>
        public SimpleClientAuthentication(string userName, string password, bool useSecurity, bool allowAllCertificates)
        {
            this.userName = userName;
            this.password = password;
            this.useSecurity = useSecurity;
            ServerCertValidator = new NaivCertValidator();
            hasCredentials = true;
        }

        /// <summary>
        /// Initializes a new instance of tge SimpleClientAuthentication class
        /// </summary>
        /// <param name="userName">the default username</param>
        /// <param name="password">the default password</param>
        /// <param name="useSecurity">indicates whether to use security</param>
        public SimpleClientAuthentication(string userName, string password, bool useSecurity, X509CertificateValidator customValidator)
        {
            this.userName = userName;
            this.password = password;
            this.useSecurity = useSecurity;
            ServerCertValidator = customValidator;
            hasCredentials = true;
        }

        /// <summary>
        /// Initializes a new instance of tge SimpleClientAuthentication class
        /// </summary>
        public SimpleClientAuthentication(bool allowAllCertificates)
        {
            ServerCertValidator = new NaivCertValidator();
            hasCredentials = false;
        }

        /// <summary>
        /// Initializes a new instance of tge SimpleClientAuthentication class
        /// </summary>
        public SimpleClientAuthentication(X509CertificateValidator customValidator)
        {
            ServerCertValidator = customValidator;
            hasCredentials = false;
        }



        /// <summary>
        /// Gets a custom X509Certificate Validator that can be used to verify client certificates
        /// </summary>
        public X509CertificateValidator ServerCertValidator { get; }

        /// <summary>
        /// 
        /// </summary>
        public bool HasCredentials => hasCredentials;

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Requests the credentials for a specific connection
        /// </summary>
        /// <param name="remoteAddress">the remote address</param>
        /// <param name="userName">the userName</param>
        /// <param name="password">the password</param>
        public void GetCredentials(string remoteAddress, out string userName, out string password)
        {
            userName = this.userName;
            password = this.password;
#if (NET47 || NET461)
            if (useSecurity)
            {
                password = PasswordSecurity.ToInsecureString(PasswordSecurity.DecryptString(password));
            }
#endif
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            userName = null;
            password = null;
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;

        private class NaivCertValidator : X509CertificateValidator
        {
            #region Overrides of X509CertificateValidator

            public override void Validate(X509Certificate2 certificate)
            {
                Debug.WriteLine(certificate.SerialNumber);
            }


            #endregion
        }
    }
}
//#endif