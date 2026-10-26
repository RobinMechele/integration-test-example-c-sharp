using System.Collections.Concurrent;
using Ordering.Infrastructure.Messaging;

namespace Ordering.TestKit;

/// <summary>
/// Hand-written test double for the message broker (an unmanaged dependency). A spy records what was
/// published so tests can assert on it, and can simulate broker failures and latency.
/// Hand-written spies are easier to read and reuse than mocking-library setups (Khorikov, chapter 9).
/// </summary>
public sealed class MessagePublisherSpy : IMessagePublisher
{
    private readonly ConcurrentQueue<OutgoingMessage> _published = new();
    private volatile Func<OutgoingMessage, bool> _shouldFail = _ => false;
    private TimeSpan _latency = TimeSpan.Zero;

    public IReadOnlyList<OutgoingMessage> Published => [.. _published];

    public async Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        if (_latency > TimeSpan.Zero)
        {
            await Task.Delay(_latency, cancellationToken);
        }

        if (_shouldFail(message))
        {
            throw new InvalidOperationException($"Simulated broker failure for {message.MessageType}.");
        }

        _published.Enqueue(message);
    }

    public void FailWhen(Func<OutgoingMessage, bool> predicate) => _shouldFail = predicate;

    public void SimulateLatency(TimeSpan latency) => _latency = latency;

    /// <summary>Waits (polling) until a matching message was published; for tests of background processing.</summary>
    public async Task<OutgoingMessage> WaitForMessageAsync(Func<OutgoingMessage, bool> predicate, TimeSpan timeout, Action? onEachPoll = null)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!cancellation.IsCancellationRequested)
        {
            var match = _published.FirstOrDefault(predicate);
            if (match is not null)
            {
                return match;
            }

            onEachPoll?.Invoke();
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        throw new TimeoutException($"No matching message was published within {timeout}.");
    }

    public void Reset()
    {
        _published.Clear();
        _shouldFail = _ => false;
        _latency = TimeSpan.Zero;
    }
}
