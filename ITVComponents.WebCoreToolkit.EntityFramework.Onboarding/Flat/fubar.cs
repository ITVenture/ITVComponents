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
    /// Reference implementation showing how a consumer should derive its own DbContext from
    /// <see cref="AspNetSecurityContext{TContext}"/> and implement <see cref="ISecurityContextWithOnboarding"/>.
    /// A third-party context only needs to:
    /// <list type="number">
    /// <item>provide the two constructors (design-time + runtime) that forward to the base,</item>
    /// <item>expose the onboarding <c>DbSet</c>s required by the interface, and</item>
    /// <item>call <see cref="ModelBuilderExtensions.ConfigureOnboardingModel"/> from <c>OnModelCreating</c>
    /// (unconditionally — it wires keys/FKs and is independent of whether the global COB query filters are active).</item>
    /// </list>
    /// The <c>UserId</c>/<c>UserMail</c> filter replacers that the onboarding global filters resolve at query time
    /// are provided by the base context (its <c>CurrentUserId</c>/<c>CurrentUserMail</c>, both registered via
    /// <c>ConfigureExpressionProperty</c> in the base runtime constructor) — a consumer does NOT need to declare them.
    /// </summary>
    internal class fubar:AspNetSecurityContext<fubar>, ISecurityContextWithOnboarding
    {
        public fubar(DbContextModelBuilderOptions<fubar> builderOptions,DbContextOptions<fubar> options) : base(builderOptions, options)
        {
        }

        public fubar(IPermissionScope tenantProvider, IContextUserProvider userProvider, ILogger<fubar> logger, IOptions<DbContextModelBuilderOptions<fubar>> builderOptions, DbContextOptions<fubar> options) : base(tenantProvider, userProvider, logger, builderOptions, options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Structural onboarding model (keys/FKs). Call this UNCONDITIONALLY — it is independent of whether the
            // global COB query filters are active, and it is what makes runtime and design-time (migrations) agree.
            modelBuilder.ConfigureOnboardingModel();
        }

        public DbSet<BillingProfile> BillingProfiles { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<EmployeeRole> EmployeeRoles { get; set; }
        public DbSet<EmployeeRoleMapping> EmployeeRoleMappings { get; set; }
        public DbSet<PendingOnboarding> PendingOnboardings { get; set; }
        public DbSet<ConsentRecord> ConsentRecords { get; set; }
    }
}
