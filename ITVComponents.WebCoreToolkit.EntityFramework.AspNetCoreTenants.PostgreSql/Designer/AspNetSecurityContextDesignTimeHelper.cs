﻿using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.PostgreSql.SyntaxHelper;
using ITVComponents.WebCoreToolkit.EntityFramework.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.PostgreSql.Designer
{
    public class AspNetSecurityContextDesignTimeHelper:IDesignTimeDbContextFactory<AspNetSecurityContext>
    {
        public AspNetSecurityContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AspNetSecurityContext>();
            optionsBuilder.UseNpgsql( so => so.MigrationsAssembly(typeof(AspNetSecurityContextDesignTimeHelper).Assembly.FullName));
            var builderOptions = new DbContextModelBuilderOptions<AspNetSecurityContext>();
            PostgreSqlColumnsSyntaxHelper.ConfigureComputedColumns(builderOptions);
            return new AspNetSecurityContext(builderOptions,optionsBuilder.Options);
        }
    }
}
