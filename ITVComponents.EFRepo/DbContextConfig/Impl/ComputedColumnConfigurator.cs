using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.DbContextConfig.Impl
{
    public class ComputedColumnConfigurator<T, TProperty>:IEntityConfigurator where T : class
    {
        private readonly string customSql;
        private readonly Expression<Func<T, TProperty>> propertyFunc;
        private readonly bool? stored;

        /// <param name="stored">
        /// ob die Spalte gespeichert wird. <c>null</c> laesst die Entscheidung beim Provider - was auf
        /// SQL Server richtig ist, weil dort <c>persisted</c> ohnehin im Ausdruck selbst steht.
        /// <b>PostgreSQL braucht hier ausdruecklich <c>true</c></b>: unterhalb von Version 18 kennt es
        /// nur gespeicherte berechnete Spalten und bricht das Erzeugen der Migration sonst mit
        /// „Virtual (non-stored) generated columns are only supported on PostgreSQL 18 and up" ab.
        /// </param>
        public ComputedColumnConfigurator(string customSql, Expression<Func<T, TProperty>> propertyFunc, bool? stored = null)
        {
            this.customSql = customSql;
            this.propertyFunc = propertyFunc;
            this.stored = stored;
        }

        public void ConfigureEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<T>().Property(propertyFunc).HasComputedColumnSql(customSql, stored);
        }
    }
}
