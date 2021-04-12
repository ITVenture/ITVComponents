using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Localization
{
    internal class FallbackLocalizer : IStringLocalizer
    {
        private IStringLocalizer primary;
        private IStringLocalizerFactory factory;
        private readonly AttributeTranslationOptions options;

        public FallbackLocalizer(IStringLocalizer primary, IStringLocalizerFactory factory, AttributeTranslationOptions options)
        {
            this.primary = primary;
            this.factory = factory;
            this.options = options;
        }
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return primary.GetAllStrings(includeParentCultures);
        }

#if NETCOREAPP3_1
        public IStringLocalizer WithCulture(CultureInfo culture)
        {
            return new FallbackLocalizer(primary.WithCulture(culture), factory, options);
        }
#endif
        public LocalizedString this[string name]
        {
            get
            {
                string localName;
                var type = options.GetResourceType(name, out localName);
                if (type == null)
                {
                    localName = name;
                }
                
                LocalizedString retVal = new LocalizedString(localName, localName, false);
                if (!TryReadResource(primary, localName, null, ref retVal))
                {
                    if (type != null)
                    {
                        if (type != null)
                        {
                            var seco = factory.Create(type);
                            TryReadResource(seco, localName, null, ref retVal);
                        }
                    }
                }

                return retVal;
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                string localName;
                var type = options.GetResourceType(name, out localName);
                if (type == null)
                {
                    localName = name;
                }
                
                LocalizedString retVal = new LocalizedString(localName, localName, false);
                if (!TryReadResource(primary, localName, arguments, ref retVal))
                {
                    if (type != null)
                    {
                        if (type != null)
                        {
                            var seco = factory.Create(type);
                            TryReadResource(seco, localName, arguments, ref retVal);
                        }
                    }
                }

                return retVal;
            }
        }

        private bool TryReadResource(IStringLocalizer localizer, string resourceName, object[] arguments, ref LocalizedString value)
        {
            bool retVal = false;
            try
            {
                LocalizedString tmp = null;
                if (arguments == null)
                {
                    tmp = localizer[resourceName];
                }
                else
                {
                    tmp = localizer[resourceName, arguments];
                }

                if (!tmp.ResourceNotFound)
                {
                    retVal = true;
                    value = tmp;
                }
            }
            catch { }
            return retVal;
        } 
    }
}