using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.SqlServer.Designer
{
    public class SecurityContextDesignTimeHelper:IDesignTimeDbContextFactory<SecurityContext>
    {
        public SecurityContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SecurityContext>();
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=IWCSecurity;Trusted_Connection=True;", so => so.MigrationsAssembly(typeof(SecurityContextDesignTimeHelper).Assembly.FullName));

            return new SecurityContext(optionsBuilder.Options);
        }
    }
}
