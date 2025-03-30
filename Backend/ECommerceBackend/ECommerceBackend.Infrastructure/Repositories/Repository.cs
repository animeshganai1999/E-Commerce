using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly AppDbContext _dbContext;
        private readonly DbSet<T> _dbSet;

        public Repository(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _dbSet = _dbContext.Set<T>() ?? throw new InvalidOperationException("DbSet could not be initialized.");
        }

        public async Task<T> GetByIdAsync(int id)
        {
            var entity = await _dbSet.FindAsync(id);
            return entity ?? throw new KeyNotFoundException($"Entity with id {id} not found.");
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public async Task UpdateAsync(T entity)
        {
            _dbSet.Update(entity);
            await _dbContext.SaveChangesAsync();
        }

        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteAsync(Expression<Func<T, bool>> filter)
        {
            var entities = await _dbSet.Where(filter).ToListAsync();
            if (entities == null || entities.Count == 0)
            {
                throw new KeyNotFoundException("No entities found matching the specified filter.");
            }

            _dbSet.RemoveRange(entities);
            await _dbContext.SaveChangesAsync();
        }
    }
}
