using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ordering.Application.Abstractions;
using Ordering.Domain.Customers;
using Ordering.Domain.Orders;
using Ordering.Infrastructure.Messaging;
using Ordering.Infrastructure.Outbox;
using Ordering.Infrastructure.Persistence;
using Ordering.Infrastructure.Persistence.Repositories;

namespace Ordering.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // TryAdd: hosts and tests can register their own TimeProvider / IMessagePublisher first.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IMessagePublisher, LoggingMessagePublisher>();

        services.AddSingleton<InsertOutboxMessagesInterceptor>();
        services.AddDbContext<OrderingDbContext>((serviceProvider, options) =>
        {
            // Resolved lazily (per DbContext) so that configuration overrides made by WebApplicationFactory
            // in the tests are always picked up.
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>()
                .GetConnectionString(OracleConfiguration.ConnectionStringName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{OracleConfiguration.ConnectionStringName}' is not configured.");
            }

            options
                .UseOrderingOracle(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<InsertOutboxMessagesInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        services.AddOptions<OutboxOptions>()
            .BindConfiguration(OutboxOptions.SectionName)
            .Validate(o => o.BatchSize > 0 && o.MaxAttempts > 0 && o.PollingInterval > TimeSpan.Zero, "Invalid outbox options.")
            .ValidateOnStart();
        services.AddScoped<OutboxProcessor>();
        services.AddHostedService<OutboxBackgroundService>();

        return services;
    }

    /// <summary>
    /// Applies pending EF Core migrations. Convenient for local development; in production prefer
    /// running migrations as a separate deployment step (migration bundle or generated SQL script).
    /// </summary>
    public static async Task MigrateOrderingDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
