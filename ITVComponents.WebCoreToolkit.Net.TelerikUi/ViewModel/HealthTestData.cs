using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel
{
    public class HealthTestData
    {
        public string Name { get; set; }
        public string Result { get; set; }
        public string? Description { get; set; }
        public string Tags { get; set; }
        public string Message { get; set; }
        public HealthCheckBubbleInfo Bubble { get; set; }
    }
}
