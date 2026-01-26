namespace DupSweep.Core.Models;

public class FileEntry
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Directory { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public FileType FileType { get; set; }

    // Hash values (computed on demand)
    public string? QuickHash { get; set; }
    public string? FullHash { get; set; }
    public ulong? PerceptualHash { get; set; }
    public ulong? AudioFingerprint { get; set; }

    // Thumbnail
    public string? ThumbnailPath { get; set; }
    public byte[]? ThumbnailData { get; set; }

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

public enum FileType
{
    Image,
    Video,
    Audio,
    Other
}
