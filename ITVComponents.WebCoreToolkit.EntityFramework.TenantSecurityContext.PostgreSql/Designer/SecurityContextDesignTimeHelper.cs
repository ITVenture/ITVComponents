using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.PostgreSql.SyntaxHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.PostgreSql.Designer
{
    // --force nicht vergessen bei dotnet ef migrations remove
    public class SecurityContextDesignTimeHelper:IDesignTimeDbContextFactory<SecurityContext>
    {
        public SecurityContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SecurityContext>();
            optionsBuilder.UseNpgsql(so => so.MigrationsAssembly(typeof(SecurityContextDesignTimeHelper).Assembly.FullName));
            var builderOptions = new DbContextModelBuilderOptions<SecurityContext>();
            PostgreSqlColumnsSyntaxHelper.ConfigureComputedColumns(builderOptions);
            return new SecurityContext(builderOptions,optionsBuilder.Options);
        }
    }
}
