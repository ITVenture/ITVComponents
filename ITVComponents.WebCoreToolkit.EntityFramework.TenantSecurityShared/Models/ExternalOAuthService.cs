using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    [Index(nameof(CalculatedUniqueServiceName), IsUnique = true, Name="UQOAuthService")]
    public class ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TTenant : Tenant
        where TExternalOAuthService: ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceState: ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    {
        [Key]
        public int OAuthServiceId { get; set; }

        [MaxLength(255)]
        public string UniqueConnectionName { get; set; }

        public string AuthorizationEndpoint { get; set; } = null!;
        public string TokenEndpoint { get; set; } = null!;
        public string RevocationEndpoint { get; set; } = null!;
        public string ClientId { get; set; } = null!;
        public string ClientSecret { get; set; } = null!;

        public string Scope { get; set; } = null!;

        public bool Global { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed), MaxLength(1024), Required]
        public string CalculatedUniqueServiceName { get; set; }

        public int? TenantId { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual TTenant Tenant { get; set; }

        public virtual IList<TExternalOAuthServiceState> ServiceStates { get; set; } = new List<TExternalOAuthServiceState>();
    }
}
