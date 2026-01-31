namespace DupSweep.Core.Models;

/// <summary>
/// 스캔된 파일 정보를 담는 모델 클래스
/// 파일 경로, 크기, 해시값, 썸네일 등 중복 탐지에 필요한 모든 정보 포함
/// </summary>
public class FileEntry
{
    // 기본 파일 정보
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Directory { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public FileType FileType { get; set; }

    // 이미지/비디오 해상도
    public int Width { get; set; }
    public int Height { get; set; }

    // 계산된 프로퍼티
    public string Resolution => Width > 0 && Height > 0 ? $"{Width}×{Height}" : "-";
    public string FormattedSize
    {
        get
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = Size;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }
    }

    // 해시값 (필요 시 계산)
    public string? QuickHash { get; set; }      // 빠른 비교용 부분 해시
    public string? FullHash { get; set; }       // 정확한 비교용 전체 해시
    public ulong? PerceptualHash { get; set; }  // 이미지/비디오 유사도 비교용 지각 해시
    public ulong? AudioFingerprint { get; set; } // 오디오 유사도 비교용 핑거프린트

    // 썸네일 정보
    public string? ThumbnailPath { get; set; }
    public byte[]? ThumbnailData { get; set; }

    /// <summary>
    /// 파일 경로로부터 FileEntry 객체 생성
    /// </summary>
    public static FileEntry FromPath(string path)
    {
        var fileInfo = new FileInfo(path);
        return new FileEntry
        {
            FilePath = path,
            FileName = fileInfo.Name,
            Directory = fileInfo.DirectoryName ?? string.Empty,
            Extension = fileInfo.Extension.ToLowerInvariant(),
            Size = fileInfo.Length,
            CreatedDate = fileInfo.CreationTime,
            ModifiedDate = fileInfo.LastWriteTime,
            FileType = GetFileType(fileInfo.Extension)
        };
    }

    /// <summary>
    /// 확장자로 파일 유형 판별
    /// </summary>
    private static FileType GetFileType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".tiff" or ".ico" or ".heic" or ".heif" => FileType.Image,
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".flv" or ".webm" or ".m4v" or ".3gp" => FileType.Video,
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".wma" or ".m4a" or ".opus" => FileType.Audio,
            _ => FileType.Other
        };
    }
}

/// <summary>
/// 파일 유형 열거형
/// </summary>
public enum FileType
{
    Image,  // 이미지 파일
    Video,  // 비디오 파일
    Audio,  // 오디오 파일
    Other   // 기타 파일
}
