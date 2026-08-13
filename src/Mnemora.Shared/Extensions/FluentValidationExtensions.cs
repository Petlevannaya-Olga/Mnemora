using CSharpFunctionalExtensions;
using FluentValidation;
using FluentValidation.Results;

namespace Mnemora.Shared.Extensions;

public static class FluentValidationExtensions
{
    public static IRuleBuilderOptionsConditions<T, TElement>
        MustBeValueObject<T, TElement, TValueObject>(
            this IRuleBuilder<T, TElement> ruleBuilder,
            Func<TElement, Result<TValueObject, Error>> factoryMethod)
    {
        return ruleBuilder.Custom((value, context) =>
        {
            var result = factoryMethod(value);

            if (result.IsSuccess)
            {
                return;
            }

            var propertyName = string.IsNullOrWhiteSpace(
                result.Error.InvalidField)
                ? context.PropertyPath
                : result.Error.InvalidField;

            context.AddFailure(
                new ValidationFailure(
                    propertyName,
                    result.Error.Message)
                {
                    ErrorCode = result.Error.Code
                });
        });
    }

    public static IRuleBuilderOptions<T, TProperty> WithError<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> ruleBuilder,
        Error error)
    {
        return ruleBuilder
            .WithMessage(error.Message)
            .WithErrorCode(error.Code);
    }

    public static IRuleBuilderOptions<T, IEnumerable<TElement>>
        MustBeUnique<T, TElement>(
            this IRuleBuilder<T, IEnumerable<TElement>> ruleBuilder)
    {
        return ruleBuilder
            .Must(collection =>
            {
                if (collection is null)
                {
                    return true;
                }

                var uniqueItems = new HashSet<TElement>();

                return collection.All(uniqueItems.Add);
            })
            .WithError(CommonErrors.CollectionContainsDuplicates());
    }
}