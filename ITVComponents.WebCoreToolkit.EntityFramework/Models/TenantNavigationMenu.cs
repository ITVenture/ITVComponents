﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
{
    public class TenantNavigationMenu
    {
        [Key]
        public int TenantNavigationMenuId { get;set; }

        public int TenantId{get;set;}

        public int NavigationMenuId { get; set; }
        
        public int? PermissionId{get;set;}

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant Tenant { get; set; }

        [ForeignKey(nameof(NavigationMenuId))]
        public virtual NavigationMenu NavigationMenu { get; set; }
        
        [ForeignKey(nameof(PermissionId))]
        public virtual Permission Permission { get; set; }
    }
}
