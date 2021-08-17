using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.EFRepo.DynamicData;
using ITVComponents.TypeConversion;
using Kendo.Mvc;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DynamicTableSort = ITVComponents.EFRepo.DynamicData.DynamicTableSort;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.DynamicData
{
    public abstract class DynamicDataController: Controller
    {
        private readonly DynamicDataAdapter dataSource;

        protected DynamicDataController(DynamicDataAdapter dataSource)
        {
            this.dataSource = dataSource;
        }

        /// <summary>
        /// Exposes the DataSource object to derived controllers
        /// </summary>
        protected DynamicDataAdapter DataSource => dataSource;

        /// <summary>
        /// Translates the filter-part of the data-source request
        /// </summary>
        /// <param name="request">the data-source request for which to create a datasource-request</param>
        /// <returns>the filter that results out of the given request</returns>
        protected DynamicTableFilter TranslateFilter(DataSourceRequest request)
        {
            return BuildFilters(request.Filters, DynamicCompositeFilterType.And);
        }

        /// <summary>
        /// Translates the posted form-data into a model
        /// </summary>
        /// <param name="tableName">the table-name form which to read the model-fields</param>
        /// <param name="form">the form that contains the posted data</param>
        /// <returns>a dictionary containing the converted data that was posted</returns>
        protected IDictionary<string, object> GetModel(string tableName, IFormCollection form)
        {
            var retVal = dataSource.GetEntity();
            var cols = dataSource.DescribeTable(tableName, true, out _);
            foreach (var tmp in cols)
            {
                if (form.ContainsKey(tmp.ColumnName))
                {
                    var val = form[tmp.ColumnName].FirstOrDefault();
                    var cvf = TypeConverter.TryConvert(val, tmp.Type.ManagedType);
                    if (cvf != null)
                    {
                        retVal.Add(tmp.ColumnName, cvf);
                    }
                }
            }

            return retVal;
        }

        protected DataSourceResult DataResultFor(string tableName, DynamicTableFilter filter, List<DynamicTableSort> sort, string alias, DynamicQueryCallbackProvider callbacks, int? hitsPerPage, int? page, IEnumerable defaultResult = null)
        {
            var tmp = dataSource.QueryDynamicTable(tableName, filter, sort, out var totalCount, alias, callbacks, hitsPerPage, page);
            DataSourceResult result = new DataSourceResult();
            result.Data = defaultResult ?? tmp;
            result.Total = totalCount;
            return result;
        }
        
        protected List<DynamicTableSort> TranslateSort(DataSourceRequest request, DynamicTableSort defaultSort)
        {
            List<DynamicTableSort> retVal = new List<DynamicTableSort>();
            foreach (var sort in request.Sorts)
            {
                retVal.Add(new DynamicTableSort {ColumnName = sort.Member, SortOrder = sort.SortDirection == ListSortDirection.Ascending ? SortOrder.Asc : SortOrder.Desc});
            }

            if (retVal.Count == 0 && defaultSort != null)
            {
                retVal.Add(defaultSort);
            }
            return retVal;
        }

        private DynamicTableFilter BuildFilters(ICollection<IFilterDescriptor> filters, DynamicCompositeFilterType compositeType)
        {
            var retVal = new DynamicCompositeFilter(compositeType);
            foreach (var filter in filters)
            {
                if (filter is CompositeFilterDescriptor cp)
                {
                    retVal.AddFilter(BuildFilters(cp.FilterDescriptors, cp.LogicalOperator == FilterCompositionLogicalOperator.And?DynamicCompositeFilterType.And:DynamicCompositeFilterType.Or));
                }
                else if (filter is FilterDescriptor fd)
                {
                    var value = fd.ConvertedValue;
                    retVal.AddFilter(new DynamicTableColumnFilter(fd.Member, TranslateOperator(fd.Operator, ref value), value));
                }
                else
                {
                    throw new InvalidOperationException("Unable to completely translate filter!");
                }
            }
            
            return retVal;
        }

        private BinaryCompareFilterOperator TranslateOperator(FilterOperator kendoOperator, ref object convertedValue)
        {
            switch (kendoOperator)
            {
                case FilterOperator.IsLessThan:
                    return BinaryCompareFilterOperator.LessThan;
                case FilterOperator.IsLessThanOrEqualTo:
                    return BinaryCompareFilterOperator.LessThanOrEqual;
                case FilterOperator.IsEqualTo:
                    return BinaryCompareFilterOperator.Equal;
                case FilterOperator.IsNotEqualTo:
                    return BinaryCompareFilterOperator.NotEqual;
                case FilterOperator.IsGreaterThanOrEqualTo:
                    return BinaryCompareFilterOperator.GreaterThanOrEqual;
                case FilterOperator.IsGreaterThan:
                    return BinaryCompareFilterOperator.GreaterThan;
                case FilterOperator.StartsWith:
                case FilterOperator.EndsWith:
                case FilterOperator.Contains:
                case FilterOperator.IsContainedIn:
                    if (kendoOperator == FilterOperator.StartsWith || kendoOperator == FilterOperator.Contains || kendoOperator == FilterOperator.IsContainedIn)
                    {
                        convertedValue = $"{convertedValue}%";
                    }

                    if (kendoOperator == FilterOperator.EndsWith || kendoOperator == FilterOperator.Contains || kendoOperator == FilterOperator.IsContainedIn)
                    {
                        convertedValue = $"%{convertedValue}";
                    }
                    
                    return BinaryCompareFilterOperator.Like;
                case FilterOperator.DoesNotContain:
                    convertedValue = $"%{convertedValue}%";
                    return BinaryCompareFilterOperator.NotLike;
                case FilterOperator.IsNull:
                    convertedValue = null;
                    return BinaryCompareFilterOperator.Equal;
                case FilterOperator.IsNotNull:
                    convertedValue = null;
                    return BinaryCompareFilterOperator.NotEqual;
                case FilterOperator.IsEmpty:
                    convertedValue = "";
                    return BinaryCompareFilterOperator.Equal;
                case FilterOperator.IsNotEmpty:
                    convertedValue = "";
                    return BinaryCompareFilterOperator.NotEqual;
                case FilterOperator.IsNullOrEmpty:
                    convertedValue = null;
                    return BinaryCompareFilterOperator.Equal;
                case FilterOperator.IsNotNullOrEmpty:
                    convertedValue = null;
                    return BinaryCompareFilterOperator.NotEqual;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kendoOperator), kendoOperator, null);
            }
        }
    }
}
