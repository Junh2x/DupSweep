namespace DupSweep.Core.Models;

public class ScanConfig
{
    public List<string> Directories { get; set; } = new();

    // Detection methods
    public bool UseHashComparison { get; set; } = true;
    public bool UseSizeComparison { get; set; } = true;
    public bool UseResolutionComparison { get; set; } = false;
    public bool UseImageSimilarity { get; set; } = true;
    public bool UseVideoSimilarity { get; set; } = true;
    public bool MatchCreatedDate { get; set; } = false;
    public bool MatchModifiedDate { get; set; } = false;

    // File types to scan
    public bool ScanImages { get; set; } = true;
    public bool ScanVideos { get; set; } = true;

    // Thresholds
    public double ImageSimilarityThreshold { get; set; } = 85;
    public double VideoSimilarityThreshold { get; set; } = 85;

    public int ThumbnailSize { get; set; } = 128;
    public string? FfmpegPath { get; set; }
    public string? FfprobePath { get; set; }

    // Performance
    public int ParallelThreads { get; set; } = Environment.ProcessorCount;
    public long MinFileSize { get; set; } = 0;
    public long MaxFileSize { get; set; } = long.MaxValue;

    // Options
    public bool FollowSymlinks { get; set; } = false;
    public bool IncludeHiddenFiles { get; set; } = false;
    public bool RecursiveScan { get; set; } = true;

    public IEnumerable<string> GetSupportedExtensions()
    {
        var extensions = new List<string>();

        if (ScanImages)
        {
            extensions.AddRange(new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".ico", ".heic", ".heif" });
        }

        if (ScanVideos)
        {
            extensions.AddRange(new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".3gp" });
        }

        return extensions;
    }
}
