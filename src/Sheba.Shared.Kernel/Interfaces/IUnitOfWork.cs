namespace Sheba.Shared.Kernel.Interfaces;

/// <summary>
/// Unit of Work contract — wraps a single DbContext transaction.
/// Each module has its own UoW implementation backed by its own DbContext.
/// Cross-module transactions are NOT supported by design (use domain events instead).
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>Persists all pending changes and dispatches domain events.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Begins an explicit database transaction (use for complex multi-step commands).</summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Commits the active transaction.</summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Rolls back the active transaction.</summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
