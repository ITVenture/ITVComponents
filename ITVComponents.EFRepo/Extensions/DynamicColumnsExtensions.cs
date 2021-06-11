using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.EFRepo.DynamicData;

namespace ITVComponents.EFRepo.Extensions
{
    public static class DynamicColumnsExtensions
    {
        public static IList<TableColumnDefinition> ColumnsReadOnly(this IList<TableColumnDefinition> cols, params string[] readOnlyColumns)
        {
            (from c in cols join r in readOnlyColumns on c.ColumnName.ToLower() equals r.ToLower() select c).ForEach(n => n.ColumnReadOnly());
            return cols;
        }

        public static IList<TableColumnDefinition> ColumnsHidden(this IList<TableColumnDefinition> cols, params string[] readOnlyColumns)
        {
            (from c in cols join r in readOnlyColumns on c.ColumnName.ToLower() equals r.ToLower() select c).ForEach(n => n.ColumnHidden());
            return cols;
        }

        public static IList<TableColumnDefinition> WithColumns(this IList<TableColumnDefinition> cols, Action<TableColumnDefinition> action, params string[] readOnlyColumns)
        {
            (from c in cols join r in readOnlyColumns on c.ColumnName.ToLower() equals r.ToLower() select c).ForEach(action);
            return cols;
        }

        public static TableColumnDefinition ColumnReadOnly(this TableColumnDefinition col)
        {
            col.ReadOnly = true;
            return col;
        }

        public static TableColumnDefinition ColumnHidden(this TableColumnDefinition col)
        {
            col.Hidden = true;
            return col;
        }
    }
}