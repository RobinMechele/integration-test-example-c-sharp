using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ordering.Infrastructure.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        // Table and column names are used by the hand-written SQL in OutboxProcessor - keep them in sync.
        builder.ToTable("OUTBOX_MESSAGES");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.MessageType).HasMaxLength(OutboxMessage.MessageTypeMaxLength).IsRequired();
        // Without an explicit type the Oracle provider maps an unbounded string to NVARCHAR2(2000) and large
        // payloads fail with ORA-12899 (caught by OutboxWriteTests.Large_payloads_are_stored_without_being_truncated).
        builder.Property(m => m.Payload).HasColumnType("NCLOB").IsRequired();
        builder.Property(m => m.OccurredOnUtc).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(OutboxMessage.LastErrorMaxLength);

        // Supports "WHERE PROCESSED_ON_UTC IS NULL ORDER BY OCCURRED_ON_UTC" (Oracle indexes NULLs in
        // composite indexes as long as one column is not null).
        builder.HasIndex(m => new { m.ProcessedOnUtc, m.OccurredOnUtc });
    }
}
