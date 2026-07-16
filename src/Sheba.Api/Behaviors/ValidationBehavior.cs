using FluentValidation;
using MediatR;
using ValidationException = Sheba.Shared.Kernel.Exceptions.ValidationException;

namespace Sheba.Api.Behaviors;

/// <summary>
/// MediatR pipeline behavior — runs all FluentValidation validators for the request.
/// Aggregates all failures into a single ValidationException with a field-error dictionary.
/// Runs after LoggingBehavior but before TransactionBehavior.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        var errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());

        throw new ValidationException(errors);
    }
}
