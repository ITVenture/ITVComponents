using System;
using ITVComponents.TypeConversion;

namespace ITVComponents.EFRepo.DynamicData
{
    public class DynamicTableColumnFilter:DynamicTableFilter
    {
        private readonly object value2;
        private readonly string columnName;
        private readonly BinaryCompareFilterOperator op;
        private readonly object value;

        /// <summary>
        /// Initializes a new instance of the DynamicTableColumnFilter class
        /// </summary>
        /// <param name="columnName">the columnName for this Filter-Object</param>
        /// <param name="op">the Filter-Operator</param>
        /// <param name="value">the first value</param>
        /// <param name="value2">the last value</param>
        public DynamicTableColumnFilter(string columnName, BinaryCompareFilterOperator op, object value, object value2):this(columnName, op, value)
        {
            this.value2 = value2;
            if (value != null && op != BinaryCompareFilterOperator.Between)
            {
                throw new InvalidOperationException("Value2 is only acceptable when the Filter is 'Between'");
            }
        }

        public DynamicTableColumnFilter(string columnName, BinaryCompareFilterOperator op, object value)
        {
            this.columnName = columnName;
            this.op = op;
            this.value = value;
        }
        public override string BuildQueryPart(TableColumnResolveCallback tableColumnNameCallback, Func<object, string> addQueryParam, IQuerySyntaxProvider syntaxProvider)
        {
            var fn = tableColumnNameCallback(columnName, out var columnDef);
            return syntaxProvider.BinaryCompareOperation(fn, op, TypeConverter.TryConvert(value, columnDef.Type.ManagedType), TypeConverter.TryConvert(value2, columnDef.Type.ManagedType), addQueryParam);
        }
    }

    public enum BinaryCompareFilterOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        NotLike,
        Like,
        Between,
        NotBetween
    }
}
