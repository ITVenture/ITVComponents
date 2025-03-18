using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Options
{
    public class ViewOptions
    {
        public string LayoutPage { get; set; } = "_Layout";

        public bool UseHealthView { get; set; } = false;

        public bool UseControllerView { get; set; } = false;

        public bool UseViewsView { get; set; } = false;

        public bool UseTagHelperView { get; set; } = false;

        public bool UseViewComponentView { get; set; }
    }
}
