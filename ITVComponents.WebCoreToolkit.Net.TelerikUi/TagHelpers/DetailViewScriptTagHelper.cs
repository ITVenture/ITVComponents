using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TagHelpers
{
    [HtmlTargetElement("detail-view-script", Attributes="dummy-div-id,view-action", TagStructure = TagStructure.NormalOrSelfClosing)]
    public class DetailViewScriptTagHelper:TagHelper
    {
        public string DummyDivId { get; set; } 
        public string ViewAction { get; set; }
        public bool ReplaceDummy { get; set; } = true;

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "script";
            output.Attributes.SetAttribute("type", "text/x-kendo-template");
            output.TagMode = TagMode.StartTagAndEndTag;
            output.Content.SetHtmlContent(@$"<div id=""{DummyDivId}"" viewSrc=""{ViewAction}""></div>
<script>$(""\\#{DummyDivId}"").loadPartial({ReplaceDummy.ToString().ToLower()});<\/script>");
        }
    }
}
