using Mediator;
using Ordering.Api.Endpoints;
using Ordering.Api.ErrorHandling;
using Ordering.Application;
using Ordering.Application.Behaviors;
using Ordering.Infrastructure;
using Ordering.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddMediator(options =>
{
    // Scoped, because handlers depend on the scoped DbContext (one per HTTP request).
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.Assemblies = [typeof(Ordering.Application.DependencyInjection)];
    options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

builder.Services.AddHealthChecks().AddDbContextCheck<OrderingDbContext>("oracle");

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateOrderingDatabaseAsync();
}

app.MapHealthChecks("/health");
app.MapCustomerEndpoints();
app.MapOrderEndpoints();

await app.RunAsync();
