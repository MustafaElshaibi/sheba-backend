using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Shared.Kernel.Persistence;

/// <summary>
/// Generic <see cref="IUnitOfWork"/> over a module's own DbContext (T-EVT-1). Registered once
/// per module — <c>services.AddScoped&lt;IUnitOfWork, EfUnitOfWork&lt;XDbContext&gt;&gt;()</c> —
/// so <see cref="Sheba.Api.Behaviors.TransactionBehavior{TRequest,TResponse}"/> resolves a real
/// implementation instead of silently running without a transaction. The repository and this
/// unit of work share the same scoped DbContext instance, so BeginTransactionAsync here wraps
/// whatever SaveChangesAsync the repository calls inside the handler.
/// </summary>
public sealed class EfUnitOfWork<TContext>(TContext context) : IUnitOfWork
    where TContext : DbContext
{
    private IDbContextTransaction? _transaction;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        => _transaction = await context.Database.BeginTransactionAsync(cancellationToken);

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            return;

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            return;

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public void Dispose() => _transaction?.Dispose();
}
