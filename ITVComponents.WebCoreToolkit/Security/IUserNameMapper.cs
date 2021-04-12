using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security
{
    /// <summary>
    /// Maps a physical user to one or multiple labels
    /// </summary>
    public interface IUserNameMapper
    {
        /// <summary>
        /// Gets all labels for the given principaluser
        /// </summary>
        /// <param name="user">the user for which to get all labels</param>
        /// <returns>a list of labels that are assigned to the given user</returns>
        string[] GetUserLabels(IPrincipal user);

        /// <summary>
        /// Gets all labels for the given Identity
        /// </summary>
        /// <param name="user">the user for which to get all labels</param>
        /// <returns>a list of labels that are assigned to the given user</returns>
        string[] GetUserLabels(IIdentity user);
    }
}
