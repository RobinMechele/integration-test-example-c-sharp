using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Domain.Customers;

namespace Ordering.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public const string UniqueEmailIndexName = "UX_CUSTOMERS_EMAIL";

    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).HasMaxLength(Customer.NameMaxLength).IsRequired();

        builder.Property(c => c.Email)
            .HasConversion(email => email.Value, value => Email.Create(value))
            .HasMaxLength(Email.MaxLength)
            .IsRequired();

        // The real guarantee for "one customer per e-mail address", also under concurrent requests.
        // The name is referenced by UnitOfWork to translate ORA-00001 into a meaningful error.
        builder.HasIndex(c => c.Email).IsUnique().HasDatabaseName(UniqueEmailIndexName);

        builder.Property(c => c.RegisteredAtUtc).IsRequired();

        builder.Ignore(c => c.DomainEvents);
    }
}
