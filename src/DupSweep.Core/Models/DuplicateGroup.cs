namespace DupSweep.Core.Models;

/// <summary>
/// 중복 파일 그룹 모델 클래스
/// 동일하거나 유사한 파일들의 그룹과 관련 통계 정보 포함
/// </summary>
public class DuplicateGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public List<FileEntry> Files { get; set; } = new();
    public DuplicateType Type { get; set; }       // 중복 유형
    public double Similarity { get; set; } = 100; // 유사도 (0-100%)

    // 통계 속성
    public int FileCount => Files.Count;
    public long TotalSize => Files.Sum(f => f.Size);
    public long PotentialSavings => Files.Skip(1).Sum(f => f.Size); // 절약 가능 용량

    // 파일 선택 도우미 메서드
    public FileEntry? GetOldest() => Files.OrderBy(f => f.CreatedDate).FirstOrDefault();
    public FileEntry? GetNewest() => Files.OrderByDescending(f => f.ModifiedDate).FirstOrDefault();
    public FileEntry? GetSmallest() => Files.OrderBy(f => f.Size).FirstOrDefault();
    public FileEntry? GetLargest() => Files.OrderByDescending(f => f.Size).FirstOrDefault();
}

/// <summary>
/// 중복 유형 열거형
/// </summary>
public enum DuplicateType
{
    ExactMatch,   // 정확히 일치 (동일 해시)
    SimilarImage, // 유사 이미지 (지각 해시 기반)
    SimilarVideo, // 유사 비디오 (키프레임 기반)
    SimilarAudio  // 유사 오디오 (핑거프린트 기반)
}
