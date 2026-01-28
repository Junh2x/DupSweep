namespace DupSweep.Core.Models;

/// <summary>
/// 스캔 결과 모델 클래스
/// 스캔 완료 후 발견된 중복 그룹, 통계 정보, 오류 여부 등 포함
/// </summary>
public class ScanResult
{
    public Guid ScanId { get; set; } = Guid.NewGuid();
    public DateTime StartTime { get; set; }           // 스캔 시작 시간
    public DateTime EndTime { get; set; }             // 스캔 종료 시간
    public TimeSpan Duration => EndTime - StartTime;  // 소요 시간

    public ScanConfig Config { get; set; } = new();   // 사용된 스캔 설정
    public List<DuplicateGroup> DuplicateGroups { get; set; } = new(); // 중복 그룹 목록

    // 통계 정보
    public int TotalFilesScanned { get; set; }
    public int TotalDuplicates => DuplicateGroups.Sum(g => g.FileCount - 1);
    public long TotalPotentialSavings => DuplicateGroups.Sum(g => g.PotentialSavings);

    // 결과 상태
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }

    // 중복 유형별 필터링 메서드
    public IEnumerable<DuplicateGroup> GetExactMatches()
        => DuplicateGroups.Where(g => g.Type == DuplicateType.ExactMatch);

    public IEnumerable<DuplicateGroup> GetSimilarImages()
        => DuplicateGroups.Where(g => g.Type == DuplicateType.SimilarImage);

    public IEnumerable<DuplicateGroup> GetSimilarVideos()
        => DuplicateGroups.Where(g => g.Type == DuplicateType.SimilarVideo);

    public IEnumerable<DuplicateGroup> GetSimilarAudio()
        => DuplicateGroups.Where(g => g.Type == DuplicateType.SimilarAudio);
}
