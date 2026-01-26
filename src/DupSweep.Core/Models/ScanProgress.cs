namespace DupSweep.Core.Models;

public class ScanProgress
{
    public ScanPhase Phase { get; set; }
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public int DuplicateGroupsFound { get; set; }
    public long PotentialSavings { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public bool IsPaused { get; set; }
    public bool IsCancelled { get; set; }

    public double ProgressPercentage => TotalFiles > 0
        ? (double)ProcessedFiles / TotalFiles * 100
        : 0;

    public string StatusMessage => Phase switch
    {
        ScanPhase.Initializing => "Initializing...",
        ScanPhase.Scanning => $"Scanning files... ({ProcessedFiles}/{TotalFiles})",
        ScanPhase.Hashing => $"Computing hashes... ({ProcessedFiles}/{TotalFiles})",
        ScanPhase.Comparing => "Comparing files...",
        ScanPhase.GeneratingThumbnails => "Generating thumbnails...",
        ScanPhase.Completed => "Scan completed",
        ScanPhase.Cancelled => "Scan cancelled",
        ScanPhase.Error => "Error occurred",
        _ => "Unknown"
    };
}

public enum ScanPhase
{
    Initializing,
    Scanning,
    Hashing,
    Comparing,
    GeneratingThumbnails,
    Completed,
    Cancelled,
    Error
}
