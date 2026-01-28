using DupSweep.Core.Models;

namespace DupSweep.Core.Algorithms;

/// <summary>
/// 디렉토리 스캔 알고리즘
/// 설정에 따라 파일을 탐색하고 필터링하여 FileEntry 목록 생성
/// </summary>
public class FileScanner
{
    /// <summary>
    /// 설정에 따라 디렉토리를 스캔하고 파일 목록 반환
    /// </summary>
    /// <param name="config">스캔 설정</param>
    /// <param name="onFileDiscovered">파일 발견 시 콜백</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <param name="pauseEvent">일시 정지 이벤트</param>
    public IEnumerable<FileEntry> Scan(ScanConfig config, Action<string>? onFileDiscovered, CancellationToken cancellationToken, ManualResetEventSlim? pauseEvent)
    {
        // 지원하는 확장자 목록 구성
        var extensions = new HashSet<string>(
            config.GetSupportedExtensions().Select(e => e.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        // 파일 열거 옵션 설정
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = config.RecursiveScan,
            IgnoreInaccessible = true,
            AttributesToSkip = GetAttributesToSkip(config)
        };

        // 중복 제거된 디렉토리 목록
        var directories = config.Directories
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*", options);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var filePath in files)
            {
                // 취소/일시정지 확인
                cancellationToken.ThrowIfCancellationRequested();
                pauseEvent?.Wait(cancellationToken);

                onFileDiscovered?.Invoke(filePath);

                FileInfo fileInfo;
                try
                {
                    fileInfo = new FileInfo(filePath);
                }
                catch
                {
                    continue;
                }

                // 파일 크기 필터
                if (fileInfo.Length < config.MinFileSize || fileInfo.Length > config.MaxFileSize)
                {
                    continue;
                }

                // 확장자 필터
                var extension = fileInfo.Extension.ToLowerInvariant();
                if (extensions.Count > 0 && !extensions.Contains(extension))
                {
                    continue;
                }

                yield return FileEntry.FromPath(filePath);
            }
        }
    }

    /// <summary>
    /// 설정에 따라 건너뛸 파일 속성 반환
    /// </summary>
    private static FileAttributes GetAttributesToSkip(ScanConfig config)
    {
        var attributes = FileAttributes.System;
        if (!config.IncludeHiddenFiles)
        {
            attributes |= FileAttributes.Hidden;
        }

        if (!config.FollowSymlinks)
        {
            attributes |= FileAttributes.ReparsePoint;
        }

        return attributes;
    }
}
