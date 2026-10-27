using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models
{
    /// <summary>
    /// An additional form-parameter that is sent with the token-request of an external service (e.g. client_id or organization)
    /// </summary>
    /// <remarks>
    /// Intentionally non-generic: a generic child would require a new type-parameter on all security-context types and would
    /// thereby shift the arity of every type that carries the context generically (invalidating seeded trust-entries).
    /// It has no navigation back to the service and therefore no tenant-filter of its own - always access it through the
    /// (filtered) ExternalOAuthService.TokenParameters navigation.
    /// </remarks>
    [Table("ExternalOAuthServiceParameters")]
    [Index(nameof(OAuthServiceId), nameof(ParameterName), IsUnique = true, Name = "UQ_ExternalOAuthServiceParameter")]
    public class ExternalOAuthServiceParameter
    {
        [Key]
        public int OAuthServiceParameterId { get; set; }

        public int OAuthServiceId { get; set; }

        [Required, MaxLength(255)]
        public string ParameterName { get; set; }

        /// <summary>
        /// The value. Encrypted when <see cref="IsSecret"/> is set - with the same key as the service's ClientSecret
        /// (the system key for a global service, the tenant's key otherwise).
        /// </summary>
        public string ParameterValue { get; set; }

        /// <summary>
        /// Whether <see cref="ParameterValue"/> is a secret (e.g. a client_secret for the password grant). Secret values
        /// are stored encrypted, never shown again in the admin views, and decrypted only right before the token request.
        /// Not every parameter is a secret (audience, resource, organization), hence a flag and not blanket encryption.
        /// </summary>
        public bool IsSecret { get; set; }
    }
}
