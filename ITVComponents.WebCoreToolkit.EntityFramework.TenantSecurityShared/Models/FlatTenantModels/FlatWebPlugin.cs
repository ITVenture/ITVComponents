using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.FlatTenantModels
{
    public class FlatWebPlugin:WebPlugin<Tenant, FlatWebPlugin, FlatWebPluginGenericParameter>
    {
    }
}
