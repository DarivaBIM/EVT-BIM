namespace DarivaBIM.Application.DTOs.Family
{
    /// <summary>
    /// Snapshot of an in-flight download. <see cref="TotalBytes"/> is nullable
    /// because some HTTP responses omit <c>Content-Length</c> — callers must be
    /// prepared to render an indeterminate progress UI in that case.
    /// </summary>
    public readonly struct DownloadProgress
    {
        public DownloadProgress(long bytesDownloaded, long? totalBytes)
        {
            BytesDownloaded = bytesDownloaded;
            TotalBytes = totalBytes;
        }

        public long BytesDownloaded { get; }

        public long? TotalBytes { get; }

        public double? Fraction =>
            TotalBytes is long total && total > 0
                ? (double)BytesDownloaded / total
                : null;
    }
}
