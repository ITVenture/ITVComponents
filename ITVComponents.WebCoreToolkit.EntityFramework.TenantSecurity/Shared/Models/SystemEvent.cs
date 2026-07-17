using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models
{
    /// <summary>
    /// The index serves the two orderings the system-log views rely on: the paged list (newest first) and the
    /// entry-context window, which seeks to an anchor and reads its neighbours. EventTime is not unique, so
    /// SystemEventId is part of the key to make the tie-break seekable rather than a sort.
    /// </summary>
    [Index(nameof(EventTime), nameof(SystemEventId), IsUnique = false, Name = "IX_SystemLogEventTime")]
    public class SystemEvent:ITVComponents.WebCoreToolkit.Models.SystemEvent
    {
        [Key]
        public override int SystemEventId { get; set; }
    }
}
