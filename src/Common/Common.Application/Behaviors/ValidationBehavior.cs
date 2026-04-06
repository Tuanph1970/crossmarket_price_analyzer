using Common.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Common.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that validates requests using FluentValidation before execution.
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
        {
            var errors = failures.Select(f => f.ErrorMessage).Distinct().ToArray();
            throw new Common.Domain.Exceptions.ApplicationException(
                $"Validation failed for {typeof(TRequest).Name}. {errors.Length} error(s).",
                errors);
        }

        return await next();
    }
}
