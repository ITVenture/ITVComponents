using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.EFRepo.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DbContextConfig.Impl;
using ITVComponents.Plugins.DatabaseDrivenConfiguration.Models;

namespace ITVComponents.Plugins.EntityFrameworkDrivenConfiguration.Filter
{
    public class PlugInContextFilterInitializer<TContext> : DbModelBuilderOptionsProvider<TContext> where TContext : Microsoft.EntityFrameworkCore.DbContext
    {
        private readonly string customPluginsTable;
        private readonly string customPluginParameterTable;

        public PlugInContextFilterInitializer(string customPluginsTable, string customPluginParameterTable) : this()
        {
            this.customPluginsTable = customPluginsTable;
            this.customPluginParameterTable = customPluginParameterTable;
        }

        public PlugInContextFilterInitializer(string customPluginsTable, string customPluginParameterTable, DbModelBuilderOptionsProvider<TContext> parent) : base(parent)
        {
            this.customPluginsTable = customPluginsTable;
            this.customPluginParameterTable = customPluginParameterTable;
        }

        public PlugInContextFilterInitializer():base()
        {
        }

        public PlugInContextFilterInitializer(DbModelBuilderOptionsProvider<TContext> parent) : base(parent) { }

        protected override void Configure(DbContextModelBuilderOptions<TContext> options)
        {
            DefaultPluginFilters.ConfigureFilters(options);
            if (!string.IsNullOrEmpty(customPluginParameterTable))
            {
                options.AddCustomConfigurator(new TableRedirect<DatabasePluginTypeParam>(customPluginParameterTable));
            }

            if (!string.IsNullOrEmpty(customPluginsTable))
            {
                options.AddCustomConfigurator(new TableRedirect<DatabasePlugin>(customPluginsTable));
            }
        }
    }
}
