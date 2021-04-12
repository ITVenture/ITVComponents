using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Options
{
    public class CultureOptions
    {
        private Dictionary<string,string> uiCultureMappings = new Dictionary<string, string>();
        private Dictionary<string,string> cultureMappings = new Dictionary<string, string>();

        public void MapUiCulture(string clientCulture, string cultureToUse)
        {
            uiCultureMappings[clientCulture] = cultureToUse;
        }

        public void MapCulture(string clientCulture, string cultureToUse)
        {
            cultureMappings[clientCulture] = cultureToUse;
        }

        public CultureInfo MapUiCulture(CultureInfo clientCulture)
        {
            if (uiCultureMappings.ContainsKey(clientCulture.Name))
            {
                var n = uiCultureMappings[clientCulture.Name];
                return CultureInfo.GetCultureInfo(n);
            }

            return clientCulture;
        }

        public CultureInfo MapCulture(CultureInfo clientCulture)
        {
            if (cultureMappings.ContainsKey(clientCulture.Name))
            {
                var n = cultureMappings[clientCulture.Name];
                return CultureInfo.GetCultureInfo(n);
            }

            return clientCulture;
        }
    }
}
