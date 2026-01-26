namespace DupSweep.Core.Models;

public class DuplicateGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public List<FileEntry> Files { get; set; } = new();
    public DuplicateType Type { get; set; }
    public double Similarity { get; set; } = 100;

    public int FileCount => Files.Count;
    public long TotalSize => Files.Sum(f => f.Size);
    public long PotentialSavings => Files.Skip(1).Sum(f => f.Size);

    public FileEntry? GetOldest() => Files.OrderBy(f => f.CreatedDate).FirstOrDefault();
    public FileEntry? GetNewest() => Files.OrderByDescending(f => f.ModifiedDate).FirstOrDefault();
    public FileEntry? GetSmallest() => Files.OrderBy(f => f.Size).FirstOrDefault();
    public FileEntry? GetLargest() => Files.OrderByDescending(f => f.Size).FirstOrDefault();
}

public enum DuplicateType
{
    ExactMatch,      // Same hash
    SimilarImage,    // Similar perceptual hash
    SimilarVideo,    // Similar video keyframes
    SimilarAudio     // Similar audio fingerprint
}
