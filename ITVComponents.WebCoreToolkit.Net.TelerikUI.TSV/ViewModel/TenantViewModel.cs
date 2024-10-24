﻿using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class TenantViewModel
    {
        [Key]
        public int TenantId { get; set; }

        [Required, MaxLength(150)]
        public string TenantName { get; set; }

        [MaxLength(1024)]
        public string DisplayName { get; set; }

        public string TimeZone { get; set; }

        public dynamic UserId { get; set; }

        public string UniQUID { get; set; }

        public bool Assigned { get; set; }
    }
}
