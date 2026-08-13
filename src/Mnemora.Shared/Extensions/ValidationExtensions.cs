using FluentValidation.Results;

namespace Mnemora.Shared.Extensions;

public static class ValidationExtensions
{
    public static Errors ToErrors(this ValidationResult validationResult) =>
        validationResult
            .Errors
            .Select(e => CommonErrors.Validation(
                e.ErrorCode,
                e.ErrorMessage,
                e.PropertyName))
            .ToArray();

    public static Errors ToErrors(this IEnumerable<ValidationResult> validationResults) =>
        validationResults
            .SelectMany(e => e.Errors)
            .Select(e => CommonErrors.Validation(
                e.ErrorCode,
                e.ErrorMessage,
                e.PropertyName))
            .ToArray();
}