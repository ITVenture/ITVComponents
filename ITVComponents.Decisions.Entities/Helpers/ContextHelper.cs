using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Proxies.Internal;

namespace ITVComponents.Decisions.Entities.Helpers
{
    internal static class ContextHelper
    {
        public static DbContext GetDbContextFromEntity(object entity)
        {
            if (entity is IProxyLazyLoader ll)
            {
                var llTyp = ll.LazyLoader.GetType();
                var prop = llTyp.GetProperty("Context", BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null)
                {
                    return (DbContext)prop.GetValue(ll.LazyLoader);
                }
            }

            return null;
        }
    }
}
