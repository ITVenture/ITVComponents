using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.BackgroundProcessing
{
    /// <summary>
    /// Provides context information for Background-tasks that were invoked from a frontend-request
    /// </summary>
    public class BackgroundTaskContext
    {
        private readonly IContextUserProvider userProvider;

        /// <summary>
        /// Initializes a new instance of the BackgroundTaskContext class
        /// </summary>
        /// <param name="services">the serviceprovider for the current Dependency-Scope</param>
        /// <param name="userProvider">information about the user-context that was used to initiate this background call</param>
        public BackgroundTaskContext(IServiceProvider services, IContextUserProvider userProvider)
        {
            this.userProvider = userProvider;
            Services = services;
        }

        /// <summary>
        /// Gets the Service-Provider for the current background-context
        /// </summary>
        public IServiceProvider Services { get; }

        /// <summary>
        /// Gets the User that has initiated the current background-call
        /// </summary>
        public IPrincipal User => userProvider?.User;

        /// <summary>
        /// Gets the RequestPath of the action where the current background-call was initiated
        /// </summary>
        public string RequestPath => userProvider?.RequestPath;

        /// <summary>
        /// Gets the Route-Dictionary from the action that has lead to the current background-call
        /// </summary>
        public IDictionary<string, object> RouteData => userProvider?.RouteData;
    }
}
