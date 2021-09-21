using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using ITVComponents.DataAccess;

namespace ITVComponents.Logging.SqlLite.Viewer
{
    public class FilterSettings
    {
        public ComboBoxItem EventType { get; set; }

        public DateTime StartDate { get; set; } = DateTime.Today;

        public DateTime EndDate { get; set; } = DateTime.Now;

        public string CategoryFilter { get; set; }

        public string EventFilter { get; set; }

        public override string ToString()
        {
            StringBuilder retVal = new StringBuilder();
            string typeFilter = EventType?.Tag as string;
            if (!string.IsNullOrEmpty(typeFilter))
            {
                retVal.Append(typeFilter);
            }

            retVal.Append($"{(retVal.Length == 0 ? "" : " and ")}EventTime between $startDate and $endDate");
            if (!string.IsNullOrEmpty(CategoryFilter))
            {
                retVal.Append(" and EventContext like $evContext");
            }

            if (!string.IsNullOrEmpty(EventFilter))
            {
                retVal.Append(" and EventText like $evText");
            }

            if (retVal.Length != 0)
            {
                retVal.Insert(0, "where ");
            }

            return retVal.ToString();
        }

        public IDbDataParameter[] GetArguments(IDbWrapper database)
        {
            List<IDbDataParameter> retVal = new List<IDbDataParameter>
            {
                database.GetParameter("startDate", StartDate),
                database.GetParameter("endDate", EndDate)
            };

            if (!string.IsNullOrEmpty(CategoryFilter))
            {
                retVal.Add(database.GetParameter("evContext", $"%{CategoryFilter}%"));
            }

            if (!string.IsNullOrEmpty(EventFilter))
            {
                retVal.Add(database.GetParameter("evText", $"%{EventFilter}%"));
            }

            return retVal.ToArray();
        }
    }
}
