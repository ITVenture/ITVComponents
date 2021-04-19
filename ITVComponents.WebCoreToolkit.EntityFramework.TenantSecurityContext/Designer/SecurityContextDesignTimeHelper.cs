using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Designer
{
    public class SecurityContextDesignTimeHelper:IDesignTimeDbContextFactory<SecurityContext>
    {
        public SecurityContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SecurityContext>();
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=IWCSecurity;Trusted_Connection=True;");

            return new SecurityContext(optionsBuilder.Options);
        }
    }
}
