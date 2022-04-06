﻿using System;
using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTenantSecurityUserView.Extensions
{
    public static class ApplicationPartExtensions
    {
        public static ApplicationPartManager EnableItvUserView(this ApplicationPartManager manager)
        {
            return manager.EnableItvUserView<AspNetSecurityContext>();
        }

        public static ApplicationPartManager EnableItvUserView<TContext>(this ApplicationPartManager manager)
        where TContext:AspNetSecurityContext<TContext>
        {
            var dic = new Dictionary<string, Type>
            {
                { "TContext", typeof(TContext)}
            };

            AssemblyPartWithGenerics part = new AssemblyPartWithGenerics(typeof(ApplicationPartExtensions).Assembly, dic);
            manager.ApplicationParts.Add(part);
            return manager;
        }
    }
}
