using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.DataSources;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations
{
    public class DiagnosticResultAttribute : CustomValueSourceAttribute
    {
        public string DiagnosticQueryName { get; }

        public DiagnosticResultAttribute(string diagnosticQueryName)
        {
            this.DiagnosticQueryName = diagnosticQueryName;
        }

        protected override object GetCustomValueFor(object originalObject, Func<Type, object> requestInstance)
        {
            var services = (IServiceProvider)requestInstance(typeof(IServiceProvider));
            var options = (DiagnoseQueryOptions)requestInstance(typeof(DiagnoseQueryOptions));
            if (services != null && options != null)
            {
                IWrappedDataSource context;
                DiagnosticsQueryDefinition qr;
                if (options.KnownQueries.ContainsKey(DiagnosticQueryName))
                {
                    var item = options.KnownQueries[DiagnosticQueryName];
                    context = item.Context;
                    qr = item.Query;
                }
                else
                {
                    context = services.ContextForDiagnosticsQuery(DiagnosticQueryName, options.Area, out qr);
                    options.KnownQueries.Add(DiagnosticQueryName, new DiagnoseQueryHelper.DiagQueryItem
                    {
                        Query = qr,
                        Context = context
                    });
                }

                if (context != null)
                {
                    return context.RunDiagnosticsQuery(qr, options.Arguments).Cast<object>().FirstOrDefault();
                    /*if (p.Property.PropertyType == typeof(SimpleTriStateResult))
                    {
                        p.Property.SetValue(ret, context.RunDiagnosticsQuery(qr, queryArguments).Cast<SimpleTriStateResult>().FirstOrDefault());
                    }
                    else
                    {
                        p.Property.SetValue(ret, context.RunDiagnosticsQuery(qr, queryArguments).Cast<object>().FirstOrDefault());
                    }*/
                }
            }

            return null;
        }
    }
}
