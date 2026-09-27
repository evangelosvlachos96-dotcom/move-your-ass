namespace Mya.Application.Abstractions.Media;

/// <summary>
/// One failure type for every storage provider fault, so handlers do not have to know which SDK
/// is underneath. Messages are for logs, never for the wire: handlers map to a Greek error code.
/// </summary>
public sealed class VideoStorageException : Exception
{
    public VideoStorageException()
    {
    }

    public VideoStorageException(string message)
        : base(message)
    {
    }

    public VideoStorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>True when the provider said the object or upload does not exist.</summary>
    public bool NotFound { get; init; }
}
