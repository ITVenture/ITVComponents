using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Localization.Model
{
    public class PrePostValLocalizationBlock
    {
        public string Title { get; set; }
        public string Pre { get; set; }

        public string TagTx { get; set; }

        public string Post { get; set; }
        public void FormatProperties(params object[] formatArgs)
        {
            Title = string.Format(Title, formatArgs);
            Pre = string.Format(Pre, formatArgs);
            TagTx = string.Format(TagTx, formatArgs);
            Post = string.Format(Post, formatArgs);
        }
    }
}
