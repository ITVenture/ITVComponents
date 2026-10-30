using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DbContextConfig.Impl;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.EFRepo.Options;

namespace ITVComponents.EFRepo.DIIntegration.Impl
{
    public class CustomSchemaOptionsLoader<TContext> : DbModelBuilderOptionsProvider<TContext> where TContext : DbContext
    {
        private string targetSchema;

        public CustomSchemaOptionsLoader(string targetSchema)
        {
            ApplySchema(targetSchema);
        }

        public CustomSchemaOptionsLoader(DbModelBuilderOptionsProvider<TContext> parent, string targetSchema) : base(parent)
        {
            ApplySchema(targetSchema);
        }

        private void ApplySchema(string providedSchema)
        {
            targetSchema = SchemaConfiguration.Resolve(providedSchema);
        }

        protected override void Configure(DbContextModelBuilderOptions<TContext> options)
        {
            if (!string.IsNullOrEmpty(targetSchema))
            {
                options.AddCustomConfigurator(new SchemaRedirect(targetSchema));
            }
        }
    }
}
