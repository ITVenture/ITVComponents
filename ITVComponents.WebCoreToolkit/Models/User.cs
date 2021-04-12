using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Models
{
    public class User
    {
        [MaxLength(150)]
        [Required]
        public string UserName { get; set; }

        [MaxLength(255), Required]
        public string AuthenticationType { get;set; }
    }
}
