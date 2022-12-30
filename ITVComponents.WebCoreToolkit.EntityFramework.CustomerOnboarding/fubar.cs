using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants;
using ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.SyntaxHelper;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding
{
    internal class fubar:AspNetSecurityContext<fubar>, ISecurityContextWithOnboarding
    {
        private string currentUserId;
        private string currentUserEmail;

        public fubar(ICalculatedColumnsSyntaxProvider syntaxProvider,DbContextOptions<fubar> options) : base(syntaxProvider, options)
        {
        }

        public fubar(IPermissionScope tenantProvider, IContextUserProvider userProvider, ILogger<fubar> logger, DbContextOptions<fubar> options) : base(tenantProvider, userProvider, logger, options)
        {
        }

        public string UserId => currentUserId ??= GetUserId();

        private string UserMail => currentUserEmail ??= GetUserMail();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureDefaultFilters(UseFilters, () => FilterAvailable, () => FilterAvailable,
                () => CurrentTenantId, () => UserId, () => UserMail);
        }

        public DbSet<CompanyInfo> Companies { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<EmployeeRole> EmployeeRoles { get; set; }

        private string GetUserId()
        {
            string retVal = null;
            if (Me?.Identity is ClaimsIdentity ci)
            {
                var idClaim = ci.Claims.FirstOrDefault(n => n.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
                if (idClaim != null)
                {
                    retVal = idClaim.Value;
                }
            }

            return retVal;
        }

        private string GetUserMail()
        {
            string retVal = null;
            if (Me?.Identity is ClaimsIdentity ci)
            {
                var idClaim = ci.Claims.FirstOrDefault(n => n.Type == System.Security.Claims.ClaimTypes.Email);
                if (idClaim != null)
                {
                    retVal = idClaim.Value;
                }
            }

            return retVal;
        }
    }
}
