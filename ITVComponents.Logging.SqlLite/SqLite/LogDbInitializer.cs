using System;
using System.Collections.Generic;
using System.Data;
//using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess;
using ITVComponents.DataAccess.SqLite;
using ITVComponents.Plugins;

namespace ITVComponents.Logging.SqlLite.SqLite
{
    public class LogDbInitializer:ISqLiteDbInitializer
    {
        public string GetPreciseDatabaseName(string databaseNameRaw)
        {
            return string.Format(databaseNameRaw, DateTime.Now);
        }

        public void SetupDatabase(IDbWrapper sqLiteLink)
        {
            sqLiteLink.ExecuteCommand(
                @"Create table Log (LogId Integer Primary Key, EventTime DateTime not null, Severity int not null, EventContext varchar(512), EventText text not null);
create index idx_EventTime on Log(EventTime);
create index idx_EventSeverity on Log(Severity);
create index idx_EventContext on Log(EventContext);");
        }
    }
}
