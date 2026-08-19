using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.PostgreSql.SyntaxHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.PostgreSql.Designer
{
    public class AspNetTreeSecurityContextDesignTimeHelper : IDesignTimeDbContextFactory<AspNetTreeSecurityContext>
    {
        public AspNetTreeSecurityContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AspNetTreeSecurityContext>();
            optionsBuilder.UseNpgsql(so => so.MigrationsAssembly(typeof(AspNetTreeSecurityContextDesignTimeHelper).Assembly.FullName));
            var builderOptions = new DbContextModelBuilderOptions<AspNetTreeSecurityContext>();
            PostgreSqlColumnsSyntaxHelper.ConfigureComputedColumns(builderOptions);
            return new AspNetTreeSecurityContext(builderOptions, optionsBuilder.Options);
        }
    }
}
