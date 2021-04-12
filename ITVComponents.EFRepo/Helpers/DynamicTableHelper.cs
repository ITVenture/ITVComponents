using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DynamicData;

namespace ITVComponents.EFRepo.Helpers
{
    public static class DynamicTableHelper
    {
        public static TableDiff[] CompareDefinitions(ICollection<TableColumnDefinition> table1Definition, ICollection<TableColumnDefinition> table2Definition)
        {
            var allColumns = table1Definition.Select(n => n.ColumnName).Union(table2Definition.Select(n => n.ColumnName)).Distinct().ToArray();
            var tmp = (from a in allColumns
                join s in table1Definition on a equals s.ColumnName into sg
                from sc in sg.DefaultIfEmpty()
                join d in table2Definition on a equals d.ColumnName into dg
                from dc in dg.DefaultIfEmpty()
                orderby sc?.Position??dc.Position
                select new  TableDiff{ColumnName = a, Table1Def= sc, Table2Def= dc}).ToArray();
            return tmp;
        }
    }
}
