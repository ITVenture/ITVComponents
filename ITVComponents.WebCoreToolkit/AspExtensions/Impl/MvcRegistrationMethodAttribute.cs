using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Impl
{
    public class MvcRegistrationMethodAttribute : CustomConfiguratorAttribute
    {
        public MvcRegistrationMethodAttribute() : base(typeof(ApplicationPartManager))
        {
        }
    }
}
