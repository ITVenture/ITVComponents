using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models
{
    public class SystemEvent:ITVComponents.WebCoreToolkit.Models.SystemEvent
    {
        [Key]
        public override int SystemEventId { get; set; }
    }
}
