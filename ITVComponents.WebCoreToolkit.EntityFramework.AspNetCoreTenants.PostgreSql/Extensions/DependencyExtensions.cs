using System;
using System.Security;
using ITVComponents.EFRepo.Options;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.PostgreSql.SyntaxHelper;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.PostgreSql.Extensions
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
            var method = LambdaHelper.GetMethodInfo(() => ConfigureComputedColumns<AspNetSecurityContext>(services))
                .GetGenericMethodDefinition();
            method = method.MakeGenericMethod(dbContextType);
            return (IServiceCollection)method.Invoke(null, new[] { services });
        }
    }
}
