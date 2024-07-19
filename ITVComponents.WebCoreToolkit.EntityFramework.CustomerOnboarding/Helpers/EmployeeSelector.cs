using ITVComponents.WebCoreToolkit.EntityFramework.Help.QueryExtenders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Expressions.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Models.BinderContext;

namespace ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Helpers
{
    public class EmployeeSelector: ForeignKeySelectorHelperBase<BinderEmployee, Guid>
    {
        public override Sort[] DefaultSorts { get; } = new Sort[]
        {
            new Sort
            {
                Direction = SortDirection.Ascending,
                MemberName = nameof(BinderEmployee.LastName)
            },
            new Sort
            {
                Direction = SortDirection.Ascending,
                MemberName = nameof(BinderEmployee.FirstName)
            }
        };

        protected override Expression<Func<BinderEmployee, IDictionary<string, object>>> GetFullRecordExpressionImpl()
        {
            return emp => new Dictionary<string, object>
            {
                { nameof(BinderEmployee.LastName), emp.LastName },
                { nameof(BinderEmployee.FirstName), emp.FirstName },
                { nameof(BinderEmployee.EMail), emp.EMail },
                { nameof(BinderEmployee.UserId), emp.UserId },
                { nameof(BinderEmployee.TenantUserId), emp.TenantUserId },
                { nameof(BinderEmployee.CompanyInfoId), emp.CompanyInfoId }
            };
        }

        protected override Expression<Func<BinderEmployee, string>> GetLabelExpressionImpl()
        {
            return emp => $"{emp.LastName} {emp.FirstName}";
        }
    }
}
