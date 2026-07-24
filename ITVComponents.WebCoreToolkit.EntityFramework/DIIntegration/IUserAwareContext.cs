using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DIIntegration
{
    [ExplicitlyExpose]
    public interface IUserAwareContext
    {
        /// <summary>
        /// Gets the userName of the currently logged-in user
        /// </summary>
        string CurrentUserName { get; }

        /// <summary>
        /// Gets the name of the currently active tenant (unique per tenant), or null when no tenant is
        /// in scope. Lets a context outside the WebCoreToolkit (e.g. the workflow store) read the current
        /// tenant from an injected security context without depending on the web/security stack.
        /// </summary>
        string CurrentTenant { get; }
    }
}
