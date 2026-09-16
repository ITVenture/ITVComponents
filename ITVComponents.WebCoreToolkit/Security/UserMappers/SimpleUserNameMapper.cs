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
        // Die Einspeisung bleibt, damit die DI-Signatur unveraendert ist; ausgewertet wird nichts
        // mehr (siehe UserMappingOptions.MapApplicationId).
        public SimpleUserNameMapper(IOptions<UserMappingOptions> userMappingOptions)
        {
        }

        public SimpleUserNameMapper()
        {
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

            // KEIN Schalter mehr davor. Die Wicklung entsteht ohnehin nur, wenn der Anspruch da ist,
            // und den setzt allein, wer bewusst Anwendungs-Zugaenge einschaltet. Der frueher hier
            // stehende UserMappingOptions.MapApplicationId wurde im ganzen Repositorium an genau EINER
            // Stelle gesetzt - im BEARER-Zweig von WebPartInit. Wer sich per X-Api-Key anmeldete und kein
            // Bearer konfiguriert hatte, verlor damit seine Maschinen-Rechte, obwohl der Anspruch stand.
            if (user is ClaimsIdentity identity)
            {
                retVal.AddRange(from t in identity.Claims.Where(n => n.Type == ClaimTypes.ClientAppAccess) select string.Format(Global.AppUserKeyIndicatorFormat, t.Value));
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
