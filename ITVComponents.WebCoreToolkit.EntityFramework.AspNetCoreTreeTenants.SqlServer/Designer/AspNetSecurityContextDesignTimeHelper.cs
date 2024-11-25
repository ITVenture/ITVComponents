using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.SqlServer.SyntaxHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.SqlServer.Designer
{
    public class AspNetSecurityContextDesignTimeHelper:IDesignTimeDbContextFactory<AspNetTreeSecurityContext>
    {
        public AspNetTreeSecurityContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AspNetTreeSecurityContext>();
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=IWCASPSecurity;Trusted_Connection=True;", so => so.MigrationsAssembly(typeof(AspNetSecurityContextDesignTimeHelper).Assembly.FullName));
            var builderOptions = new DbContextModelBuilderOptions<AspNetTreeSecurityContext>();
            SqlColumnsSyntaxHelper.ConfigureComputedColumns(builderOptions);
            return new AspNetTreeSecurityContext(builderOptions,optionsBuilder.Options);
        }
    }
}
