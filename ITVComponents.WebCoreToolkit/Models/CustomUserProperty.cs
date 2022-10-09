using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models
{
    [Serializable]
    public class CustomUserProperty
    {
        [MaxLength(150),Required]
        public string PropertyName{get;set;}

        [Required]
        public string Value { get; set; }
        public CustomUserPropertyType PropertyType { get; set; } = CustomUserPropertyType.Claim;
    }

    public enum CustomUserPropertyType
    {
        Claim,
        Literal,
        Json
    }
}
