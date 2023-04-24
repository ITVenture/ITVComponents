using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dynamitey.DynamicObjects;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.WebCoreToolkit.Options
{
    public class CultureOptions
    {
        
        private ConcurrentDictionary<string, CultureRedirectOption> cultureMappings = new();

        private ConcurrentDictionary<string, CultureConfigOption> cultureConfigurations = new();

        public void MapUiCulture(string clientCulture, string cultureToUse)
        {
            GetCultureMapping(clientCulture, o => o.UiCulture = cultureToUse);
        }

        public void MapCulture(string clientCulture, string cultureToUse)
        {
            GetCultureMapping(clientCulture, o => o.Culture = cultureToUse);
        }

        public CultureInfo MapUiCulture(CultureInfo clientCulture)
        {
            return MapAndConfigure(clientCulture, n => n.UiCulture);
        }

        public CultureInfo MapCulture(CultureInfo clientCulture)
        {
            return MapAndConfigure(clientCulture, n => n.Culture);
        }

        public void ConfigureCulture(CultureConfigOption cultureOption)
        {
            cultureConfigurations.AddOrUpdate(cultureOption.Name, cultureOption, (n, o) => cultureOption);
        }

        private CultureInfo MapAndConfigure(CultureInfo clientCulture,
            Func<CultureRedirectOption, string> propertyAccess)
        {
            var retVal = clientCulture;
            var cu = GetCultureMapping(clientCulture.Name, null);
            var tcu = propertyAccess(cu);
            bool isNew = false;
            if (tcu != clientCulture.Name)
            {
                retVal = new CultureInfo(tcu);
                isNew = true;
            }

            if (cultureConfigurations.TryGetValue(retVal.Name, out var cfg))
            {
                if (!isNew)
                {
                    retVal = new CultureInfo(retVal.Name);
                }

                if (!string.IsNullOrEmpty(cfg.NumberDecimalSeparator))
                {
                    retVal.NumberFormat.NumberDecimalSeparator = cfg.NumberDecimalSeparator;
                    retVal.NumberFormat.CurrencyDecimalSeparator = cfg.NumberDecimalSeparator;
                    retVal.NumberFormat.PercentDecimalSeparator = cfg.NumberDecimalSeparator;
                }

                if (!string.IsNullOrEmpty(cfg.CurrencyDecimalSeparator))
                {
                    retVal.NumberFormat.CurrencyDecimalSeparator = cfg.CurrencyDecimalSeparator;
                }

                if (!string.IsNullOrEmpty(cfg.CurrencySymbol))
                {
                    retVal.NumberFormat.CurrencySymbol = cfg.CurrencySymbol;
                }

                if (!string.IsNullOrEmpty(cfg.PercentDecimalSeparator))
                {
                    retVal.NumberFormat.PercentDecimalSeparator = cfg.PercentDecimalSeparator;
                }
            }

            return retVal;
        }

        private CultureRedirectOption GetCultureMapping(string clientCulture, Action<CultureRedirectOption> cfg)
        {
            return cultureMappings.AddOrUpdate(clientCulture, ci =>
            {
                var ret = new CultureRedirectOption
                {
                    UiCulture = ci,
                    Culture = ci
                };
                cfg?.Invoke(ret);
                return ret;
            }, (ci, or) =>
            {
                cfg?.Invoke(or);
                return or;
            });
        }
    }
}
