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
            target.ConfigureGlobalFilter(billingProfileExpression);
            target.ConfigureGlobalFilter(employeeExpression);
            // EmployeeRole points at an EmployeeRoleMapping; keep that FK non-cascading so the tenant->employee
            // cascade path is the only one reaching EmployeeRole (avoids SQL Server multiple-cascade-path errors).
            target.ConfigureGlobalFilter(employeeRoleExpression,
                b => b.HasOne(er => er.RoleMapping).WithMany(m => m.EmployeeRoles)
                    .HasForeignKey(er => er.EmployeeRoleMappingId).OnDelete(DeleteBehavior.Restrict));
            target.ConfigureGlobalFilter(employeeRoleMappingExpression, b =>
            {
                b.HasOne(m => m.Tenant).WithMany().HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Restrict);
                b.HasOne(m => m.Role).WithMany().HasForeignKey(m => m.RoleId).OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
