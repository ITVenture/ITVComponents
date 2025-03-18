using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    [Index(nameof(WebPluginId), nameof(GenericTypeName), IsUnique = true, Name = "IX_UniqueGenericParamName")]
    public class WebPluginGenericParameter<TTenant,TWebPlugin, TWebPluginGenericParameter> : ITVComponents.WebCoreToolkit.Models.WebPluginGenericParam 
        where TTenant : Tenant
        where TWebPlugin: WebPlugin<TTenant, TWebPlugin,TWebPluginGenericParameter>
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
    {
        [Key]
        public int WebPluginGenericParameterId { get; set; }

        public int WebPluginId { get; set; }

        [ForeignKey(nameof(WebPluginId))]
        public virtual TWebPlugin Plugin { get; set; }
    }
}
