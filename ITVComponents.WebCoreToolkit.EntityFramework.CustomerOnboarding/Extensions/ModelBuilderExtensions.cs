using System;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using ITVComponents.Scripting.CScript.Helpers;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Extensions
{
    public static class ModelBuilderExtensions
    {
        public static ModelBuilder ConfigureDefaultFilters(this ModelBuilder modelBuilder, bool useFilters, Expression<Func<bool>> filterAvailable, Expression<Func<bool>> showAllTenants, Expression<Func<int?>> currentTenantId, Expression<Func<string>> userId, Expression<Func<string>> userMail)
        {
            if (useFilters)
            {
                var nofi = Expression.Not(filterAvailable.GetPropertyExpression());
                var uid = userId.GetPropertyExpression();
                var uml = userMail.GetPropertyExpression();
                var nofiosat = Expression.OrElse(nofi, showAllTenants.GetPropertyExpression());
                var noo = Expression.Constant(null);
                var nocun = Expression.NotEqual(currentTenantId.GetPropertyExpression(), noo);
                var ci = Expression.Parameter(typeof(CompanyInfo));
                var em = Expression.Parameter(typeof(Employee));
                var er = Expression.Parameter(typeof(EmployeeRole));
                Expression<Func<CompanyInfo, int?>> cot = (CompanyInfo i) => i.TenantId;
                Expression<Func<Employee, int>> emt = (Employee i) => i.TenantId;
                Expression<Func<Employee, string>> emu = (Employee i) => i.UserId;
                Expression<Func<Employee, string>> emm = (Employee i) => i.EMail;
                Expression<Func<Employee, InvitationStatus>> emi = (Employee i) => i.InvitationStatus;
                Expression<Func<EmployeeRole, int>> emrot = (EmployeeRole i) => i.Employee.TenantId;
                Expression<Func<EmployeeRole, Employee>> rolemp = (EmployeeRole i) => i.Employee;
                var cota = Expression.Property(ci, cot.GetPropertyAccess());
                var emta = Expression.Property(em, emt.GetPropertyAccess());
                var emua = Expression.Property(em, emu.GetPropertyAccess());
                var emma = Expression.Property(em, emm.GetPropertyAccess());
                var emrota = Expression.Property(er, emrot.GetPropertyAccess());
                var emrema = Expression.Property(er, rolemp.GetPropertyAccess());
                var cnota = Expression.AndAlso(nocun, Expression.Equal(cota,currentTenantId.GetPropertyExpression()));
                var enmta = Expression.AndAlso(nocun, Expression.Equal(emta, currentTenantId.GetPropertyExpression()));
                var emnrota = Expression.AndAlso(nocun, Expression.Equal(emrota, currentTenantId.GetPropertyExpression()));
                var companyFilterRaw = Expression.OrElse(nofiosat, cnota);
                var employeeFilterRaw = Expression.OrElse(nofiosat, enmta);
                var roleFilterRaw = Expression.OrElse(nofiosat, emnrota);
                var uic = Expression.Constant(InvitationStatus.Committed);
                var uip = Expression.Constant(InvitationStatus.Pending);
                var uidc = Expression.AndAlso(Expression.Equal(emua, uid), Expression.Equal(emi, uic));
                var uemp = Expression.AndAlso(Expression.Equal(emma, uml), Expression.Equal(emi, uip));
                var empFinal = Expression.Lambda<Func<Employee, bool>>(Expression.OrElse(Expression.OrElse(employeeFilterRaw, uidc), uemp), em);
                var empPartial = Expression.Lambda<Func<Employee, bool>>(Expression.OrElse(uidc, uemp), em);
                Expression<Func<CompanyInfo, bool>> cemp = (CompanyInfo i) => i.Employees.AsQueryable().Any(empPartial);
                var emprFil = Expression.Invoke(empPartial, emrema);
                var cempFil = Expression.Invoke(cemp, ci);
                var empRoleFinal =
                    Expression.Lambda<Func<EmployeeRole, bool>>(Expression.OrElse(roleFilterRaw, emprFil), er);
                var companyfinal =
                    Expression.Lambda<Func<CompanyInfo, bool>>(Expression.OrElse(companyFilterRaw, cempFil), ci);
                modelBuilder.Entity<CompanyInfo>().HasQueryFilter(companyfinal);
                modelBuilder.Entity<Employee>().HasQueryFilter(empFinal);
                modelBuilder.Entity<EmployeeRole>().HasQueryFilter(empRoleFinal);
            }

            return modelBuilder;
        }
    }
}
