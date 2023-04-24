using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITVComponents.Annotations;

namespace ITVComponents.WebCoreToolkit.Helpers
{
    public class EmailAddress
    {
        private readonly IMailDomainVerifier verifier;
        private string email;

        /// <summary>
        /// Gets a value indicating whether the Format is ok
        /// </summary>
        public bool FormatOk { get; private set; }

        /// <summary>
        /// Indicates whether the domain-check was successful.
        /// </summary>
        public bool DomainOk { get; private set; }

        /// <summary>
        /// Gets the verified e-mail address
        /// </summary>
        public string Email => email;

        /// <summary>
        /// Gets the Domain-Name part of this e-mail address object
        /// </summary>
        public string DomainName { get; private set; }

        /// <summary>
        /// Gets the Alias part of this e-mail address object
        /// </summary>
        public string Alias { get; private set; }

        /// <summary>
        /// Initializes a new instance of the EmailAddress class
        /// </summary>
        /// <param name="email">the email-address</param>
        public EmailAddress(string email):this(email,null)
        {
        }

        public EmailAddress(string email, IMailDomainVerifier verifier)
        {
            this.verifier = verifier;
            this.email = email ?? throw new ArgumentNullException(nameof(email));
            VerifyEmail();
        }

        private void VerifyEmail()
        {
            if (string.IsNullOrWhiteSpace(email))
                FormatOk = false;

            try
            {
                // Normalize the domain
                email = Regex.Replace(email, @"(@)(.+)$", DomainMapper,
                    RegexOptions.None, TimeSpan.FromMilliseconds(200));

                // Examines the domain part of the email and normalizes it.
                string DomainMapper(Match match)
                {
                    // Use IdnMapping class to convert Unicode domain names.
                    var idn = new IdnMapping();

                    // Pull out and process domain name (throws ArgumentException on invalid)
                    DomainName = idn.GetAscii(match.Groups[2].Value);

                    return match.Groups[1].Value + DomainName;
                }
            }
            catch (RegexMatchTimeoutException e)
            {
                FormatOk = false;
            }
            catch (ArgumentException e)
            {
                FormatOk = false;
            }

            try
            {
                FormatOk = Regex.IsMatch(email,
                    @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                    RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
            }
            catch (RegexMatchTimeoutException)
            {
                FormatOk = false;
            }

            if (FormatOk)
            {
                Alias = email.Substring(0, email.IndexOf("@"));
                if (verifier != null)
                {
                    DomainOk = verifier.DomainValid(DomainName);
                }
            }
        }
    }
}
