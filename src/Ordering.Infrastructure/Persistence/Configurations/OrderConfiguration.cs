using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Domain.Customers;
using Ordering.Domain.Orders;

namespace Ordering.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.HasOne<Customer>().WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => new { o.CustomerId, o.PlacedAtUtc });

        // Stored as text ('Placed', 'Cancelled') so the data is readable without the code.
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.Total).HasPrecision(18, 2);
        builder.Property(o => o.PlacedAtUtc).IsRequired();
        builder.Property(o => o.CancellationReason).HasMaxLength(Order.CancellationReasonMaxLength);

        // Optimistic concurrency: UPDATE ... WHERE ID = :id AND VERSION = :originalVersion
        builder.Property(o => o.Version).IsConcurrencyToken();

        builder.OwnsMany(o => o.Lines, lines =>
        {
            lines.ToTable("ORDER_LINES");
            lines.WithOwner().HasForeignKey("OrderId");
            lines.Property<int>("Id").ValueGeneratedOnAdd();
            lines.HasKey("Id");
            lines.Property(l => l.Sku).HasMaxLength(OrderLine.SkuMaxLength).IsRequired();
            lines.Property(l => l.UnitPrice).HasPrecision(18, 2);
            lines.Ignore(l => l.LineTotal);
        });
        builder.Navigation(o => o.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(o => o.DomainEvents);
    }
}
