using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace GSTReraReconciliation.Data.Repositories
{
    /// <summary>
    /// Generic repository implementation using Entity Framework 6.
    /// All database access goes through EF parameterized queries — no raw SQL concatenation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly ReconciliationDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public GenericRepository(ReconciliationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = context.Set<T>();
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public async Task<T> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        public IQueryable<T> Find(Expression<Func<T, bool>> predicate)
        {
            return _dbSet.Where(predicate);
        }

        public void Add(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _dbSet.Add(entity);
        }

        public void AddRange(IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            _dbSet.AddRange(entities);
        }

        public void Update(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
        }

        public void Remove(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _dbSet.Remove(entity);
        }

        public void RemoveRange(IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            _dbSet.RemoveRange(entities);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}
