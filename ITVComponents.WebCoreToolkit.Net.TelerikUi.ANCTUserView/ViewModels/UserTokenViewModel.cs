using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTenantSecurityUserView.ViewModels
{
    public class UserTokenViewModel
    {
        /// <summary>
        /// Gets or sets the primary key of the user that the token belongs to.
        /// </summary>
        public string UserId { get; set; }
        /// <summary>Gets or sets the LoginProvider this token is from.</summary>
        public string LoginProvider { get; set; }
        /// <summary>Gets or sets the name of the token.</summary>
        public string Name { get; set; }
        /// <summary>Gets or sets the token value.</summary>
        public string Value { get; set; }
    }
}
