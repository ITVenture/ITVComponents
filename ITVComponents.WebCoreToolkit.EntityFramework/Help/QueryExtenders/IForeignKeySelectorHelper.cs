using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Expressions.Models;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Help.QueryExtenders
{
    public interface IForeignKeySelectorHelper
    {
        Expression GetLabelExpression();

        Expression GetKeyExpression();

        Expression GetFullRecordExpression();

        Sort[] DefaultSorts { get; }

        Func<string, string[]> ColumnRedirects { get; }

        FilterBase GetCustomFilterAddition(IDictionary<string, object> customFilterInfo);
    }
}
