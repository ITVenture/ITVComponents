using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Designer
{
    public class AspNetSecurityContextDesignTimeHelper:IDesignTimeDbContextFactory<AspNetSecurityContext>
    {
        public AspNetSecurityContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AspNetSecurityContext>();
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=IWCASPSecurity;Trusted_Connection=True;");

            return new AspNetSecurityContext(optionsBuilder.Options);
        }
    }
}
