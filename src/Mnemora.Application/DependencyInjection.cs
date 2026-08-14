using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Application.Decorators;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        var assembly = typeof(DependencyInjection).Assembly;

        services.Scan(scan => scan.FromAssemblies(assembly)
            .AddClasses(classes => classes
                .AssignableToAny(typeof(ICommandHandler<,>), typeof(ICommandHandler<>)))
            .AsSelfWithInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan.FromAssemblies(assembly)
            .AddClasses(classes => classes
                .AssignableToAny(typeof(IQueryHandler<,>)))
            .AsSelfWithInterfaces()
            .WithScopedLifetime());

        services.AddScoped(typeof(ValidationExecutor<>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(CommandValidationDecorator<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(QueryValidationDecorator<,>));

        return services;
    }
}