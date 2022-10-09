using Microsoft.AspNetCore.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Impl
{
    public class AuthenticationRegistrationMethodAttribute: CustomConfiguratorAttribute
    {
        public AuthenticationRegistrationMethodAttribute() : base(typeof(AuthenticationBuilder))
        {
        }
    }
}
