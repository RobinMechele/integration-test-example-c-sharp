namespace Ordering.Application.Exceptions;

/// <summary>
/// The request conflicts with the current state of the data, for example a duplicate e-mail address
/// or a concurrent update of the same order. Mapped to HTTP 409 by the API.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
