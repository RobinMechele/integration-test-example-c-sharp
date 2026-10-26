using Microsoft.EntityFrameworkCore;

namespace Ordering.Infrastructure.IntegrationTests.Persistence;

public sealed class MigrationTests(InfrastructureFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task All_migrations_apply_cleanly_to_a_real_Oracle_database()
    {
        await using var dbContext = Fixture.Database.CreateDbContext();

        (await dbContext.Database.GetPendingMigrationsAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.Database.GetAppliedMigrationsAsync(CancellationToken)).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task The_EF_Core_model_has_no_changes_that_are_missing_a_migration()
    {
        await using var dbContext = Fixture.Database.CreateDbContext();

        dbContext.Database.HasPendingModelChanges().ShouldBeFalse(
            "The model changed without a migration. Run: dotnet ef migrations add <Name> --project src/Ordering.Infrastructure --output-dir Persistence/Migrations");
    }
}
