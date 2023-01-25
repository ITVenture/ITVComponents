using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.DbContextConfig.Impl
{
    public class TableRedirect<T>: IEntityConfigurator where T : class
    {
        private readonly string tableName;
        private readonly string schema;

        public TableRedirect(string tableName, string schema)
        {
            this.tableName = tableName;
            this.schema = schema;
        }

        public TableRedirect(string tableName) : this(tableName, null)
        {
        }

        public void ConfigureEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<T>().ToTable(tableName, schema);
        }
    }
}
