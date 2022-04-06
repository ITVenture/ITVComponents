using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTenantSecurityUserView.ViewModels
{
    public class UserClaimViewModel
    {
        /// <summary>Gets or sets the identifier for this user claim.</summary>
        public virtual int Id { get; set; }
        /// <summary>
        /// Gets or sets the primary key of the user associated with this claim.
        /// </summary>
        public virtual string UserId { get; set; }
        /// <summary>Gets or sets the claim type for this claim.</summary>
        public virtual string ClaimType { get; set; }
        /// <summary>Gets or sets the claim value for this claim.</summary>
        public virtual string ClaimValue { get; set; }
    }
}
