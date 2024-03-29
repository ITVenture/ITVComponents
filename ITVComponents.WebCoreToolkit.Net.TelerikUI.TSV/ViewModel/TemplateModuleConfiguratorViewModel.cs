﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class TemplateModuleConfiguratorViewModel
    {
        public int TemplateModuleConfiguratorId { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(2048)]
        public string CustomConfiguratorView { get; set; }

        [Required, MaxLength(2048),DataType(DataType.MultilineText)]
        public string ConfiguratorTypeBack { get; set; }

        [DataType(DataType.MultilineText)]
        public string DisplayName { get; set; }

        public int TemplateModuleId { get; set; }
    }
}
