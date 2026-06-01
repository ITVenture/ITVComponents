using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TagHelpers
{
    [HtmlTargetElement("itv-dynamic-content", Attributes = "id", TagStructure = TagStructure.NormalOrSelfClosing)]
    public class DynContentLoadTagHelper:TagHelper
    {
        public string Id { get; set; }
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            bool deferred = true;
            string targetHandler = null;
            bool autonomousHandler = false;
            if (output.Attributes.TryGetAttribute("deferred", out var attDefer) && attDefer is { Value: not null })
            {
                deferred = attDefer.Value.ToString()?.ToLower() == "true";
                output.Attributes.Remove(attDefer);
            }

            if (output.Attributes.TryGetAttribute("handlerTarget", out var attTarget) &&
                attTarget is { Value: not null })
            {
                targetHandler = attTarget.Value.ToString() ?? string.Empty;
                output.Attributes.Remove(attTarget);
            }

            if (output.Attributes.TryGetAttribute("handlerAutonomous", out var attAutonomous) &&
                attAutonomous is { Value: not null })
            {
                autonomousHandler = attAutonomous.Value.ToString()?.ToLower() == "true";
                output.Attributes.Remove(attAutonomous);
            }

            output.TagName = "div";
            output.Attributes.SetAttribute("id", Id);
            output.Attributes.SetAttribute("deferLoad", deferred.ToString().ToLower());
            
            output.TagMode = TagMode.StartTagAndEndTag;
            var scriptTag = new TagBuilder("script");
            if (string.IsNullOrEmpty(targetHandler))
            {
                scriptTag.InnerHtml.AppendHtml($"$(\"#{Id}\").loadPartial();");
            }
            else
            {
                scriptTag.InnerHtml.AppendHtml($"{(autonomousHandler?"var ":"")}{targetHandler} = $(\"#{Id}\").loadPartial().data(\"viewLoader\");");
            }

            output.PostContent.AppendHtml(scriptTag);
        }
    }
}
