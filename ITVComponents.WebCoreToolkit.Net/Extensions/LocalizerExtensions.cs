using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Net.Extensions
{
    public static class LocalizerExtensions
    {
        public static string DumpLocalizer(this IStringLocalizer localizer, string localObjectName)
        {
            var dic = new Dictionary<string, string>();
            foreach (var s in localizer.GetAllStrings(true))
            {
                dic.Add(s.Name, s.Value);
            }

            var json = JsonHelper.ToJson(dic);
            StringBuilder bld = new StringBuilder();
            bld.AppendLine($"var __loca{localObjectName}={json};");
            bld.AppendLine($@"for (var name in __loca{localObjectName}){{
    if (__loca{localObjectName}.hasOwnProperty(name)){{
        ITVenture.Text.setText(ITVenture.Lang,name,__loca{localObjectName}[name]);
    }}
}}");
            return bld.ToString();
        }
    }
}
