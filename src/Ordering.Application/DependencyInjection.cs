using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Ordering.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the application layer's services. The mediator itself is registered by the host
    /// (Ordering.Api) because the Mediator source generator runs in the outermost project.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, ServiceLifetime.Singleton);
        return services;
    }
}
