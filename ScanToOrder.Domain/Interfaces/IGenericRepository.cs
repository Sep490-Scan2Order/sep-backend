using System.Linq.Expressions;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IGenericRepository<T> where T : class
    {
        Task<List<T>> GetAllAsync(Expression<Func<T, bool>>? predicate = null, params Expression<Func<T, object>>[] includes);
        Task<T?> GetByIdAsync(object id);
        Task<T?> GetByFieldsIncludeAsync(
            Expression<Func<T, bool>> predicate,
            params Expression<Func<T, object>>[] includes);
        Task AddRangeAsync(IEnumerable<T> entities);
        Task AddAsync(T entity);
        void Update(T entity);
        void Delete(T entity);
        IQueryable<T> GetQueryable();
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
        Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, string includeProperties = "");
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        void RemoveRange(IEnumerable<T> entities);
        void UpdateRange(IEnumerable<T> entities);
    }
}
