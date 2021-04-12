using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models
{
    [Serializable]
    public class WebPlugin
    {
        [MaxLength(50)]
        [Required]
        public string UniqueName { get; set; }

        [MaxLength(2048)]
        public string Constructor { get; set; }

        public bool AutoLoad { get; set; }

        [MaxLength(2048)]
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
