using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models
{
    public class WebPlugin
    {
        [MaxLength(300)]
        [Required]
        public string UniqueName { get; set; }

        [MaxLength(8192)]
        public string Constructor { get; set; }

        public bool AutoLoad { get; set; }

        public bool Transient { get; set; } = false;

        /// <summary>
        /// Whether this plugin may be loaded WITHOUT an authenticated user. Off by default: the permission
        /// check that gates plugin loading also asserts that somebody is signed in, and this flag is the
        /// explicit, per-plugin exception to that (needed e.g. for anonymous onboarding).
        /// </summary>
        /// <remarks>
        /// Only meaningful on GLOBAL plugins. A tenant-scoped plugin can never be reached anonymously -
        /// without a signed-in user there is no tenant membership to select it by - so the derived entity
        /// forces this to <c>false</c> whenever a tenant owns the row, and the editor only offers it for
        /// global plugins.
        /// </remarks>
        public virtual bool AllowAnonymous { get; set; } = false;

        [MaxLength(8192)]
        public string StartupRegistrationConstructor { get; set; }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(UniqueName))
            {
                return $"{UniqueName} (Auto-Load {(AutoLoad?"enabled":"disabled")})";
            }

            return base.ToString();
        }
    }
}
