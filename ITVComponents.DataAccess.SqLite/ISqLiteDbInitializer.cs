using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.DataAccess.SqLite
{
    public interface ISqLiteDbInitializer
    {
        string GetPreciseDatabaseName(string databaseNameRaw);

        void SetupDatabase(IDbWrapper sqLiteLink);
    }
}
