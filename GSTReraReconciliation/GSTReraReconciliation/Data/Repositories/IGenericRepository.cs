using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace GSTReraReconciliation.Data.Repositories
{
    /// <summary>
    /// Generic repository interface providing standard CRUD operations
    /// for all entity types.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    public interface IGenericRepository<T> where T : class
    {
        /// <summary>
        /// Retrieves all entities of type T.
        /// </summary>
        Task<IEnumerable<T>> GetAllAsync();

        /// <summary>
        /// Retrieves a single entity by its primary key.
        /// </summary>
        Task<T> GetByIdAsync(int id);

        /// <summary>
        /// Retrieves entities matching the given predicate.
        /// </summary>
        IQueryable<T> Find(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Adds a single entity to the context.
        /// </summary>
        void Add(T entity);

        /// <summary>
        /// Adds a collection of entities to the context.
        /// </summary>
        void AddRange(IEnumerable<T> entities);

        /// <summary>
        /// Marks an entity as modified in the context.
        /// </summary>
        void Update(T entity);

        /// <summary>
        /// Removes a single entity from the context.
        /// </summary>
        void Remove(T entity);

        /// <summary>
        /// Removes a collection of entities from the context.
        /// </summary>
        void RemoveRange(IEnumerable<T> entities);

        /// <summary>
        /// Persists all pending changes to the database.
        /// </summary>
        Task<int> SaveChangesAsync();
    }
}
