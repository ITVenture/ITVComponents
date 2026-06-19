using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using ITVComponents.EFRepo.DbContextConfig.Expressions;
using ITVComponents.Scripting.CScript.Helpers;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ITVComponents.EFRepo.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Extensions
{
    public static class ModelBuilderExtensions
    {
        [ExpressionPropertyRedirect("UserMail")]
        public static string UserMail { get; set; }

        [ExpressionPropertyRedirect("UserId")]
        public static string UserId { get; set; }

        [ExpressionPropertyRedirect("CurrentTenantId")]
        public static int? CurrentTenantId { get; set; }

        [ExpressionPropertyRedirect("ShowAllTenants")]
        public static bool ShowAllTenants { get; set; }

        [ExpressionPropertyRedirect("FilterAvailable")]
        public static bool FilterAvailable { get; set; }

        public static void ConfigureDefaultFilters<TContext>(DbContextModelBuilderOptions<TContext> target)
        {
            Expression<Func<BillingProfile, bool>> billingProfileExpression = bp => !FilterAvailable
                                                                          || ShowAllTenants ||
                                                                          CurrentTenantId != null &&
                                                                          bp.TenantId == CurrentTenantId ||
                                                                          bp.Employees.Any(n =>
                                                                              n.UserId == UserId &&
                                                                              n.InvitationStatus ==
                                                                              InvitationStatus.Committed
                                                                              || n.EMail == UserMail &&
                                                                              n.InvitationStatus ==
                                                                              InvitationStatus.Pending);
            Expression<Func<Employee, bool>> employeeExpression = em => !FilterAvailable
                                                                        || ShowAllTenants
                                                                        || CurrentTenantId != null &&
                                                                        em.TenantId == CurrentTenantId
                                                                        || em.UserId == UserId &&
                                                                        em.InvitationStatus ==
                                                                        InvitationStatus.Committed
                                                                        || em.EMail == UserMail &&
                                                                        em.InvitationStatus ==
                                                                        InvitationStatus.Pending;
            Expression<Func<EmployeeRole, bool>> employeeRoleExpression = er => !FilterAvailable
                || ShowAllTenants
                || CurrentTenantId != null && er.Employee.TenantId == CurrentTenantId
                || er.Employee.UserId == UserId && er.Employee.InvitationStatus == InvitationStatus.Committed
                || er.Employee.EMail == UserMail && er.Employee.InvitationStatus == InvitationStatus.Pending;
            Expression<Func<EmployeeRoleMapping, bool>> employeeRoleMappingExpression = m => !FilterAvailable
                || ShowAllTenants
                || CurrentTenantId != null && m.TenantId == CurrentTenantId;
            // Query filters ONLY. The structural model (keys/FKs) lives in ConfigureOnboardingModel so it can be
            // applied unconditionally (runtime + design-time) without being coupled to filter activation.
            target.ConfigureGlobalFilter(billingProfileExpression);
            target.ConfigureGlobalFilter(employeeExpression);
            target.ConfigureGlobalFilter(employeeRoleExpression);
            target.ConfigureGlobalFilter(employeeRoleMappingExpression);
        }

        /// <summary>
        /// Configures the structural model for the flat onboarding entities (keys come from data annotations; this
        /// wires the foreign keys with their delete behaviour). Call this from the consuming context's
        /// <c>OnModelCreating</c> <b>unconditionally</b> — independently of whether the global COB query filters are
        /// active (those are registered separately via <see cref="ConfigureDefaultFilters{TContext}"/> /
        /// <c>ActivateGlobalCobFilters</c>). Mirrors the <c>ConfigureBilling()</c> pattern: a single source for the
        /// onboarding relationships so runtime and design-time (migrations) stay consistent and there is no
        /// duplicated relationship configuration.
        /// </summary>
        public static ModelBuilder ConfigureOnboardingModel(this ModelBuilder modelBuilder)
        {
            // EmployeeRole -> EmployeeRoleMapping: keep this FK non-cascading so the tenant->employee path stays
            // the only cascade path reaching EmployeeRole (avoids SQL Server multiple-cascade-path errors / 1785).
            modelBuilder.Entity<EmployeeRole>()
                .HasOne(er => er.RoleMapping).WithMany(m => m.EmployeeRoles)
                .HasForeignKey(er => er.EmployeeRoleMappingId).OnDelete(DeleteBehavior.Restrict);

            // EmployeeRoleMapping -> Tenant / Role: real FKs, non-cascading for the same reason.
            modelBuilder.Entity<EmployeeRoleMapping>(b =>
            {
                b.HasOne(m => m.Tenant).WithMany().HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Restrict);
                b.HasOne(m => m.Role).WithMany().HasForeignKey(m => m.RoleId).OnDelete(DeleteBehavior.Restrict);
            });

            return modelBuilder;
        }
    }
}
