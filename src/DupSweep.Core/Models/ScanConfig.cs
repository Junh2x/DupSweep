namespace DupSweep.Core.Models;

/// <summary>
/// 스캔 설정 모델 클래스
/// 스캔 대상 디렉토리, 비교 방법, 필터 조건 등 모든 스캔 옵션 포함
/// </summary>
public class ScanConfig
{
    // 스캔 대상 디렉토리 목록
    public List<string> Directories { get; set; } = new();

    // 중복 탐지 방법 설정
    public bool UseHashComparison { get; set; }        // 해시값 비교 사용
    public bool UseSizeComparison { get; set; }        // 파일 크기 비교 사용
    public bool UseResolutionComparison { get; set; }  // 해상도 비교 사용
    public bool UseImageSimilarity { get; set; }       // 이미지 유사도 비교 사용
    public bool UseVideoSimilarity { get; set; }       // 비디오 유사도 비교 사용
    public bool MatchCreatedDate { get; set; }         // 생성일 일치 필요
    public bool MatchModifiedDate { get; set; }        // 수정일 일치 필요

    // 스캔 대상 파일 유형
    public bool ScanAllFiles { get; set; } = true;  // 모든 파일 스캔 (해시 기반 중복 탐지)
    public bool ScanImages { get; set; } = true;    // 이미지 스캔 (유사도 비교용)
    public bool ScanVideos { get; set; } = true;    // 비디오 스캔 (유사도 비교용)
    public bool ScanAudio { get; set; } = false;    // 오디오 스캔
    public bool ScanDocuments { get; set; } = false; // 문서 스캔

    // 유사도 임계값 (0-100)
    public double ImageSimilarityThreshold { get; set; } = 85;
    public double VideoSimilarityThreshold { get; set; } = 85;

    // 썸네일 및 외부 도구 설정
    public int ThumbnailSize { get; set; } = 128;
    public string? FfmpegPath { get; set; }   // FFmpeg 실행 파일 경로
    public string? FfprobePath { get; set; }  // FFprobe 실행 파일 경로

    // 성능 설정
    public int ParallelThreads { get; set; } = Environment.ProcessorCount;
    public long MinFileSize { get; set; } = 0;           // 최소 파일 크기 (바이트)
    public long MaxFileSize { get; set; } = long.MaxValue; // 최대 파일 크기 (바이트)

    // 스캔 옵션
    public bool FollowSymlinks { get; set; } = false;     // 심볼릭 링크 추적
    public bool IncludeHiddenFiles { get; set; } = false; // 숨김 파일 포함
    public bool RecursiveScan { get; set; } = true;       // 하위 폴더 재귀 스캔

    /// <summary>
    /// 현재 설정에서 지원하는 파일 확장자 목록 반환
    /// ScanAllFiles가 true면 빈 리스트 반환 (모든 파일 스캔)
    /// </summary>
    public IEnumerable<string> GetSupportedExtensions()
    {
        // 모든 파일 스캔 시 빈 리스트 반환 (FileScanner에서 확장자 필터 건너뜀)
        if (ScanAllFiles)
        {
            return Enumerable.Empty<string>();
        }

        var extensions = new List<string>();

        if (ScanImages)
        {
            extensions.AddRange(new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".ico", ".heic", ".heif" });
        }

        if (ScanVideos)
        {
            extensions.AddRange(new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".3gp" });
        }

        if (ScanAudio)
        {
            extensions.AddRange(new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a" });
        }

        if (ScanDocuments)
        {
            extensions.AddRange(new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".hwp", ".hwpx" });
        }

        return extensions;
    }

    /// <summary>
    /// 이미지 확장자 목록 (유사도 비교용)
    /// </summary>
    public static IReadOnlyList<string> ImageExtensions { get; } = new[]
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".ico", ".heic", ".heif"
    };

    /// <summary>
    /// 비디오 확장자 목록 (유사도 비교용)
    /// </summary>
    public static IReadOnlyList<string> VideoExtensions { get; } = new[]
    {
        ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".3gp"
    };

    /// <summary>
    /// 오디오 확장자 목록
    /// </summary>
    public static IReadOnlyList<string> AudioExtensions { get; } = new[]
    {
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a"
    };

    /// <summary>
    /// 문서 확장자 목록
    /// </summary>
    public static IReadOnlyList<string> DocumentExtensions { get; } = new[]
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".hwp", ".hwpx"
    };
}
