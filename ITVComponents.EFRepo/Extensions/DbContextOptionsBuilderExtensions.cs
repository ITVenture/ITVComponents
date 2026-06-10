using ITVComponents.EFRepo.Helpers;
using ITVComponents.EFRepo.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace ITVComponents.EFRepo.Extensions
{
    public static class DbContextOptionsBuilderExtensions
    {
        public static T AddModCreateInterceptor<T>(this T builder, IServiceProvider services, bool useUtc)
            where T: DbContextOptionsBuilder
        {
            builder.AddInterceptors(new ModCreateInterceptor(services, useUtc));
            return builder;
        }

        public static T AddModCreateInterceptor<T>(this T builder, ICurrentUserProvider userProvider, bool useUtc)
            where T : DbContextOptionsBuilder
        {
            builder.AddInterceptors(new ModCreateInterceptor(userProvider, useUtc));
            return builder;
        }

        public static T AddEntityWriteTrackerInterceptor<T>(this T builder, IServiceProvider services)
            where T : DbContextOptionsBuilder
        {
            builder.AddInterceptors(new EntityWriteTrackerInterceptor(services));
            return builder;
        }

        public static DbContextOptionsBuilder<TContext> AddEntityWriteTrackerInterceptor<TContext>(this DbContextOptionsBuilder<TContext> builder, IEntityWriteTracker<TContext> tracker)
            where TContext : DbContext
        {
            builder.AddInterceptors(new EntityWriteTrackerInterceptor(tracker));
            return builder;
        }
    }
}
