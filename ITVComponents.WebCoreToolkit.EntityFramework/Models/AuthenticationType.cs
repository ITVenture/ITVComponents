using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
{
    [Index(nameof(AuthenticationTypeName),IsUnique=true, Name="IX_UniqueAuthenticationType")]
    public class AuthenticationType
    {
        [Key]
        public int AuthenticationTypeId { get; set; }

        [Required, MaxLength(512)]
        public string AuthenticationTypeName { get; set; }
    }
}
