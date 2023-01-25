using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins.DatabaseDrivenConfiguration.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.Plugins.EntityFrameworkDrivenConfiguration
{
    public interface IPluginRepo
    {
        string CurrentTenant { get; }

        DbSet<DatabasePlugin> Plugins { get; set; }

        DbSet<DatabasePluginTypeParam> PluginGenericParameters { get; set; }
    }
}
