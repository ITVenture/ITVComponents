using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models
{
    [PrimaryKey(nameof(Key),nameof(ValidThrough))]
    public class ServerCookie
    {
        public Guid Key { get; set; }

        public DateTime ValidThrough { get; set; }

        public string Content { get; set; }
    }
}
