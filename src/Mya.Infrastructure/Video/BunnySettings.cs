namespace Mya.Infrastructure.Streaming;
public sealed class BunnySettings
{
    public bool Enabled { get; set; }
    public long LibraryId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string ReadOnlyApiKey { get; set; } = string.Empty;
    public string TokenKey { get; set; } = string.Empty;
    public string CdnHost { get; set; } = string.Empty;
}
