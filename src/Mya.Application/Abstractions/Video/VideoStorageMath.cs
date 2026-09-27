namespace Mya.Application.Abstractions.Media;

/// <summary>
/// Part arithmetic shared by the handler, the adapter and the browser. One definition, so a
/// resumed upload cannot disagree with the provider about which part number holds which bytes.
/// </summary>
public static class VideoStorageMath
{
    /// <summary>How many parts a file of this size takes, at least one even for an empty file.</summary>
    public static int PartCount(long sizeBytes, long partSizeBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partSizeBytes);
        return (int)Math.Max(1, (sizeBytes + partSizeBytes - 1) / partSizeBytes);
    }
}
