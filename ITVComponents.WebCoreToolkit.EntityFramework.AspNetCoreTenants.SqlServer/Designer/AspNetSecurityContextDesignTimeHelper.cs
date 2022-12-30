using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.SqlServer.SyntaxHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.SqlServer.Designer
{
    public class AspNetSecurityContextDesignTimeHelper:IDesignTimeDbContextFactory<AspNetSecurityContext>
    {
        public AspNetSecurityContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AspNetSecurityContext>();
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=IWCASPSecurity;Trusted_Connection=True;", so => so.MigrationsAssembly(typeof(AspNetSecurityContextDesignTimeHelper).Assembly.FullName));

            return new AspNetSecurityContext(new SqlColumnsSyntaxHelper(),optionsBuilder.Options);
        }
    }
}
