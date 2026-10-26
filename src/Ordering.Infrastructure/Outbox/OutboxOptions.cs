namespace Ordering.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Runs the background publisher. Tests switch it off and call <see cref="OutboxProcessor"/> directly.</summary>
    public bool Enabled { get; set; } = true;

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>How many messages one processor claims (and locks) per transaction.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>After this many attempts a message is no longer retried and needs manual attention.</summary>
    public int MaxAttempts { get; set; } = 5;
}
