using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
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

        public string ParameterValue { get; set; }
    }
}
