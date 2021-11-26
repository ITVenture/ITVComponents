using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class VideoTutorialViewModel
    {
        public int VideoTutorialId { get; set; }

        [Required, MaxLength(200)]
        public string SortableName { get; set; }

        [Required]
        [DataType(DataType.MultilineText)]
        public string DisplayName { get; set; }

        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        public string ModuleUrl { get; set; }
    }
}
