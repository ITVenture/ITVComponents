using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
{
    public class TutorialDefinition
    {
        public int VideoTutorialId { get; set; }

        public string DisplayName { get; set; }

        public string Description { get; set; }

        public string VideoMarkup { get; set; }

        public TutorialStreamDefinition[] Streams { get; set; }
    }
}
