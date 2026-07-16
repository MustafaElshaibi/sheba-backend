using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Api.Behaviors;

/// <summary>
/// Marker interface — apply this to any Command that requires a database transaction.
/// Only commands that implement ITransactionalCommand will be wrapped in a transaction.
/// Queries are never wrapped.
/// </summary>
public interface ITransactionalCommand { }

/// <summary>
/// MediatR pipeline behavior — wraps commands that implement ITransactionalCommand
/// in an explicit UnitOfWork transaction. Commits on success, rolls back on exception.
///
/// Architecture note: Each module's UoW is scoped to its own DbContext.
/// Cross-module consistency is achieved via domain events, not distributed transactions.
///
/// The IUnitOfWork is resolved lazily (only when the request is an ITransactionalCommand)
/// so that modules whose handlers manage their own persistence (via a repository
/// SaveChangesAsync) do not need to register an IUnitOfWork just to satisfy the pipeline.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(
    IServiceProvider serviceProvider,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only wrap transactional commands — queries and self-persisting commands pass through
        if (request is not ITransactionalCommand)
            return await next();

        var unitOfWork = serviceProvider.GetService<IUnitOfWork>();
        if (unitOfWork is null)
        {
            logger.LogWarning(
                "[TX] {RequestName} is ITransactionalCommand but no IUnitOfWork is registered — running without an explicit transaction.",
                typeof(TRequest).Name);
            return await next();
        }

        var requestName = typeof(TRequest).Name;
        logger.LogDebug("[TX] Beginning transaction for {RequestName}", requestName);

        try
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            var response = await next();
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            logger.LogDebug("[TX] Committed transaction for {RequestName}", requestName);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TX] Rolling back transaction for {RequestName}", requestName);
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
