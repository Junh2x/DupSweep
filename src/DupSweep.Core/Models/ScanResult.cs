namespace DupSweep.Core.Models;

public class ScanResult
{
    public Guid ScanId { get; set; } = Guid.NewGuid();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;

    public ScanConfig Config { get; set; } = new();
    public List<DuplicateGroup> DuplicateGroups { get; set; } = new();

    public int TotalFilesScanned { get; set; }
    public int TotalDuplicates => DuplicateGroups.Sum(g => g.FileCount - 1);
    public long TotalPotentialSavings => DuplicateGroups.Sum(g => g.PotentialSavings);

    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }

    public IEnumerable<DuplicateGroup> GetExactMatches()
        => DuplicateGroups.Where(g => g.Type == DuplicateType.ExactMatch);

    public IEnumerable<DuplicateGroup> GetSimilarImages()
        => DuplicateGroups.Where(g => g.Type == DuplicateType.SimilarImage);

    public IEnumerable<DuplicateGroup> GetSimilarVideos()
        => DuplicateGroups.Where(g => g.Type == DuplicateType.SimilarVideo);

    public IEnumerable<DuplicateGroup> GetSimilarAudio()
        => DuplicateGroups.Where(g => g.Type == DuplicateType.SimilarAudio);
}
