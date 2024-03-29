﻿using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    [Index(nameof(PluginNameUniqueness),IsUnique=true,Name="IX_UniquePluginName")]
    public class WebPlugin:ITVComponents.WebCoreToolkit.Models.WebPlugin
    {
        [Key]
        public int WebPluginId { get; set; }
        
        public int? TenantId{get;set;}
        
        [DatabaseGenerated(DatabaseGeneratedOption.Computed),MaxLength(1024),Required]
        public string PluginNameUniqueness { get; set; }
        
        [ForeignKey(nameof(TenantId))]
        public virtual Tenant Tenant { get; set; }

        public virtual ICollection<WebPluginGenericParameter> Parameters { get; set; } = new List<WebPluginGenericParameter>();
    }
}
