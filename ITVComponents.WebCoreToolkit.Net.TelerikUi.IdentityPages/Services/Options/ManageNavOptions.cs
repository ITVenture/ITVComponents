using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Services.Options
{
    public class ManageNavOptions
    {

        private List<ManageNavPage> navPages = new List<ManageNavPage>();

        public IReadOnlyList<ManageNavPage> Pages => navPages.ToArray();

        public void ClearNavPages()
        {
            navPages.Clear();
        }

        public void AddNavPage(ManageNavPage page)
        {
            navPages.Add(page);
        }

        public bool InsertAfter(ManageNavPage page, string afterNavTag)
        {
            var tmp = navPages.Select((p, id) => new { Id = id, Page = p })
                .FirstOrDefault(n => n.Page.NavTag == afterNavTag)?.Id ?? -1;
            bool retVal = tmp != -1;
            if (retVal)
            {
                navPages.Insert(tmp + 1, page);
            }

            return retVal;
        }

        public bool InsertBefore(ManageNavPage page, string afterNavTag)
        {
            var tmp = navPages.Select((p, id) => new { Id = id, Page = p })
                .FirstOrDefault(n => n.Page.NavTag == afterNavTag)?.Id ?? -1;
            bool retVal = tmp != -1;
            if (retVal)
            {
                navPages.Insert(tmp, page);
            }

            return retVal;
        }

        public bool IsNavPage(string page)
        {
            return Pages.Any(n => n.NavTag == page);
        }
    }
}
