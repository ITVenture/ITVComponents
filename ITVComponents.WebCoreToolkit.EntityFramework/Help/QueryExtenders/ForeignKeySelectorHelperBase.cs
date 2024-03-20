using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Expressions.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Help.QueryExtenders
{
    public abstract class ForeignKeySelectorHelperBase<T, TKey>:IForeignKeySelectorHelper
    {
        public Expression GetLabelExpression()
        {
            return GetLabelExpressionImpl();
        }

        protected virtual Expression<Func<T, string>> GetLabelExpressionImpl() => default;

        public Expression GetKeyExpression()
        {
            return GetKeyExpressionImpl();
        }

        protected virtual Expression<Func<T, TKey>> GetKeyExpressionImpl() => default;

        public Expression GetFullRecordExpression()
        {
            return GetFullRecordExpressionImpl();
        }

        protected virtual Expression<Func<T, IDictionary<string, object>>> GetFullRecordExpressionImpl() => default;

        public virtual Sort[] DefaultSorts { get; } = null;
        public virtual Func<string, string[]> ColumnRedirects { get; } = null;
    }
}
