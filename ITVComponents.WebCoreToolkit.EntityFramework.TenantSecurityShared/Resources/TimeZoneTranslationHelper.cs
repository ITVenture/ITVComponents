using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Resources
{
    public static class TimeZoneTranslationHelper
    {
        private static ResourceManager resourceMan;
        private static ResourceManager ResourceManager => resourceMan ??= new ResourceManager("ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Resources.TimeZoneTranslations", typeof(TimeZoneTranslationHelper).Assembly);

        public static string GetTZLabel(string zoneId, string defaultValue, CultureInfo culture = null)
        {
            culture ??= CultureInfo.CurrentUICulture;
            var mgr = ResourceManager;
            var retVal = mgr.GetString(zoneId, culture);
            if (string.IsNullOrEmpty(retVal) || retVal == zoneId)
            {
                retVal = defaultValue;
            }

            return retVal;
        }

        public static List<ForeignKeyData<string>> GetTranslatedTimeZoneList()
        {
            return TimeZoneInfo.GetSystemTimeZones().Select(n => new ForeignKeyData<string>
                { Key = n.Id, Label = TimeZoneTranslationHelper.GetTZLabel(n.Id, n.DisplayName) }).ToList();
        }
    }
}
