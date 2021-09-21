using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace ITVComponents.DataAccess
{
    public class Where
    {
        /// <summary>
        /// The list of sub expressions contained in this expression
        /// </summary>
        private List<Where> subWheres;

        /// <summary>
        /// the parent where
        /// </summary>
        private Where parent;

        /// <summary>
        /// the Query mode of this item
        /// </summary>
        private WhereMode whereMode = WhereMode.Where;

        /// <summary>
        /// the queried column of this Where
        /// </summary>
        private Column column;

        /// <summary>
        /// Operators described by the ComparisonMode enum
        /// </summary>
        private readonly string[] Operators = { "=", "!=", ">", "<", ">=", "<=", "Between", "Like", "not Like" };

        /// <summary>
        /// Contains regexes used to compare string values
        /// </summary>
        private static Dictionary<string, Regex> regexes = new Dictionary<string, Regex>();

        /// <summary>
        /// Initializes a new instance of the Where class
        /// </summary>
        /// <param name="column">the Column tested for this where item</param>
        public Where(Column column)
            : this()
        {
            this.column = column;
        }

        /// <summary>
        /// Prevents a default instance of the Where class from being created
        /// </summary>
        private Where()
        {
            subWheres = new List<Where>();
        }

        /// <summary>
        /// Adds an And expression to this Where
        /// </summary>
        /// <param name="column">the Search Column</param>
        /// <returns>this where</returns>
        public Where And(Column column)
        {
            subWheres.Add(CreateWhere(WhereMode.And, column));
            return this;
        }

        /// <summary>
        /// Adds an Or expression to this Where
        /// </summary>
        /// <param name="column">the search Column</param>
        /// <returns>this where</returns>
        public Where Or(Column column)
        {
            subWheres.Add(CreateWhere(WhereMode.Or, column));
            return this;
        }

        /// <summary>
        /// Opens an And bracket and returns the first item within the bracket
        /// </summary>
        /// <param name="column">the Column to search for</param>
        /// <returns>the first where clause within the bracket</returns>
        public Where OpenBracketAnd(Column column)
        {
            Where retVal = CreateWhere(WhereMode.And, column);
            subWheres.Add(retVal);
            return retVal;
        }

        /// <summary>
        /// Opens an Or bracket and returns the first item within the bracket
        /// </summary>
        /// <param name="column">the Column to search for</param>
        /// <returns>the first where clause within the bracket</returns>
        public Where OpenBracketOr(Column column)
        {
            Where retVal = CreateWhere(WhereMode.Or, column);
            subWheres.Add(retVal);
            return retVal;
        }

        /// <summary>
        /// Closes the Bracket and returns to the underlaying item
        /// </summary>
        /// <returns></returns>
        public Where CloseBracket()
        {
            return parent;
        }

        /// <summary>
        /// Gets the Command including arguments
        /// </summary>
        /// <param name="wrapper">the wrapper used to connect to the database</param>
        /// <param name="arguments">the arguments used to bind all the search columns</param>
        /// <returns>a string representing this where clause</returns>
        public string GetCommand(IDbWrapper wrapper,out IDbDataParameter[] arguments)
        {
            StringBuilder bld = new StringBuilder();
            List<IDbDataParameter> parameters = new List<IDbDataParameter>();
            BuildCommand(wrapper, bld, parameters);
            arguments = parameters.ToArray();
            return bld.ToString();
        }

        /// <summary>
        /// Validates the passed result for fitting the query
        /// </summary>
        /// <param name="result">the result to be validated with this query</param>
        /// <returns>a value indicating whether this result matches the provided query</returns>
        public bool Validate(DynamicResult result)
        {
            bool retVal = Validate(result, column);
            foreach (Where w in subWheres)
            {
                if (w.whereMode == WhereMode.And)
                {
                    if (retVal)
                    {
                        retVal &= w.Validate(result);
                    }
                }
                else
                {
                    if (!retVal)
                    {
                        retVal |= w.Validate(result);
                    }
                }
            }

            return retVal;
        }

        /// <summary>
        /// Validates a Dynamic Result against a DataColumn
        /// </summary>
        /// <param name="result">the result to check</param>
        /// <param name="column">the column to check against it</param>
        /// <returns>a value indicating whether the provided result matches the query in the column</returns>
        private static bool Validate(DynamicResult result, Column column)
        {
            dynamic d = result[column.ColumnName];
            if (d == null && column.Value == null && column.ComparisonMode == ComparisonMode.Equal)
            {
                return true;
            }
            if (d == null && column.Value != null && column.ComparisonMode == ComparisonMode.NotEqual)
            {
                return true;
            }
            if (d == null)
            {
                return false;
            }

            switch (column.ComparisonMode)
            {
                case ComparisonMode.Equal:
                    {
                        return (d is string) ? d.Equals(column.Value, StringComparison.OrdinalIgnoreCase) : d.Equals(column.Value);
                    }
                case ComparisonMode.NotEqual:
                    {
                        return d != column.Value;
                    }
                case ComparisonMode.GreaterThan:
                    {
                        return d > column.Value;
                    }
                case ComparisonMode.SmallerThan:
                    {
                        return d < column.Value;
                    }
                case ComparisonMode.GreaterThanOrEqual:
                    {
                        return d >= column.Value;
                    }
                case ComparisonMode.SmallerThanOrEqual:
                    {
                        return d <= column.Value;
                    }
                case ComparisonMode.Between:
                    {
                        return d >= column.Value && d <= column.Value2;
                    }
                case ComparisonMode.Like:
                    {
                        return ValidateRx(d, column.Value);
                    }
                case ComparisonMode.NotLike:
                    {
                        return !ValidateRx(d, column.Value);
                    }
            }

            return false;
        }

        /// <summary>
        /// Adds a subQuery to the current query
        /// </summary>
        /// <param name="subWhere">the subQuery to add as And-subQuery to an existing query</param>
        /// <returns>the created Where containing the subWhere</returns>
        public Where OpenBracketAnd(Where subWhere)
        {
            subWhere.whereMode = WhereMode.And;
            subWhere.parent = this;
            subWheres.Add(subWhere);
            return subWhere;
        }

        /// <summary>
        /// Adds a subQuery to the current query
        /// </summary>
        /// <param name="subWhere">the subQuery to add as Or-subQuery to an existing query</param>
        /// <returns>the created Where containing the subWhere</returns>
        public Where OpenBracketOr(Where subWhere)
        {
            subWhere.whereMode = WhereMode.Or;
            subWhere.parent = this;
            subWheres.Add(subWhere);
            return subWhere;
        }

        /// <summary>
        /// Gets the String representation of this Where statement
        /// </summary>
        /// <returns>the string reresentation of this here statement</returns>
        public override string ToString()
        {
            StringBuilder bld = new StringBuilder();
            BuildCommand(null, bld, null);
            return bld.ToString();
        }

        /// <summary>
        /// Validates like expression
        /// </summary>
        /// <param name="value">the value to compare as like</param>
        /// <param name="rx">the like expression</param>
        /// <returns>a value indicating whether the value matches the like expression</returns>
        private static bool ValidateRx(string value, string rx)
        {
            if (!regexes.ContainsKey(rx))
            {
                lock (regexes)
                {
                    string expression = string.Format("^{0}$",
                                                      rx.Replace(".", "\\.").Replace("%%", "\\PERCENT\\").Replace("__",
                                                                                                                  "\\UNDERSCORE\\")
                                                          .Replace("%", ".*").Replace("_", ".").Replace("\\PERCENT\\",
                                                                                                        "%").
                                                          Replace("\\UNDERSCORE\\", "_"));
                    regexes.Add(rx, new Regex(expression,
                              RegexOptions.CultureInvariant | RegexOptions.Multiline |
                              RegexOptions.IgnoreCase));
                }
            }

            Regex r = regexes[rx];
            return r.Match(value).Success;
        }

        /// <summary>
        /// builds the Command of a Where including nested wheres
        /// </summary>
        /// <param name="wrapper"></param>
        /// <param name="bld"></param>
        /// <param name="argumentList"></param>
        private void BuildCommand(IDbWrapper wrapper, StringBuilder bld, List<IDbDataParameter> argumentList)
        {
            string parameterName = null;
            string parameterName2 = null;
            IDbDataParameter param = null;
            IDbDataParameter param2 = null;
            if (wrapper != null && argumentList != null)
            {
                int id = argumentList.Count + 1;
                parameterName = string.Format("Argument{0}", id);
                parameterName2 = string.Format("Argument{0}", id + 1);
                param = wrapper.GetParameter(parameterName, column.Value ?? DBNull.Value);
                parameterName = param.ParameterName;
                argumentList.Add(param);
                if (column.ComparisonMode == ComparisonMode.Between)
                {
                    param2 = wrapper.GetParameter(parameterName2, column.Value2 ?? DBNull.Value);
                    parameterName2 = param2.ParameterName;
                    argumentList.Add(param2);
                }
            }

            bool brackets = subWheres.Count != 0;
            bld.AppendFormat("{0} {1}{2} {3} {4}", whereMode, brackets ? "(" : "", column.ColumnName, Operators[(int)column.ComparisonMode], parameterName ?? string.Format("\"{0}\"", column.Value));
            if (column.ComparisonMode == ComparisonMode.Between)
            {
                bld.AppendFormat(" and {0}", parameterName2 ?? string.Format("\"{0}\"", column.Value2));
            }

            bld.Append(" ");
            foreach (Where w in subWheres)
            {
                w.BuildCommand(wrapper, bld, argumentList);
            }

            if (brackets)
            {
                bld.Append(")");
            }
        }

        /// <summary>
        /// Creates a new Where item
        /// </summary>
        /// <param name="mode">the Wheremode of the new item</param>
        /// <param name="column">the search column of this query</param>
        /// <returns>the created where for further usage</returns>
        private Where CreateWhere(WhereMode mode, Column column)
        {
            return new Where { whereMode = mode, column = column, parent = this };
        }

        /// <summary>
        /// a single column expression
        /// </summary>
        public class Column
        {
            /// <summary>
            /// Gets or sets the ColumnName of this Column
            /// </summary>
            public string ColumnName { get; set; }

            /// <summary>
            /// Gets or sets the Expected Value for this Column
            /// </summary>
            public dynamic Value { get; set; }

            /// <summary>
            /// Gets or sets the 2nd Value for a Between Column
            /// </summary>
            public dynamic Value2 { get; set; }

            /// <summary>
            /// Gets or sets the Comparison mode for this Column
            /// </summary>
            public ComparisonMode ComparisonMode { get; set; }
        }

        /// <summary>
        /// A list of valid Comparison operation for Database Columns
        /// </summary>
        public enum ComparisonMode
        {
            /// <summary>
            /// Test for Equality
            /// </summary>
            Equal = 0,

            /// <summary>
            /// Test for Non-Equality
            /// </summary>
            NotEqual = 1,

            /// <summary>
            /// Test Column to be Greater than value
            /// </summary>
            GreaterThan = 2,

            /// <summary>
            /// Test Column to be Smaller than value
            /// </summary>
            SmallerThan = 3,

            /// <summary>
            /// Test Column to be Greater than or equal to value
            /// </summary>
            GreaterThanOrEqual = 4,

            /// <summary>
            /// Test Column to be Smaller than or equal to value
            /// </summary>
            SmallerThanOrEqual = 5,

            /// <summary>
            /// Test Column to be Between two passed values
            /// </summary>
            Between = 6,

            /// <summary>
            /// Test Column to be Like a passed string value
            /// </summary>
            Like = 7,

            /// <summary>
            /// Test Column to be Not Like a passed string value
            /// </summary>
            NotLike = 8
        }

        /// <summary>
        /// The wheremode for a Where item
        /// </summary>
        private enum WhereMode
        {
            /// <summary>
            /// Initial Where
            /// </summary>
            Where,

            /// <summary>
            /// And linked
            /// </summary>
            And,

            /// <summary>
            /// Or linked
            /// </summary>
            Or
        }
    }
}
