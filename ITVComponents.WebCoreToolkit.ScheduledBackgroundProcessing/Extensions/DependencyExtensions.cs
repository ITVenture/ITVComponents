﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.BackgroundProcessing;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.ScheduledBackgroundProcessing.Extensions
{
    public static class DependencyExtensions
    {
        /// <summary>
        /// Initializes a service that is capable to process long-term actions in the background
        /// </summary>
        /// <param name="services">the service-collection where ot inject the background-service</param>
        /// <returns>the servicecollection instance that was passed as argument</returns>
        public static IServiceCollection UseScheduledBackgroundTasks(this IServiceCollection services, Action<ITimeTableBackgroundTaskQueue> setDefaultTasks = null)
        {
            services.AddHostedService<BackgroundTaskProcessorService<ITimeTableBackgroundTaskQueue,TimeTableBackgroundTask>>();
            services.AddSingleton<ITimeTableBackgroundTaskQueue>(ctx =>
            {
                var retVal = new TimeTableBackgroundTaskQueue();
                setDefaultTasks?.Invoke(retVal);
                return retVal;
            });
            return services;
        }
    }
}
