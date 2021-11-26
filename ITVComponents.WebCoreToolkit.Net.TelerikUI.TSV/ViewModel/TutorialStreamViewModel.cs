using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class TutorialStreamViewModel
    {
        public int TutorialStreamId { get; set; }

        [MaxLength(100), Required]
        public string LanguageTag { get; set; }

        [MaxLength(100), Required]
        public string ContentType { get; set; }

        public int VideoTutorialId { get; set; }

        public string DownloadToken { get; set; }
    }
}
