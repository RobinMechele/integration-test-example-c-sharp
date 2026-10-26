using FluentValidation;
using Mediator;
using Ordering.Application.Behaviors;

namespace Ordering.Application.UnitTests.Behaviors;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task A_valid_message_is_passed_on_to_the_handler()
    {
        var behavior = new ValidationBehavior<Ping, string>([new PingTextValidator()]);
        var handler = new HandlerStub("pong");

        var response = await behavior.Handle(new Ping("hello", 1), handler.Handle, TestContext.Current.CancellationToken);

        response.ShouldBe("pong");
        handler.ReceivedMessages.ShouldHaveSingleItem().ShouldBe(new Ping("hello", 1));
    }

    [Fact]
    public async Task An_invalid_message_never_reaches_the_handler()
    {
        var behavior = new ValidationBehavior<Ping, string>([new PingTextValidator()]);
        var handler = new HandlerStub("pong");

        await Should.ThrowAsync<ValidationException>(async () =>
            await behavior.Handle(new Ping("", 1), handler.Handle, TestContext.Current.CancellationToken));

        handler.ReceivedMessages.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_failures_of_all_validators_are_reported_together()
    {
        var behavior = new ValidationBehavior<Ping, string>([new PingTextValidator(), new PingNumberValidator()]);

        var exception = await Should.ThrowAsync<ValidationException>(async () =>
            await behavior.Handle(new Ping("", -1), new HandlerStub("pong").Handle, TestContext.Current.CancellationToken));

        exception.Errors.Select(e => e.PropertyName).ShouldBe(["Text", "Number"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_message_without_validators_is_passed_on_to_the_handler()
    {
        var behavior = new ValidationBehavior<Ping, string>([]);

        var response = await behavior.Handle(new Ping("", -1), new HandlerStub("pong").Handle, TestContext.Current.CancellationToken);

        response.ShouldBe("pong");
    }

    public sealed record Ping(string Text, int Number) : IRequest<string>;

    private sealed class PingTextValidator : AbstractValidator<Ping>
    {
        public PingTextValidator() => RuleFor(p => p.Text).NotEmpty();
    }

    private sealed class PingNumberValidator : AbstractValidator<Ping>
    {
        public PingNumberValidator() => RuleFor(p => p.Number).GreaterThan(0);
    }

    /// <summary>Stands in for the rest of the pipeline (the "next" delegate).</summary>
    private sealed class HandlerStub(string response)
    {
        public List<Ping> ReceivedMessages { get; } = [];

        public ValueTask<string> Handle(Ping message, CancellationToken cancellationToken)
        {
            ReceivedMessages.Add(message);
            return ValueTask.FromResult(response);
        }
    }
}
