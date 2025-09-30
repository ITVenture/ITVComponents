using ITVComponents.EFRepo.DynamicData;
using ITVComponents.EFRepo.Expressions.Models;
using ITVComponents.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.SqlServer
{
    public class SqlQuerySyntaxProvider : IQuerySyntaxProvider
    {
        private static readonly DynamicDataColumnType[] EligibleDbTypes =
        {
            new()
            {
                DataTypeName = "bit",
                DisplayName = "Bit",
                LengthRequired = false,
                ManagedType = typeof(bool)
            },
            new()
            {
                DataTypeName = "tinyint",
                DisplayName = "TinyInt",
                LengthRequired = false,
                ManagedType=typeof(byte)
            },
            new()
            {
                DataTypeName = "smallint",
                DisplayName = "SmallInt",
                LengthRequired = false,
                ManagedType=typeof(short)
            },
            new()
            {
                DataTypeName = "int",
                DisplayName = "Int",
                LengthRequired = false,
                ManagedType=typeof(int)
            },
            new()
            {
                DataTypeName = "bigint",
                DisplayName = "BigInt",
                LengthRequired = false,
                ManagedType=typeof(long)
            },
            new()
            {
                DataTypeName = "float",
                DisplayName = "Float",
                LengthRequired = false,
                ManagedType=typeof(float)
            },
            new()
            {
                DataTypeName = "money",
                DisplayName = "Money",
                LengthRequired = false,
                ManagedType=typeof(decimal)
            },
            new()
            {
                DataTypeName = "numeric",
                DisplayName = "Numeric",
                LengthRequired = false,
                ManagedType=typeof(decimal)
            },
            new()
            {
                DataTypeName = "real",
                DisplayName = "Real",
                LengthRequired = false,
                ManagedType=typeof(decimal)
            },
            new()
            {
                DataTypeName = "smallmoney",
                DisplayName = "SmallMoney",
                LengthRequired = false,
                ManagedType=typeof(decimal)
            },
            new()
            {
                DataTypeName = "decimal",
                DisplayName = "Decimal",
                LengthRequired = false,
                ManagedType=typeof(decimal)
            },
            new()
            {
                DataTypeName = "uniqueidentifier",
                DisplayName = "UniqueIdentifier",
                LengthRequired = false,
                ManagedType=typeof(Guid)
            },
            new()
            {
                DataTypeName = "date",
                DisplayName = "Date",
                LengthRequired = false,
                ManagedType=typeof(DateTime)
            },
            new()
            {
                DataTypeName = "datetime",
                DisplayName = "DateTime",
                LengthRequired = false,
                ManagedType=typeof(DateTime)
            },
            new()
            {
                DataTypeName = "smalldatetime",
                DisplayName = "SmallDatetime",
                LengthRequired = false,
                ManagedType=typeof(DateTime)
            },
            new()
            {
                DataTypeName = "datetime2",
                DisplayName = "DateTime2",
                LengthRequired = false,
                ManagedType=typeof(DateTime)
            },
            new()
            {
                DataTypeName = "varbinary",
                DisplayName = "VarBinary",
                LengthRequired = false,
                ManagedType=typeof(byte[])
            },
            new()
            {
                DataTypeName = "timestamp",
                DisplayName = "TimeStamp",
                LengthRequired = false,
                ManagedType=typeof(byte[])
            },
            new()
            {
                DataTypeName = "varchar",
                DisplayName = "VarChar",
                LengthRequired = true,
                ManagedType=typeof(string)
            },
            new()
            {
                DataTypeName = "char",
                DisplayName = "Char",
                LengthRequired = true,
                ManagedType=typeof(string)
            },
            new()
            {
                DataTypeName = "text",
                DisplayName = "Text",
                LengthRequired = false,
                ManagedType=typeof(string)
            },
            new()
            {
                DataTypeName = "nvarchar",
                DisplayName = "NVarChar",
                LengthRequired = true,
                ManagedType=typeof(string)
            },
            new()
            {
                DataTypeName = "ntext",
                DisplayName = "NText",
                LengthRequired = false,
                ManagedType=typeof(string)
            }
        };

        /// <summary>
        /// /Gets a list of eligible types that can be used to design a table
        /// </summary>
        public DynamicDataColumnType[] EligibleTypes => EligibleDbTypes;

        /// <summary>
        /// Builds a logic Operand chain for a specific boolean operator (AND/OR)
        /// </summary>
        /// <param name="type">the used operator for the boolean operation</param>
        /// <param name="filterParts">the filter parts that need to be chained</param>
        /// <param name="tableColumnNameCallback">a callback returning the full-qualified column for a specified alias</param>
        /// <param name="addQueryParam">a callback that will add a parameter to the query that needs to be executed</param>
        /// <param name="invertEntireFilter">indicates whether to invert this query-part</param>
        /// <returns>a string representing the boolean filter chain represented by the provided params</returns>
        public virtual string BooleanLogicFilter(DynamicCompositeFilterType type, ICollection<DynamicTableFilter> filterParts, TableColumnResolveCallback tableColumnNameCallback, Func<object, string> addQueryParam, bool invertEntireFilter)
        {
            if (filterParts.Count != 0)
            {
                if (type == DynamicCompositeFilterType.And || filterParts.Count == 1)
                {
                    return $"{(invertEntireFilter ? "NOT " : "")}({string.Join(" AND ", from t in filterParts let p = t.BuildQueryPart(tableColumnNameCallback, addQueryParam, this) where p != null select p)})";
                }

                return $"{(invertEntireFilter ? "NOT " : "")}({string.Join(" OR ", from t in filterParts let p = t.BuildQueryPart(tableColumnNameCallback, addQueryParam, this) where p != null select p)})";
            }

            return null;
        }

        public string TranslateExpressionFilter(FilterBase expressionFilter,
            TableColumnDefinitionsCallback tableColumnNameCallback, Func<object, string> addQueryParam)
        {
            if (expressionFilter is CompareFilter cmpf)
            {
                var actualColumns = tableColumnNameCallback(cmpf.PropertyName);
                if (actualColumns.Length == 1)
                {
                    var col = actualColumns[0].ColumnName;
                    switch (cmpf.Operator)
                    {
                        case CompareOperator.Equal:
                            return $"{col} = {addQueryParam(cmpf.Value)}";
                        case CompareOperator.NotEqual:
                            return $"{col} != {addQueryParam(cmpf.Value)}";
                        case CompareOperator.GreaterThan:
                            return $"{col} > {addQueryParam(cmpf.Value)}";
                        case CompareOperator.GreaterThanOrEqual:
                            return $"{col} >= {addQueryParam(cmpf.Value)}";
                        case CompareOperator.LessThan:
                            return $"{col} < {addQueryParam(cmpf.Value)}";
                        case CompareOperator.LessThanOrEqual:
                            return $"{col} <= {addQueryParam(cmpf.Value)}";
                        case CompareOperator.Contains:
                            return $"{col} like {addQueryParam($"%{cmpf.Value}%")}";
                        case CompareOperator.ContainsNot:
                            return $"{col} not like {addQueryParam($"%{cmpf.Value}%")}";
                        case CompareOperator.StartsWith:
                            return $"{col} like {addQueryParam($"{cmpf.Value}%")}";
                        case CompareOperator.StartsNotWith:
                            return $"{col} not like {addQueryParam($"{cmpf.Value}%")}";
                        case CompareOperator.EndsWith:
                            return $"{col} like {addQueryParam($"%{cmpf.Value}")}";
                        case CompareOperator.EndsNotWith:
                            return $"{col} not like {addQueryParam($"%{cmpf.Value}")}";
                        case CompareOperator.Between:
                            return $"{col} between {addQueryParam(cmpf.Value)} and {addQueryParam(cmpf.Value2)}";
                        case CompareOperator.NotBetween:
                            return $"{col} not between {addQueryParam(cmpf.Value)} and {addQueryParam(cmpf.Value2)}";
                        case CompareOperator.IsNull:
                            return $"{col} is null";
                        case CompareOperator.IsNotNull:
                            return $"{col} is not null";
                        case CompareOperator.IsEmpty:
                            return $"{col} = ''";
                        case CompareOperator.IsNotEmpty:
                            return $"{col} != ''";
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                return TranslateExpressionFilter(new CompositeFilter
                {
                    Children = (from t in actualColumns
                                select (FilterBase)new CompareFilter
                                {
                                    PropertyName = t.ColumnName,
                                    Operator = cmpf.Operator,
                                    Value = cmpf.Value,
                                    Value2 = cmpf.Value2
                                }).ToArray(),
                    Operator = BoolOperator.Or
                }, tableColumnNameCallback, addQueryParam);
            }
            if (expressionFilter is CompositeFilter cf)
            {
                if (cf.Children.Length == 0)
                {
                    return null;
                }

                return
                    $"({string.Join($" {cf.Operator} ", from t in cf.Children let u = TranslateExpressionFilter(t, tableColumnNameCallback, addQueryParam) where !string.IsNullOrEmpty(u) select u)})";
            }
            else
            {
                throw new NotSupportedException("The provided filter-type is not supported for dynamic DataSource");
            }
        }

        /// <summary>
        /// Builds a binary compare operation for the given column name operator and values
        /// </summary>
        /// <param name="columnName">the column name to compare</param>
        /// <param name="op">the binary comparison filter operator</param>
        /// <param name="value">the value to compare with</param>
        /// <param name="value2">the value 2 for between operations</param>
        /// <param name="addQueryParam">a callback that will add a parameter to the query-command that needs to be executed</param>
        /// <returns>a string representing the requested binary compare operation</returns>
        public virtual string BinaryCompareOperation(string columnName, BinaryCompareFilterOperator op, object value, object value2, Func<object, string> addQueryParam)
        {
            var param1 = value != null ? addQueryParam(value) : "null";
            var param2 = op == BinaryCompareFilterOperator.Between && value2 != null ? addQueryParam(value) : "null";
            var paramop = "";
            var finalOp = op;
            switch (op)
            {
                case BinaryCompareFilterOperator.Equal:
                    if (value != null)
                    {
                        paramop = "=";
                    }
                    else
                    {
                        paramop = "is";
                    }

                    break;
                case BinaryCompareFilterOperator.NotEqual:
                    if (value != null)
                    {
                        paramop = "!=";
                    }
                    else
                    {
                        paramop = "is not";
                    }


                    break;
                case BinaryCompareFilterOperator.GreaterThan:
                    if (value != null)
                    {
                        paramop = ">";
                    }
                    else
                    {
                        paramop = "is not";
                    }
                    break;
                case BinaryCompareFilterOperator.GreaterThanOrEqual:
                    if (value != null)
                    {
                        paramop = ">=";
                    }
                    break;
                case BinaryCompareFilterOperator.LessThan:
                    if (value != null)
                    {
                        paramop = "<";
                    }
                    else
                    {
                        paramop = "is not";
                    }
                    break;
                case BinaryCompareFilterOperator.LessThanOrEqual:
                    if (value != null)
                    {
                        paramop = "<=";
                    }
                    break;
                case BinaryCompareFilterOperator.Like:
                    if (value != null)
                    {
                        paramop = "like";
                    }
                    break;
                case BinaryCompareFilterOperator.NotLike:
                    {
                        if (value != null)
                        {
                            paramop = "not like";
                        }
                        break;
                    }
                case BinaryCompareFilterOperator.Between:
                    paramop = "between";
                    break;
                case BinaryCompareFilterOperator.NotBetween:
                    paramop = "not between";
                    finalOp = BinaryCompareFilterOperator.Between;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (!string.IsNullOrEmpty(paramop))
            {
                if (finalOp == BinaryCompareFilterOperator.Between)
                {
                    return $"{columnName} {paramop} {param1} and {param2}";
                }

                return $"{columnName} {paramop} {param1}";
            }

            return "1=1";
        }

        public virtual string FullQualifyColumn(string tableName, string columnName, bool autoResolveTableName = true)
        {
            return
                $"{(tableName != null ? $"{FormatObjectName(autoResolveTableName ? ResolveTableName(tableName) : tableName)}." : "")}{FormatObjectName(columnName)}";
        }

        public virtual string FormatTableName(string tableName, bool autoResolve = true)
        {
            return FormatObjectName(autoResolve ? ResolveTableName(tableName) : tableName);
        }

        public virtual string ResolveTableName(string tableName)
        {
            return tableName;
        }

        public virtual string FormatIndexName(string indexName)
        {
            return FormatObjectName(indexName);
        }

        public virtual string FormatConstraintName(string constraintName)
        {
            return FormatObjectName(constraintName);
        }

        public virtual string FormatColumnName(string columnName)
        {
            return FormatObjectName(columnName);
        }

        protected virtual string FormatObjectName(string objectName)
        {
            return $"[{objectName}]";
        }

        public virtual DynamicDataColumnType GetAppropriateType(TableColumnDefinition definition, bool throwOnError)
        {
            var retVal = EligibleTypes.FirstOrDefault(n => n.DataTypeName.Equals(definition.DataType, StringComparison.OrdinalIgnoreCase));
            if (retVal != null)
            {
                return retVal;
            }

            var msg = $"Type {definition.DataType} not supported!";
            if (throwOnError)
            {
                throw new InvalidOperationException(msg);
            }

            LogEnvironment.LogEvent(msg, LogSeverity.Error);
            return null;
        }
    }
}
