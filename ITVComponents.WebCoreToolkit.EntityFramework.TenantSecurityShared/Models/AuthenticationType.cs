using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    [Index(nameof(AuthenticationTypeName),IsUnique=true, Name="IX_UniqueAuthenticationType")]
    public class AuthenticationType
    {
        [Key]
        public int AuthenticationTypeId { get; set; }

        [Required, MaxLength(512)]
        public string AuthenticationTypeName { get; set; }

        public virtual ICollection<AuthenticationClaimMapping> ClaimMappings { get; set; } = new List<AuthenticationClaimMapping>();
    }
}
