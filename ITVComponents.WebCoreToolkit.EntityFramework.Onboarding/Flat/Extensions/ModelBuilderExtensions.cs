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
            Expression<Func<TenantSubscription, bool>> subscriptionExpression = ts => !FilterAvailable
                || ShowAllTenants
                || CurrentTenantId != null && ts.TenantId == CurrentTenantId;
            target.ConfigureGlobalFilter(billingProfileExpression);
            target.ConfigureGlobalFilter(employeeExpression);
            target.ConfigureGlobalFilter(employeeRoleExpression);
            target.ConfigureGlobalFilter(subscriptionExpression);
        }
    }
}
