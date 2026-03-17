using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using ITVComponents.Threading;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TagHelpers
{
    [HtmlTargetElement("fk-resolve-label", Attributes = "fk-repo,fk-table,fk-id", TagStructure = TagStructure.NormalOrSelfClosing)]
    public class FkLabel : TagHelper
    {
        public string FkRepo { get; set; }

        public string FkTable { get; set; }

        public string FkId { get; set; }

        public bool Replace { get; set; } = true;

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            string id = null;
            if (output.Attributes.TryGetAttribute("id", out var attId))
            {
                id = attId.Value as string;
            }

            if (string.IsNullOrEmpty(id))
            {
                id = "fkLabel_" + Guid.NewGuid().ToString("N");
                output.Attributes.SetAttribute("id", id);
            }


            var rawContent = AsyncHelpers.RunSync(async () => await output.GetChildContentAsync()).GetContent();
            if (string.IsNullOrEmpty(rawContent))
            {
                rawContent = "<label fk-id=\"#=Key#\">#=Label#</label>";
            }

            rawContent = JavaScriptEncoder.Default.Encode(rawContent);
            output.Content.Clear();
            output.Attributes.SetAttribute("viewSrc", $"~/ForeignKey/{FkRepo}/{FkTable}?id={FkId}");
            var scriptTag = new TagBuilder("script");
            scriptTag.InnerHtml.AppendHtml($$"""
                                             $(function(){$("#{{id}}").loadPartial({
                                                replace:{{(Replace?"true":"false")}},
                                                dataType: "json",
                                                template: function(data){
                                                    return kendo.template("{{rawContent}}").apply(this,[data]);
                                                }
                                             });});
                                             """);
            output.PostElement.AppendHtml(scriptTag);
        }
    }
}
