using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ITVComponents.EFRepo.Helpers
{
    public interface IDbSet
    {
        /// <summary>
        /// Begins tracking the given entity, and any other reachable entities that are not
        /// already being tracked, in the Microsoft.EntityFrameworkCore.EntityState.Added
        /// state such that they will be inserted into the database when Microsoft.EntityFrameworkCore.DbContext.SaveChanges
        /// is called.
        /// Use Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.State to set the
        /// state of only a single entity.
        /// </summary>
        /// <param name="entity">The entity to add.</param>
        /// <returns>The Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry for the entity. The entry provides access to change tracking information and operations for the entity.
        /// </returns>
        EntityEntry Add(object entity);
        
        /// <summary>
        /// Begins tracking the given entities, and any other reachable entities that are
        /// not already being tracked, in the Microsoft.EntityFrameworkCore.EntityState.Added
        /// state such that they will be inserted into the database when Microsoft.EntityFrameworkCore.DbContext.SaveChanges
        /// is called.
        /// </summary>
        /// <param name="entities">The entities to add.</param>
        void AddRange(IEnumerable entities);
        
        /// <summary>
        /// Begins tracking the given entities, and any other reachable entities that are
        /// not already being tracked, in the Microsoft.EntityFrameworkCore.EntityState.Added
        /// state such that they will be inserted into the database when Microsoft.EntityFrameworkCore.DbContext.SaveChanges
        /// is called.
        /// </summary>
        /// <param name="entities">The entities to add.</param>
        void AddRange(params object[] entities);
        
        /// <summary>
        /// Returns this object typed as System.Linq.IQueryable`1.
        /// This is a convenience method to help with disambiguation of extension methods
        /// in the same namespace that extend both interfaces.
        /// </summary>
        /// <returns>This object.</returns>
        IQueryable AsQueryable();
        //
        // Zusammenfassung:
        //     Begins tracking the given entity and entries reachable from the given entity
        //     using the Microsoft.EntityFrameworkCore.EntityState.Unchanged state by default,
        //     but see below for cases when a different state will be used.
        //     Generally, no database interaction will be performed until Microsoft.EntityFrameworkCore.DbContext.SaveChanges
        //     is called.
        //     A recursive search of the navigation properties will be performed to find reachable
        //     entities that are not already being tracked by the context. All entities found
        //     will be tracked by the context.
        //     For entity types with generated keys if an entity has its primary key value set
        //     then it will be tracked in the Microsoft.EntityFrameworkCore.EntityState.Unchanged
        //     state. If the primary key value is not set then it will be tracked in the Microsoft.EntityFrameworkCore.EntityState.Added
        //     state. This helps ensure only new entities will be inserted. An entity is considered
        //     to have its primary key value set if the primary key property is set to anything
        //     other than the CLR default for the property type.
        //     For entity types without generated keys, the state set is always Microsoft.EntityFrameworkCore.EntityState.Unchanged.
        //     Use Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.State to set the
        //     state of only a single entity.
        //
        // Parameter:
        //   entity:
        //     The entity to attach.
        //
        // Rückgabewerte:
        //     The Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry for the entity.
        //     The entry provides access to change tracking information and operations for the
        //     entity.
        EntityEntry Attach(object entity);
        
        /// <summary>
        /// Begins tracking the given entities and entries reachable from the given entities
        /// using the Microsoft.EntityFrameworkCore.EntityState.Unchanged state by default,
        /// but see below for cases when a different state will be used.
        ///     Generally, no database interaction will be performed until Microsoft.EntityFrameworkCore.DbContext.SaveChanges
        /// is called.
        ///     A recursive search of the navigation properties will be performed to find reachable
        ///     entities that are not already being tracked by the context. All entities found
        ///     will be tracked by the context.
        ///     For entity types with generated keys if an entity has its primary key value set
        /// then it will be tracked in the Microsoft.EntityFrameworkCore.EntityState.Unchanged
        /// state. If the primary key value is not set then it will be tracked in the Microsoft.EntityFrameworkCore.EntityState.Added
        /// state. This helps ensure only new entities will be inserted. An entity is considered
        ///     to have its primary key value set if the primary key property is set to anything
        ///     other than the CLR default for the property type.
        ///     For entity types without generated keys, the state set is always Microsoft.EntityFrameworkCore.EntityState.Unchanged.
        ///     Use Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.State to set the
        /// state of only a single entity.
        /// </summary>
        /// <param name="entities">The entities to attach.</param>
        void AttachRange(IEnumerable entities);
        
        /// <summary>
        /// Begins tracking the given entities and entries reachable from the given entities
        /// using the Microsoft.EntityFrameworkCore.EntityState.Unchanged state by default,
        /// but see below for cases when a different state will be used.
        ///     Generally, no database interaction will be performed until Microsoft.EntityFrameworkCore.DbContext.SaveChanges
        /// is called.
        ///     A recursive search of the navigation properties will be performed to find reachable
        ///     entities that are not already being tracked by the context. All entities found
        ///     will be tracked by the context.
        ///     For entity types with generated keys if an entity has its primary key value set
        /// then it will be tracked in the Microsoft.EntityFrameworkCore.EntityState.Unchanged
        /// state. If the primary key value is not set then it will be tracked in the Microsoft.EntityFrameworkCore.EntityState.Added
        /// state. This helps ensure only new entities will be inserted. An entity is considered
        ///     to have its primary key value set if the primary key property is set to anything
        ///     other than the CLR default for the property type.
        ///     For entity types without generated keys, the state set is always Microsoft.EntityFrameworkCore.EntityState.Unchanged.
        ///     Use Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.State to set the
        /// state of only a single entity.
        /// </summary>
        /// <param name="entities">The entities to attach.</param>
        void AttachRange(params object[] entities);
        //
        // Zusammenfassung:
        //     Finds an entity with the given primary key values. If an entity with the given
        //     primary key values is being tracked by the context, then it is returned immediately
        //     without making a request to the database. Otherwise, a query is made to the database
        //     for an entity with the given primary key values and this entity, if found, is
        //     attached to the context and returned. If no entity is found, then null is returned.
        //
        // Parameter:
        //   keyValues:
        //     The values of the primary key for the entity to be found.
        //
        // Rückgabewerte:
        //     The entity found, or null.
        object Find(params object[] keyValues);

        /// <summary>
        /// Gets the first property of the primary-key for the given entity
        /// </summary>
        /// <param name="entity">the object for which to get the primary-key value</param>
        /// <returns>the primary-key value of the provided entity</returns>
        object GetIndex(object entity);

        /// <summary>
        /// Finds an entity with specified values in a dictionary. The dataset creates an expression based on the query and searches the requested object on the db. The operation fails, if the result is not unique
        /// </summary>
        /// <param name="query">the query that describes the requested record uniquely</param>
        /// <returns>the requested value</returns>
        object FindWithQuery(Dictionary<string, object> query, bool ignoreNotFound);
        //
        // Zusammenfassung:
        //     Begins tracking the given entity in the Microsoft.EntityFrameworkCore.EntityState.Deleted
        //     state such that it will be removed from the database when Microsoft.EntityFrameworkCore.DbContext.SaveChanges
        //     is called.
        //
        // Parameter:
        //   entity:
        //     The entity to remove.
        //
        // Rückgabewerte:
        //     The Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry`1 for the entity.
        //     The entry provides access to change tracking information and operations for the
        //     entity.
        //
        // Hinweise:
        //     If the entity is already tracked in the Microsoft.EntityFrameworkCore.EntityState.Added
        //     state then the context will stop tracking the entity (rather than marking it
        //     as Microsoft.EntityFrameworkCore.EntityState.Deleted) since the entity was previously
        //     added to the context and does not exist in the database.
        //     Any other reachable entities that are not already being tracked will be tracked
        //     in the same way that they would be if Microsoft.EntityFrameworkCore.DbSet`1.Attach(`0)
        //     was called before calling this method. This allows any cascading actions to be
        //     applied when Microsoft.EntityFrameworkCore.DbContext.SaveChanges is called.
        //     Use Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.State to set the
        //     state of only a single entity.
        EntityEntry Remove(object entity);
        //
        // Zusammenfassung:
        //     Begins tracking the given entities in the Microsoft.EntityFrameworkCore.EntityState.Deleted
        //     state such that they will be removed from the database when Microsoft.EntityFrameworkCore.DbContext.SaveChanges
        //     is called.
        //
        // Parameter:
        //   entities:
        //     The entities to remove.
        //
        // Hinweise:
        //     If any of the entities are already tracked in the Microsoft.EntityFrameworkCore.EntityState.Added
        //     state then the context will stop tracking those entities (rather than marking
        //     them as Microsoft.EntityFrameworkCore.EntityState.Deleted) since those entities
        //     were previously added to the context and do not exist in the database.
        //     Any other reachable entities that are not already being tracked will be tracked
        //     in the same way that they would be if Microsoft.EntityFrameworkCore.DbSet`1.AttachRange(`0[])
        //     was called before calling this method. This allows any cascading actions to be
        //     applied when Microsoft.EntityFrameworkCore.DbContext.SaveChanges is called.
        void RemoveRange(params object[] entities);
        //
        // Zusammenfassung:
        //     Begins tracking the given entities in the Microsoft.EntityFrameworkCore.EntityState.Deleted
        //     state such that they will be removed from the database when Microsoft.EntityFrameworkCore.DbContext.SaveChanges
        //     is called.
        //
        // Parameter:
        //   entities:
        //     The entities to remove.
        //
        // Hinweise:
        //     If any of the entities are already tracked in the Microsoft.EntityFrameworkCore.EntityState.Added
        //     state then the context will stop tracking those entities (rather than marking
        //     them as Microsoft.EntityFrameworkCore.EntityState.Deleted) since those entities
        //     were previously added to the context and do not exist in the database.
        //     Any other reachable entities that are not already being tracked will be tracked
        //     in the same way that they would be if Microsoft.EntityFrameworkCore.DbSet`1.AttachRange(System.Collections.Generic.IEnumerable{`0})
        //     was called before calling this method. This allows any cascading actions to be
        //     applied when Microsoft.EntityFrameworkCore.DbContext.SaveChanges is called.
        void RemoveRange(IEnumerable entities);
        //
        // Zusammenfassung:
        //     Begins tracking the given entity and entries reachable from the given entity
        //     using the Microsoft.EntityFrameworkCore.EntityState.Modified state by default,
        //     but see below for cases when a different state will be used.
        //     Generally, no database interaction will be performed until Microsoft.EntityFrameworkCore.DbContext.SaveChanges
        //     is called.
        //     A recursive search of the navigation properties will be performed to find reachable
        //     entities that are not already being tracked by the context. All entities found
        //     will be tracked by the context.
        //     For entity types with generated keys if an entity has its primary key value set
        //     then it will be tracked in the Microsoft.EntityFrameworkCore.EntityState.Modified
        //     state. If the primary key value is not set then it will be tracked in the Microsoft.EntityFrameworkCore.EntityState.Added
        //     state. This helps ensure new entities will be inserted, while existing entities
        //     will be updated. An entity is considered to have its primary key value set if
        //     the primary key property is set to anything other than the CLR default for the
        //     property type.
        //     For entity types without generated keys, the state set is always Microsoft.EntityFrameworkCore.EntityState.Modified.
        //     Use Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.State to set the
        //     state of only a single entity.
        //
        // Parameter:
        //   entity:
        //     The entity to update.
        //
        // Rückgabewerte:
        //     The Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry for the entity.
        //     The entry provides access to change tracking information and operations for the
        //     entity.
        EntityEntry Update(object entity);
        //
        // Zusammenfassung:
        //     Begins tracking the given entities and entries reachable from the given entities
        //     using the Microsoft.EntityFrameworkCore.EntityState.Modified state by default,
        //     but see below for cases when a different state will be used.
        //     Generally, no database interaction will be performed until Microsoft.EntityFrameworkCore.DbContext.SaveChanges
        //     is called.
        //     A recursive search of the navigation properties will be performed to find reachable
        //     entities that are not already being tracked by the context. All entities found
        //     will be tracked by the context.
        //     For entity types with generated keys if an entity has its primary key value set
        //     then it will be tracked in the Microsoft.EntityFrameworkCore.EntityState.Modified
        //     state. If the primary key value is not set then it will be tracked in the Microsoft.EntityFrameworkCore.EntityState.Added
        //     state. This helps ensure new entities will be inserted, while existing entities
        //     will be updated. An entity is considered to have its primary key value set if
        //     the primary key property is set to anything other than the CLR default for the
        //     property type.
        //     For entity types without generated keys, the state set is always Microsoft.EntityFrameworkCore.EntityState.Modified.
        //     Use Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.State to set the
        //     state of only a single entity.
        //
        // Parameter:
        //   entities:
        //     The entities to update.
        void UpdateRange(params object[] entities);
        //
        // Zusammenfassung:
        //     Begins tracking the given entities and entries reachable from the given entities
        //     using the Microsoft.EntityFrameworkCore.EntityState.Modified state by default,
        //     but see below for cases when a different state will be used.
        //     Generally, no database interaction will be performed until Microsoft.EntityFrameworkCore.DbContext.SaveChanges
        //     is called.
        //     A recursive search of the navigation properties will be performed to find reachable
        //     entities that are not already being tracked by the context. All entities found
        //     will be tracked by the context.
        //     For entity types with generated keys if an entity has its primary key value set
        //     then it will be tracked in the Microsoft.EntityFrameworkCore.EntityState.Modified
        //     state. If the primary key value is not set then it will be tracked in the Microsoft.EntityFrameworkCore.EntityState.Added
        //     state. This helps ensure new entities will be inserted, while existing entities
        //     will be updated. An entity is considered to have its primary key value set if
        //     the primary key property is set to anything other than the CLR default for the
        //     property type.
        //     For entity types without generated keys, the state set is always Microsoft.EntityFrameworkCore.EntityState.Modified.
        //     Use Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.State to set the
        //     state of only a single entity.
        //
        // Parameter:
        //   entities:
        //     The entities to update.
        void UpdateRange(IEnumerable entities);

        /// <summary>
        /// Creates a new instance of the given entity-type
        /// </summary>
        /// <returns>the created raw-type</returns>
        public object New();
    }
}
