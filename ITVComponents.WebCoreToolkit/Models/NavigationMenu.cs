using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;

namespace ITVComponents.WebCoreToolkit.Models
{
    public sealed class NavigationMenu
    {
        public string DisplayName { get; set; }

        public string Url { get; set; }

        public string SpanClass { get; set; }

        public int SortOrder { get; set; }

        public string RequiredPermission { get;set; }

        public List<NavigationMenu> Children { get;} = new List<NavigationMenu>();

        public bool IsValid => !string.IsNullOrEmpty(Url) || Children.Any(n => n.IsValid);

        public bool Active { get; private set; }

        public void CleanUp(string currentPath, string jsonLanguageRecord)
        {
            var invalids = Children.Where(n => !n.IsValid).ToArray();
            foreach (var inv in invalids)
            {
                Children.Remove(inv);
            }

            Active = Url?.Equals(currentPath, StringComparison.OrdinalIgnoreCase) ?? false;
            Children.ForEach(n => n.CleanUp(currentPath, jsonLanguageRecord));
            if (!Active)
            {
                Active = Children.Any(c => c.Active);
            }

            if (!string.IsNullOrEmpty(jsonLanguageRecord) && !string.IsNullOrEmpty(DisplayName) && DisplayName.StartsWith("{") && DisplayName.EndsWith("}"))
            {
                try
                {
                    var ok = false;
                    var op = JsonHelper.FromJsonString<Dictionary<string, string>>(DisplayName);
                    if (op.ContainsKey(jsonLanguageRecord))
                    {
                        DisplayName = op[jsonLanguageRecord];
                        ok = true;
                    }

                    if (!ok && jsonLanguageRecord.Contains("-"))
                    {
                        var lg = jsonLanguageRecord.Substring(0, jsonLanguageRecord.IndexOf("-"));
                        if (op.ContainsKey(lg))
                        {
                            DisplayName = op[lg];
                            ok = true;
                        }
                    }

                    if (!ok && op.ContainsKey("Default"))
                    {
                        DisplayName = op["Default"];
                    }
                }
                catch
                {
                }
            }
        }
    }
}
