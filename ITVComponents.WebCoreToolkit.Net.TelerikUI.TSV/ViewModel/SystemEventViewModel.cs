﻿using System;
using System.ComponentModel.DataAnnotations;
using ITVComponents.DataAccess.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class SystemEventViewModel
    {
        public int SystemEventId { get; set; }
        public LogLevel LogLevel { get; set; }
        [MaxLength(1024)]
        public string Category { get; set; }
        [MaxLength(1024)]
        public string Title { get; set; }
        public string Message { get; set; }
        
        [UtcDateTime]
        public DateTime EventTime { get; set; }

        public object LogInfo { get; set; }
    }
}
