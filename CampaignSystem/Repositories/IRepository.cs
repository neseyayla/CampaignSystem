using System.Linq.Expressions;

namespace CampaignSystem.Repositories;

/// <summary>
/// Basic data access shared by every entity. One implementation serves all of them.
///
/// Deliberately narrow: it covers the operations that look the same for every table.
/// Anything that needs grouping, aggregation or several joins — the reward calculation
/// above all — belongs in a service that works against <see cref="Data.CampaignDbContext"/>
/// directly, not behind this interface.
/// </summary>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Finds an entity by its primary key. Takes the key values as an array so that the
    /// same signature works for an int key, a long key and the two-column keys of the
    /// campaign criteria tables.
    /// The returned entity is tracked, so it can be modified and saved.
    /// </summary>
    Task<T?> GetByIdAsync(params object[] keys);

    /// <summary>Returns every row. Read only — the results are not tracked.</summary>
    Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the rows matching the condition. Read only — not tracked.</summary>
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>Whether any row matches the condition, without loading it.</summary>
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    void Update(T entity);

    /// <summary>
    /// Physically deletes the row. Tables carrying an IsActive flag are soft deleted
    /// instead — that is a decision for the service, not for this class.
    /// </summary>
    void Remove(T entity);

    /// <summary>
    /// Writes the pending changes. Kept separate from the methods above so that a service
    /// can make several changes and commit them in a single round trip.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
