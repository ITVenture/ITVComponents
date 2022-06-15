using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Security.ClaimsTransformation
{
    public class CollectedClaimsTransform:IClaimsTransformation
    {
        /// <summary>
        /// Provices injected services
        /// </summary>
        private readonly IServiceScopeFactory serviceFactory;

        /// <summary>
        /// Initializes a new instance of the CollectedClaimsTransform class
        /// </summary>
        /// <param name="services">the services that were injected into the DI</param>
        public CollectedClaimsTransform(IServiceScopeFactory serviceFactory)
        {
            this.serviceFactory = serviceFactory;
        }

        /// <summary>
        /// Provides a central transformation point to change the specified principal.
        /// Note: this will be run on each AuthenticateAsync call, so its safer to
        /// return a new ClaimsPrincipal if your transformation is not idempotent.
        /// </summary>
        /// <param name="principal">The <see cref="T:System.Security.Claims.ClaimsPrincipal" /> to transform.</param>
        /// <returns>The transformed principal.</returns>
        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            var retVal = principal;
            using var scp = serviceFactory.CreateScope();
            var transforms = scp.ServiceProvider.GetServices<ICollectedClaimsProvider>();
            foreach(var trans in transforms)
            {
                retVal = await trans.TransformAsync(retVal);
            }
            
            return retVal;
        }
    }
}
