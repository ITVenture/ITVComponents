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

            return new AspNetSecurityContext(optionsBuilder.Options);
        }
    }
}
