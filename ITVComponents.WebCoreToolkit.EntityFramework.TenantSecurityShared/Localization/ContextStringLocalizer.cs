using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Localization
{
    internal class ContextStringLocalizer:IStringLocalizer
    {
        private readonly IStringLocalizer baseLocalizer;
        private readonly IServiceProvider services;
        private readonly string resourceGroupName;

        private ConcurrentDictionary<string, List<LocalizedString>> collectedStrings =
            new ConcurrentDictionary<string, List<LocalizedString>>();

        private DateTime lastReset;

        public ContextStringLocalizer(IStringLocalizer baseLocalizer, IServiceProvider services, string resourceGroupName)
        {
            this.baseLocalizer = baseLocalizer;
            this.services = services;
            this.resourceGroupName = resourceGroupName;
            Console.WriteLine(resourceGroupName);
            lastReset = DateTime.Now;
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            var cc = CultureInfo.CurrentCulture;


            return GetStringsFor(cc, includeParentCultures);
        }

        public LocalizedString this[string name]
        {
            get
            {
                var cc = CultureInfo.CurrentCulture;
                var allStrings = GetStringsFor(cc, true);
                return allStrings.FirstOrDefault(n => n.Name == name) ?? new LocalizedString(name, name, true);
            }
        }

        public LocalizedString this[string name, params object[] arguments] =>
            new (name, string.Format(this[name].Value, arguments));


        private IEnumerable<LocalizedString> WithContext(
            Func<IBaseTenantContext, IEnumerable<LocalizedString>> contextAction)
        {
            using (var sc = services.CreateScope())
            {
                var ctx = sc.ServiceProvider.GetService<IBaseTenantContext>();
                return contextAction(ctx).ToList();
            }
        }

        private IEnumerable<LocalizedString> SelectLocales(IEnumerable<LocalizationString> strings)
        {
            return strings.Select(n => new LocalizedString(n.LocalizationKey, n.LocalizationValue));
        }

        private List<LocalizedString> GetStringsFor(CultureInfo targetCulture, bool încludeParent)
        {
            if (DateTime.Now.Subtract(lastReset).TotalHours > 12)
            {
                lastReset = DateTime.Now;
                collectedStrings.Clear();
            }

            var includeParentCultures = încludeParent;
            return collectedStrings.GetOrAdd($"{targetCulture.Name}_{includeParentCultures}", ct =>
            {
                var cc = targetCulture;
                List<string> culture = new List<string>();

                while (!Equals(cc, CultureInfo.InvariantCulture))
                {
                    culture.Add(cc.Name);
                    if (!includeParentCultures)
                    {
                        break;
                    }

                    cc = cc.Parent;
                }

                var locaStrings = WithContext(ctx =>
                {
                    var retVal = new List<LocalizedString>();
                    foreach (var c in culture)
                    {
                        var stringsRaw = ctx.Localizations.FirstOrDefault(n => n.Identifier == resourceGroupName)
                            ?.Cultures.FirstOrDefault(n => n.Culture.Name == c)?.Strings;
                        if (stringsRaw != null)
                        {
                            var tmp = SelectLocales(stringsRaw);
                            var tmp2 = (from t in tmp
                                join r in retVal on t.Name equals r.Name into no
                                from n in no.DefaultIfEmpty()
                                where n == null
                                select t).ToArray();
                            retVal.AddRange(tmp2);
                        }
                    }

                    var bas = baseLocalizer.GetAllStrings(includeParentCultures);
                    var tmp3 = (from t in bas
                        join r in retVal on t.Name equals r.Name into no
                        from n in no.DefaultIfEmpty()
                        where n == null
                        select t).ToArray();
                    retVal.AddRange(tmp3);
                    return retVal;
                }) as List<LocalizedString>;
                return locaStrings;
            });
        }
    }
}
