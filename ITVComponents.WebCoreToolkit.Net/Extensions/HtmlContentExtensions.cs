using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;

namespace ITVComponents.WebCoreToolkit.Net.Extensions
{
    public static class HtmlContentExtensions
    {
        /// <summary>
        /// Converts the content of the given html element to a string
        /// </summary>
        /// <param name="content">the content-element to convert to string</param>
        /// <returns>the string-representation of the given html-content element</returns>
        public static string GetString(this IHtmlContent content)
        {
            using (var writer = new System.IO.StringWriter())
            {        
                content.WriteTo(writer, HtmlEncoder.Default);
                return writer.ToString();
            } 
        }  
    }
}
