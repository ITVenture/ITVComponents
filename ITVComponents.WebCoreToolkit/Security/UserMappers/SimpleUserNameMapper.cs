using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Security.UserMappers
{
    /// <summary>
    /// Implements a default UserNameMapper that only returns the name of the underlaying identity user
    /// </summary>
    public class SimpleUserNameMapper:IUserNameMapper, IPlugin
    {
        private readonly UserMappingOptions userMappingOptions;

        public SimpleUserNameMapper(IOptions<UserMappingOptions> userMappingOptions)
        {
            this.userMappingOptions = userMappingOptions.Value;
        }

        public SimpleUserNameMapper()
        {
            userMappingOptions = new UserMappingOptions();
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets all labels for the given principaluser
        /// </summary>
        /// <param name="user">the user for which to get all labels</param>
        /// <returns>a list of labels that are assigned to the given user</returns>
        public string[] GetUserLabels(IIdentity user)
        {
            var retVal = new List<string>();
            if (!string.IsNullOrEmpty(user?.Name))
            {
                retVal.Add(user.Name);
            }

            if (user is ClaimsIdentity identity && userMappingOptions.MapApplicationId)
            {
                retVal.AddRange(from t in identity.Claims.Where(n => n.Type == ClaimTypes.ClientAppUser) select string.Format(Global.AppUserKeyIndicatorFormat, t.Value));
            }

            return retVal.ToArray();
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
