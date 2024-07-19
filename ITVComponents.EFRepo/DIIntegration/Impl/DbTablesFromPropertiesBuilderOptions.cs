using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DbContextConfig.Impl;
using ITVComponents.EFRepo.Options;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.DIIntegration.Impl
{
    public class DbTablesFromPropertiesBuilderOptions<TContext>:DbModelBuilderOptionsProvider<TContext> where TContext:DbContext
    {
        public DbTablesFromPropertiesBuilderOptions() : base()
        {
        }

        public DbTablesFromPropertiesBuilderOptions(DbModelBuilderOptionsProvider<TContext> parent) : base(parent)
        {
        }

        protected override void Configure(DbContextModelBuilderOptions<TContext> options)
        {
            options.AddCustomConfigurator(new TableNamesFromProperties<TContext>());
        }
    }
}
