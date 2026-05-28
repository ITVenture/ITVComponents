using System;
using ITVComponents.EFRepo.Options;
using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree.Extensions
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
