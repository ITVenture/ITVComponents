using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models
{
    public class ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TTenant : Tenant
        where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    {
        [Key]
        public int ExternalOAuthServiceTenantLoginId { get; set; }

        public int TenantId { get; set; }

        public int OAuthServiceId { get; set; }

        public string Token { get; set; }

        public bool Revoked { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual TTenant Tenant { get; set; }

        [ForeignKey(nameof(OAuthServiceId))]
        public virtual TExternalOAuthService OAuthService { get; set; }
    }
}
