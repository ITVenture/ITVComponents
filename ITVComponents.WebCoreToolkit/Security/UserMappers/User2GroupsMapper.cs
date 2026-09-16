using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Options;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Security.UserMappers
{
    /// <summary>
    /// Maps the user to the names in the group-collection in the claim
    /// </summary>
    internal class User2GroupsMapper : IUserNameMapper, IPlugin
    {
        private readonly User2GroupsMappingOptions options;
        private readonly string groupClaim;

        // Die Einspeisung bleibt, damit die DI-Signatur unveraendert ist; ausgewertet wird nichts
        // mehr (siehe UserMappingOptions.MapApplicationId).
        public User2GroupsMapper(IOptions<User2GroupsMappingOptions> options, IOptions<UserMappingOptions> userMappingOptions)
        {
            this.options = options.Value;
        }

        public User2GroupsMapper(string groupClaim)
        {
            this.groupClaim = groupClaim;
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets all labels for the given Identity
        /// </summary>
        /// <param name="user">the user for which to get all labels</param>
        /// <returns>a list of labels that are assigned to the given user</returns>
        public string[] GetUserLabels(IIdentity user)
        {
            List<string> retVal = new List<string>();
            if (!string.IsNullOrEmpty(user?.Name))
            {
                retVal.Add(user.Name);
            }

            if (user is ClaimsIdentity identity)
            {
                if (identity.Claims != null)
                {
                    var userClaim = options?.GetDefaultClaimFor(identity.AuthenticationType) ?? groupClaim;
                    foreach (var group in identity.Claims.Where(c => c.Type == userClaim))
                    {
                        retVal.Add(group.Value);
                    }

                    // KEIN Schalter mehr davor - und auch nicht mehr doppelt geschachtelt (dieselbe
                    // Bedingung stand hier zweimal ineinander). Die Wicklung entsteht ohnehin nur, wenn
                    // der Anspruch da ist, und den setzt allein, wer bewusst Anwendungs-Zugaenge
                    // einschaltet. Der frueher hier stehende UserMappingOptions.MapApplicationId wurde
                    // an genau EINER Stelle gesetzt - im BEARER-Zweig von WebPartInit -, womit ein per
                    // X-Api-Key angemeldetes Geraet ohne Bearer-Konfiguration rechtelos blieb.
                    retVal.AddRange(from t in identity.Claims.Where(n => n.Type == ClaimTypes.ClientAppAccess) select string.Format(Global.AppUserKeyIndicatorFormat, t.Value));
                }
            }

            return retVal.ToArray();
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {

        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
