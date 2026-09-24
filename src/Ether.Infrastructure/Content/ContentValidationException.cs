namespace Ether.Infrastructure.Content;

/// <summary>Raised when deployed gameplay content is missing or violates its contract.</summary>
public sealed class ContentValidationException : Exception
{
    public ContentValidationException(string message)
        : base(message)
    {
    }

    public ContentValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
