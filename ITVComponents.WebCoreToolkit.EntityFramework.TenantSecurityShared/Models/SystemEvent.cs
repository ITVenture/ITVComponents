using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    public class SystemEvent:ITVComponents.WebCoreToolkit.Models.SystemEvent
    {
        [Key]
        public override int SystemEventId { get; set; }
    }
}
