using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat
{
    /// <summary>
    /// Reference implementation showing how a consumer should derive its own DbContext
    /// from <see cref="AspNetSecurityContext{TContext}"/> and implement
    /// <see cref="ISecurityContextWithOnboarding"/>. Note the <c>ConfigureExpressionProperty</c>
    /// calls in the runtime constructor — those are required so the global filters in
    /// <see cref="ModelBuilderExtensions.ConfigureDefaultFilters{TContext}"/> can resolve
    /// <c>UserId</c> and <c>UserMail</c> at query time.
    /// </summary>
    internal class fubar:AspNetSecurityContext<fubar>, ISecurityContextWithOnboarding
    {
        private string currentUserId;
        private string currentUserEmail;

        public fubar(DbContextModelBuilderOptions<fubar> builderOptions,DbContextOptions<fubar> options) : base(builderOptions, options)
        {
        }

        public fubar(IPermissionScope tenantProvider, IContextUserProvider userProvider, ILogger<fubar> logger, IOptions<DbContextModelBuilderOptions<fubar>> builderOptions, DbContextOptions<fubar> options) : base(tenantProvider, userProvider, logger, builderOptions, options)
        {
            modelBuilderOptions.ConfigureExpressionProperty(()=>UserId);
            modelBuilderOptions.ConfigureExpressionProperty(() => UserMail);
        }

        public string UserId => currentUserId ??= GetUserId();

        private string UserMail => currentUserEmail ??= GetUserMail();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }

        public DbSet<BillingProfile> BillingProfiles { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<EmployeeRole> EmployeeRoles { get; set; }
        public DbSet<Plan> Plans { get; set; }
        public DbSet<TenantSubscription> TenantSubscriptions { get; set; }

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
