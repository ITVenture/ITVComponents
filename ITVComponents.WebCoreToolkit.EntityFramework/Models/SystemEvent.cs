using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
{
    public class SystemEvent:ITVComponents.WebCoreToolkit.Models.SystemEvent
    {
        [Key]
        public int SystemEventId { get; set; }
    }
}
