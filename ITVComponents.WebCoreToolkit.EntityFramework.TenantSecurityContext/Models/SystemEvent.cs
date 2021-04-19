using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class SystemEvent:ITVComponents.WebCoreToolkit.Models.SystemEvent
    {
        [Key]
        public int SystemEventId { get; set; }
    }
}
