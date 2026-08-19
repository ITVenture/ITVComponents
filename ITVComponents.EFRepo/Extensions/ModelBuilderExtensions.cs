using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.EFRepo.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ITVComponents.EFRepo.Extensions
{
    public static class ModelBuilderExtensions
    {
        /// <summary>
        /// Configures a DbContext and sets all table-names to the Property-Names that you have specified
        /// </summary>
        /// <param name="builder">the ModelBuilder</param>
        /// <param name="targetContext">the targetcontext on which to set the tablenames</param>
        public static void TableNamesFromProperties(this ModelBuilder builder, DbContext targetContext)
        {
            Type t = targetContext.GetType();
            builder.TableNamesFromProperties(t);
        }

        /// <summary>
        /// Configures a DbContext and sets all table-names to the Property-Names that you have specified
        /// </summary>
        /// <param name="builder">the ModelBuilder</param>
        /// <param name="targetContextType">the dataContext-Type on which to set the tablenames</param>
        /// <remarks>
        /// Die Suche laeuft ausdruecklich ueber die GEERBTEN Eigenschaften mit (<see cref="BindingFlags.FlattenHierarchy"/>) -
        /// genau daher kommen die Tabellennamen <c>Users</c>, <c>Roles</c> und <c>UserClaims</c> statt <c>AspNetUsers</c> usw.
        /// Was dabei jedoch <b>ausdruecklich ausgeschlossen</b> wurde, bleibt aussen vor: siehe die Anmerkung in der Schleife.
        /// </remarks>
        public static void TableNamesFromProperties(this ModelBuilder builder, Type targetContextType)
        {
            Type dbsType = typeof(DbSet<>);
            HashSet<Type> types = new HashSet<Type>();
            var conventionModel = (IConventionModel)builder.Model;
            PropertyInfo[] allDbSets =
                targetContextType.GetProperties(BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.Public |
                                BindingFlags.FlattenHierarchy)
                    .Where(n => n.PropertyType.IsGenericType && n.PropertyType.GetGenericTypeDefinition() == dbsType)
                    .ToArray();
            try
            {
                foreach (PropertyInfo pi in allDbSets)
                {
                    Type[] tableArg = pi.PropertyType.GetGenericArguments();

                    // Ein Typ, den das Modell ABSICHTLICH ausgeschlossen hat, gehoert nicht hierher zurueck.
                    // "Ignore" heisst "gehoert nicht in dieses Modell", und eine Konvention zur Tabellenbenennung
                    // hat kein Recht, das zu ueberstimmen.
                    //
                    // Ohne diese Pruefung reicht es, dass eine Basisklasse ein DbSet fuer etwas mitbringt, das sie
                    // selbst ausschliesst: .NET 10 hat IdentityUserContext ein UserPasskeys-DbSet hinzugefuegt und
                    // den Entitaetstyp dazu ausdruecklich ausgeschlossen (Passkeys sind opt-in). Der Aufruf von
                    // builder.Entity() zog ihn trotzdem herein - samt seiner Data-Eigenschaft, die dann als
                    // schluessellose Entitaet im Modell stand und JEDEN Identity-Kontext der Bibliothek beim
                    // Validieren scheitern liess ("IdentityPasskeyData requires a primary key"), auf allen
                    // Providern und damit auch zur Laufzeit.
                    //
                    // Bewusst nur ABSICHTLICHE Ausschluesse: was blosse Konvention ausgeschlossen hat, wird wie
                    // bisher benannt - der Fix soll die Luecke schliessen und sonst nichts am Verhalten aendern.
                    var ignoredAt = conventionModel.FindIgnoredConfigurationSource(tableArg[0]);
                    if (ignoredAt == ConfigurationSource.Explicit || ignoredAt == ConfigurationSource.DataAnnotation)
                    {
                        continue;
                    }

                    if (types.Contains(tableArg[0]))
                    {
                        throw new InvalidOperationException("Do not use the same Entity-Type twice!");
                    }

                    types.Add(tableArg[0]);
                    var entityConfig = builder.Entity(tableArg[0]);
                    var att = Attribute.GetCustomAttribute(pi, typeof(ManualTableNameAttribute)) as ManualTableNameAttribute;
                    var isBinderTable = Attribute.IsDefined(tableArg[0], typeof(BinderEntityAttribute), true) ||
                                        Attribute.IsDefined(pi, typeof(BinderEntityAttribute), true);
                    if (!isBinderTable)
                    {
                        entityConfig.ToTable(att?.TableName ?? pi.Name);
                    }
                    else
                    {
                        entityConfig.ToTable(att?.TableName ?? pi.Name, b => b.ExcludeFromMigrations());
                    }
                }
            }
            finally
            {
                types.Clear();
            }
        }
    }
}
