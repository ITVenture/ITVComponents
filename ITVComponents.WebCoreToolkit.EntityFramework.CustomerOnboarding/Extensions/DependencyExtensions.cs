using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Options;
using ITVComponents.Scripting.CScript.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Extensions
{
    public static class DependencyExtensions
    {
        public static IServiceCollection ActivateGlobalCobFilters(this IServiceCollection services, Type t)
        {
            var method = LambdaHelper.GetMethodInfo(() => ActivateGlobalCobFilters<DbContext>(services))
                .GetGenericMethodDefinition();
            method = method.MakeGenericMethod(t);
            return (IServiceCollection)method.Invoke(null, new object[] { services });
        }

        public static IServiceCollection ActivateGlobalCobFilters<TContext>(this IServiceCollection services)
        {
            return services.Configure<DbContextModelBuilderOptions<TContext>>(o =>
            {
                ModelBuilderExtensions.ConfigureDefaultFilters(o);
            });
        }
    }
}
