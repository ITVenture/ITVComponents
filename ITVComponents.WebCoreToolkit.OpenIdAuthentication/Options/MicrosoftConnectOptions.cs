﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.OpenIdAuthentication.Options
{
    public class MicrosoftConnectOptions:ExternalAuthenticationOptionsBase
    {
        public string AccessDeniedPath { get; set; }
    }
}
