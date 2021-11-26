using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.Net.Options
{
    [SettingName("TutorialVideoSettings")]
    public class TutorialOptions
    {
        public string VideoFileHandler { get; set; }
    }
}
