using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DIIntegration
{
    public interface IUserAwareContext
    {
        /// <summary>
        /// Gets the userName of the currently logged-in user
        /// </summary>
        string CurrentUserName { get; }
    }
}
