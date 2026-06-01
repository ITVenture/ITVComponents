using System;
using System.Linq;
using System.Linq.Expressions;
using ITVComponents.EFRepo.DbContextConfig.Expressions;
using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree.Extensions
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
            Expression<Func<HierarchyBillingProfile, bool>> billingProfileExpression = bp => !FilterAvailable
                || ShowAllTenants
                || CurrentTenantId != null && bp.TenantId == CurrentTenantId
                || bp.Employees.Any(n => n.UserId == UserId && n.InvitationStatus == InvitationStatus.Committed
                                         || n.EMail == UserMail && n.InvitationStatus == InvitationStatus.Pending);

            Expression<Func<HierarchyEmployee, bool>> employeeExpression = em => !FilterAvailable
                || ShowAllTenants
                || CurrentTenantId != null && em.TenantId == CurrentTenantId
                || em.UserId == UserId && em.InvitationStatus == InvitationStatus.Committed
                || em.EMail == UserMail && em.InvitationStatus == InvitationStatus.Pending;

            Expression<Func<HierarchyEmployeeRole, bool>> employeeRoleExpression = er => !FilterAvailable
                || ShowAllTenants
                || CurrentTenantId != null && er.Employee.TenantId == CurrentTenantId
                || er.Employee.UserId == UserId && er.Employee.InvitationStatus == InvitationStatus.Committed
                || er.Employee.EMail == UserMail && er.Employee.InvitationStatus == InvitationStatus.Pending;

            target.ConfigureGlobalFilter(billingProfileExpression);
            target.ConfigureGlobalFilter(employeeExpression);
            target.ConfigureGlobalFilter(employeeRoleExpression);
        }
    }
}
