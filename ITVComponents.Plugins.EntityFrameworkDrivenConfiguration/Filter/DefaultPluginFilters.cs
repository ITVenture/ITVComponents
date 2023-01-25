using ITVComponents.EFRepo.DbContextConfig.Expressions;
using ITVComponents.EFRepo.Options;
using ITVComponents.Plugins.DatabaseDrivenConfiguration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.Plugins.EntityFrameworkDrivenConfiguration.Filter
{
    public static class DefaultPluginFilters
    {
        public static void ConfigureFilters<TContext>(DbContextModelBuilderOptions<TContext> options)
        {
            options.ConfigureGlobalFilter<DatabasePlugin>(n => n.TenantId == CurrentTenant);
            options.ConfigureGlobalFilter<DatabasePluginTypeParam>(n => n.TenantId == CurrentTenant);
        }

        [ExpressionPropertyRedirect("CurrentTenant")]
        private static string CurrentTenant => "";
    }
}
