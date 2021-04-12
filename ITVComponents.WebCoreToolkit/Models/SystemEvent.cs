using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Models
{
    public class SystemEvent
    {
        public int SystemEventId { get; set; }
        public LogLevel LogLevel { get; set; }
        [MaxLength(1024)]
        public string Category { get; set; }
        [MaxLength(1024)]
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime EventTime { get; set; }
    }
}
