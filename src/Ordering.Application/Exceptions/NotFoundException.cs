namespace Ordering.Application.Exceptions;

public sealed class NotFoundException(string resource, Guid id)
    : Exception($"{resource} '{id}' was not found.")
{
    public string Resource { get; } = resource;

    public Guid Id { get; } = id;
}
