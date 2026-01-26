using DupSweep.Core.Models;

namespace DupSweep.Core.Algorithms;

public class DuplicateDetector
{
    public List<DuplicateGroup> FindExactMatches(IEnumerable<FileEntry> files, ScanConfig config)
    {
        var groups = files
            .Where(f => !string.IsNullOrWhiteSpace(f.FullHash))
            .GroupBy(f => f.FullHash, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.ToList());

        if (config.MatchCreatedDate)
        {
            groups = groups.SelectMany(g => g.GroupBy(f => f.CreatedDate).Where(sg => sg.Count() > 1).Select(sg => sg.ToList()));
        }

        if (config.MatchModifiedDate)
        {
            groups = groups.SelectMany(g => g.GroupBy(f => f.ModifiedDate).Where(sg => sg.Count() > 1).Select(sg => sg.ToList()));
        }

        return groups
            .Select(group => new DuplicateGroup
            {
                Type = DuplicateType.ExactMatch,
                Similarity = 100,
                Files = group
            })
            .ToList();
    }

    public List<DuplicateGroup> FindSimilarImages(IEnumerable<FileEntry> files, double thresholdPercent)
    {
        var candidates = files
            .Where(f => f.FileType == FileType.Image && f.PerceptualHash.HasValue)
            .ToList();

        return FindSimilarByHash(candidates, thresholdPercent, DuplicateType.SimilarImage);
    }

    public List<DuplicateGroup> FindSimilarVideos(IEnumerable<FileEntry> files, double thresholdPercent)
    {
        var candidates = files
            .Where(f => f.FileType == FileType.Video && f.PerceptualHash.HasValue)
            .ToList();

        return FindSimilarByHash(candidates, thresholdPercent, DuplicateType.SimilarVideo);
    }

    public List<DuplicateGroup> FindSimilarAudio(IEnumerable<FileEntry> files, double thresholdPercent)
    {
        if (thresholdPercent > 100)
        {
            return new List<DuplicateGroup>();
        }

        var groups = files
            .Where(f => f.FileType == FileType.Audio && f.AudioFingerprint.HasValue)
            .GroupBy(f => f.AudioFingerprint!.Value)
            .Where(g => g.Count() > 1)
            .Select(group => new DuplicateGroup
            {
                Type = DuplicateType.SimilarAudio,
                Similarity = 100,
                Files = group.ToList()
            })
            .ToList();

        return groups;
    }

    private static List<DuplicateGroup> FindSimilarByHash(List<FileEntry> candidates, double thresholdPercent, DuplicateType type)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var groups = new List<DuplicateGroup>();

        for (int i = 0; i < candidates.Count; i++)
        {
            var baseFile = candidates[i];
            if (visited.Contains(baseFile.FilePath))
            {
                continue;
            }

            var groupFiles = new List<FileEntry> { baseFile };
            double totalSimilarity = 100;
            int comparisons = 1;

            for (int j = i + 1; j < candidates.Count; j++)
            {
                var other = candidates[j];
                if (visited.Contains(other.FilePath))
                {
                    continue;
                }

                var similarity = PerceptualHash.SimilarityPercent(baseFile.PerceptualHash!.Value, other.PerceptualHash!.Value);
                if (similarity >= thresholdPercent)
                {
                    groupFiles.Add(other);
                    totalSimilarity += similarity;
                    comparisons++;
                    visited.Add(other.FilePath);
                }
            }

            if (groupFiles.Count > 1)
            {
                visited.Add(baseFile.FilePath);
                groups.Add(new DuplicateGroup
                {
                    Type = type,
                    Similarity = totalSimilarity / comparisons,
                    Files = groupFiles
                });
            }
        }

        return groups;
    }
}
