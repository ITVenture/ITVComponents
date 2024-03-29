﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Health.Model
{
    public class HealthScriptResult:IHealthDetailResult
    {
        public string TestName { get; set; }

        public HealthStatus Status { get; set; }

        public string StatusText { get; set; }
    }
}
