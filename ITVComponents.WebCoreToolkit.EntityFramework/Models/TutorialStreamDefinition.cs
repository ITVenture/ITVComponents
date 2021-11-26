using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
{
    public class TutorialStreamDefinition
    {
        public int TutorialStreamId { get; set; }

        public string LanguageTag { get; set; }

        public string ContentType { get; set; }

        public int VideoTutorialId { get; set; }

        public string DownloadToken { get; set; }
    }
}
