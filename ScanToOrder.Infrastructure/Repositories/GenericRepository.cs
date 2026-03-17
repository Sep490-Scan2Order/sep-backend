using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;
using System.Linq.Expressions;
using ScanToOrder.Domain.Entities;


namespace ScanToOrder.Infrastructure.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly AppDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public GenericRepository(AppDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public async Task<List<T>> GetAllAsync(Expression<Func<T, bool>>? predicate = null, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _dbSet.AsNoTracking();

            if (predicate != null)
                query = query.Where(predicate);

            foreach (var include in includes)
                query = query.Include(include);

            return await query.ToListAsync();
        }
        
        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }
        
        public async Task<T?> GetByIdAsync(object id)
        {
            return await _dbSet.FindAsync(id);
        }
        
        public async Task<T?> GetByFieldsIncludeAsync(
            Expression<Func<T, bool>> predicate, 
            params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _dbSet;

            if (includes != null)
            {
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            }

            return await query.FirstOrDefaultAsync(predicate);
        }
        
        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public void Update(T entity)
        {
            _dbSet.Update(entity);
        }

        public void Delete(T entity)
        {
            _dbSet.Remove(entity);
        }

        public IQueryable<T> GetQueryable()
        {
            return _dbSet.AsQueryable();
        }

        public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }

        public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, string includeProperties = "")
        {
            IQueryable<T> query = _dbSet;

            if (!string.IsNullOrEmpty(includeProperties))
            {
                var properties = includeProperties.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var property in properties)
                {
                    query = query.Include(property.Trim());
                }
            }

            return await query.FirstOrDefaultAsync(predicate);
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }

        public void RemoveRange(IEnumerable<T> entities)
        {
            _dbSet.RemoveRange(entities);
        }

        public void UpdateRange(IEnumerable<T> entities)
        {
            _dbSet.UpdateRange(entities);
        }

        public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.CountAsync(predicate);
        }
        
        public async Task<PagedResult<T>> GetPagedAndSortedAsync(
            int pageNumber, 
            int pageSize, 
            Expression<Func<T, bool>>? predicate = null, 
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, 
            params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _dbSet.AsNoTracking();

            if (predicate != null) query = query.Where(predicate);

            foreach (var include in includes) query = query.Include(include);

            int totalCount = await query.CountAsync();
            
            if (orderBy != null)
            {
                query = orderBy(query);
            }

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<T> { Items = items, TotalCount = totalCount, PageNumber = pageNumber, PageSize = pageSize };
        }
        public async Task<List<TResult>> QueryAsync<TResult>(
            Func<IQueryable<T>, IQueryable<TResult>> queryBuilder)
        {
            IQueryable<T> query = _dbSet.AsNoTracking();

            return await queryBuilder(query).ToListAsync();
        }

        public async Task<decimal> SumAsync(
    Expression<Func<T, bool>> predicate,
    Expression<Func<T, decimal>> selector)
        {
            return await _dbSet
                .Where(predicate)
                .SumAsync(selector);
        }
    }
}
