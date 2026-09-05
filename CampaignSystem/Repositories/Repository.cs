using System.Linq.Expressions;
using CampaignSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace CampaignSystem.Repositories;

/// <summary>
/// The single implementation of <see cref="IRepository{T}"/>. Registered once as an open
/// generic, so asking for IRepository&lt;Segment&gt; or IRepository&lt;Campaign&gt; resolves
/// to this class with T filled in.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    private readonly CampaignDbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(CampaignDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(params object[] keys)
        => await _dbSet.FindAsync(keys);

    public async Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<List<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);

    public async Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        => await _dbSet.AnyAsync(predicate, cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        => await _dbSet.AddAsync(entity, cancellationToken);

    public void Update(T entity) => _dbSet.Update(entity);

    public void Remove(T entity) => _dbSet.Remove(entity);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
