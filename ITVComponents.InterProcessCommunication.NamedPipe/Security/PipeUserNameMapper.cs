using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Security
{
    /// <summary>
    /// The labels of a pipe client, as the security repository looks them up: its account and every group - each by
    /// name and by SID.
    /// </summary>
    /// <remarks>
    /// So the configuration may name an account (<c>MYPC\kasse</c>, <c>NT SERVICE\MyService</c>,
    /// <c>IIS APPPOOL\MiniStore</c>), a group (<c>MYDOMAIN\Kassen</c>) or a SID (<c>S-1-5-4</c> for everyone logged on
    /// interactively, <c>S-1-5-6</c> for services). SIDs are the safe choice for the built-in groups, whose names depend
    /// on the language of Windows.
    /// </remarks>
    public class PipeUserNameMapper : IUserNameMapper, IPlugin
    {
        /// <inheritdoc />
        public string UniqueName { get; set; }

        /// <inheritdoc />
        public event EventHandler Disposed;

        /// <inheritdoc />
        public string[] GetUserLabels(IIdentity user)
        {
            var retVal = new List<string>();
            if (!string.IsNullOrEmpty(user?.Name))
            {
                retVal.Add(user.Name);
            }

            if (user is ClaimsIdentity claims)
            {
                retVal.AddRange(claims.Claims
                    .Where(c => c.Type == ClaimTypes.PrimarySid || c.Type == ClaimTypes.GroupSid || c.Type == PipeClaimTypes.GroupName)
                    .Select(c => c.Value));
            }

            return retVal.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}
