using DupSweep.Core.Models;

namespace DupSweep.Core.Algorithms;

/// <summary>
/// 중복 파일 탐지 알고리즘
/// 해시값과 지각 해시를 사용하여 정확한 일치 및 유사 파일 그룹 탐지
/// </summary>
public class DuplicateDetector
{
    /// <summary>
    /// 해시값이 완전히 일치하는 파일 그룹 탐지
    /// </summary>
    public List<DuplicateGroup> FindExactMatches(IEnumerable<FileEntry> files, ScanConfig config)
    {
        // 해시값으로 그룹화
        var groups = files
            .Where(f => !string.IsNullOrWhiteSpace(f.FullHash))
            .GroupBy(f => f.FullHash, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.ToList());

        // 생성일 일치 조건 적용
        if (config.MatchCreatedDate)
        {
            groups = groups.SelectMany(g => g.GroupBy(f => f.CreatedDate).Where(sg => sg.Count() > 1).Select(sg => sg.ToList()));
        }

        // 수정일 일치 조건 적용
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

    /// <summary>
    /// 지각 해시로 유사 이미지 그룹 탐지
    /// </summary>
    public List<DuplicateGroup> FindSimilarImages(IEnumerable<FileEntry> files, double thresholdPercent)
    {
        var candidates = files
            .Where(f => f.FileType == FileType.Image && f.PerceptualHash.HasValue)
            .ToList();

        return FindSimilarByHash(candidates, thresholdPercent, DuplicateType.SimilarImage);
    }

    /// <summary>
    /// 지각 해시로 유사 비디오 그룹 탐지
    /// </summary>
    public List<DuplicateGroup> FindSimilarVideos(IEnumerable<FileEntry> files, double thresholdPercent)
    {
        var candidates = files
            .Where(f => f.FileType == FileType.Video && f.PerceptualHash.HasValue)
            .ToList();

        return FindSimilarByHash(candidates, thresholdPercent, DuplicateType.SimilarVideo);
    }

    /// <summary>
    /// 오디오 핑거프린트로 유사 오디오 그룹 탐지
    /// </summary>
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

    /// <summary>
    /// 지각 해시 기반 유사 파일 그룹 탐지
    /// 해밍 거리를 사용하여 임계값 이상의 유사도를 가진 파일 그룹화
    /// </summary>
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

            // 다른 후보 파일들과 유사도 비교
            for (int j = i + 1; j < candidates.Count; j++)
            {
                var other = candidates[j];
                if (visited.Contains(other.FilePath))
                {
                    continue;
                }

                var similarity = PerceptualHash.CombinedSimilarityDetails(
                    baseFile.PerceptualHash!.Value, other.PerceptualHash!.Value,
                    baseFile.ColorHash, other.ColorHash);

                if (!IsCandidateCompatible(baseFile, other, type))
                {
                    continue;
                }

                var minStructure = Math.Max(70d, thresholdPercent - 5d);
                var minColor = Math.Max(55d, thresholdPercent - 15d);

                if (similarity.Combined >= thresholdPercent &&
                    similarity.Structure >= minStructure &&
                    similarity.Color >= minColor)
                {
                    groupFiles.Add(other);
                    totalSimilarity += similarity.Combined;
                    comparisons++;
                    visited.Add(other.FilePath);
                }
            }

            // 2개 이상의 파일이 있으면 그룹 생성
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

    private static bool IsCandidateCompatible(FileEntry baseFile, FileEntry other, DuplicateType type)
    {
        if (type != DuplicateType.SimilarImage)
        {
            return true;
        }

        if (baseFile.Size > 0 && other.Size > 0)
        {
            var sizeRatio = GetRatio(baseFile.Size, other.Size);
            if (sizeRatio < 0.45)
            {
                return false;
            }
        }

        if (baseFile.Width > 0 && baseFile.Height > 0 && other.Width > 0 && other.Height > 0)
        {
            var areaA = (long)baseFile.Width * baseFile.Height;
            var areaB = (long)other.Width * other.Height;
            var areaRatio = GetRatio(areaA, areaB);
            if (areaRatio < 0.55)
            {
                return false;
            }

            var aspectA = (double)baseFile.Width / baseFile.Height;
            var aspectB = (double)other.Width / other.Height;
            var aspectRatio = GetRatio(aspectA, aspectB);
            if (aspectRatio < 0.7)
            {
                return false;
            }
        }

        return true;
    }

    private static double GetRatio(long a, long b)
    {
        var min = Math.Min(a, b);
        var max = Math.Max(a, b);
        return max == 0 ? 0 : (double)min / max;
    }

    private static double GetRatio(double a, double b)
    {
        var min = Math.Min(a, b);
        var max = Math.Max(a, b);
        return max <= 0 ? 0 : min / max;
    }
}
