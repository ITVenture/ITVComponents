using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Options;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.PostgreSql.SyntaxHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.PostgreSql.Extensions
{
    public static class DependencyExtensions
    {
        public static IServiceCollection ConfigureComputedColumns<TDbContext>(this IServiceCollection services)
        {
            return services.Configure<DbContextModelBuilderOptions<TDbContext>>(o =>
            {
                PostgreSqlColumnsSyntaxHelper.ConfigureComputedColumns(o);
            });
        }

        public static IServiceCollection ConfigureComputedColumns(this IServiceCollection services, Type dbContextType)
        {
            var method = LambdaHelper.GetMethodInfo(() => ConfigureComputedColumns<SecurityContext>(services))
                .GetGenericMethodDefinition();
            method = method.MakeGenericMethod(dbContextType);
            return (IServiceCollection)method.Invoke(null, new[] { services });
        }
    }
}
