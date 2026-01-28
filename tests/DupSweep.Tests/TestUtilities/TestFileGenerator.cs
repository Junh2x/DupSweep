using System.Text;

namespace DupSweep.Tests.TestUtilities;

/// <summary>
/// 테스트용 더미 파일 및 폴더 생성 유틸리티
/// </summary>
public class TestFileGenerator : IDisposable
{
    private readonly string _testRootPath;
    private bool _disposed;

    public string TestRootPath => _testRootPath;

    public TestFileGenerator()
    {
        _testRootPath = Path.Combine(Path.GetTempPath(), $"DupSweepTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRootPath);
    }

    /// <summary>
    /// 지정된 크기의 텍스트 파일 생성
    /// </summary>
    public string CreateTextFile(string relativePath, int sizeInBytes, string? content = null)
    {
        var fullPath = Path.Combine(_testRootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        if (content != null)
        {
            File.WriteAllText(fullPath, content);
        }
        else
        {
            var data = new byte[sizeInBytes];
            Random.Shared.NextBytes(data);
            File.WriteAllBytes(fullPath, data);
        }

        return fullPath;
    }

    /// <summary>
    /// 동일한 내용의 중복 파일 생성
    /// </summary>
    public List<string> CreateDuplicateFiles(string baseName, int count, int sizeInBytes, string extension = ".dat")
    {
        var content = new byte[sizeInBytes];
        Random.Shared.NextBytes(content);

        var files = new List<string>();
        for (int i = 1; i <= count; i++)
        {
            var path = Path.Combine(_testRootPath, $"{baseName}_{i}{extension}");
            File.WriteAllBytes(path, content);
            files.Add(path);
        }

        return files;
    }

    /// <summary>
    /// 서로 다른 폴더에 중복 파일 생성
    /// </summary>
    public List<string> CreateDuplicateFilesInFolders(string fileName, string[] folderNames, int sizeInBytes)
    {
        var content = new byte[sizeInBytes];
        Random.Shared.NextBytes(content);

        var files = new List<string>();
        foreach (var folder in folderNames)
        {
            var folderPath = Path.Combine(_testRootPath, folder);
            Directory.CreateDirectory(folderPath);
            var filePath = Path.Combine(folderPath, fileName);
            File.WriteAllBytes(filePath, content);
            files.Add(filePath);
        }

        return files;
    }

    /// <summary>
    /// 간단한 테스트용 이미지 파일 생성 (PNG 헤더만 포함)
    /// </summary>
    public string CreateFakeImageFile(string relativePath, int width = 100, int height = 100)
    {
        var fullPath = Path.Combine(_testRootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        // 최소한의 PNG 파일 구조 생성
        using var ms = new MemoryStream();
        // PNG signature
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        // IHDR chunk
        ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x0D }); // length
        ms.Write(Encoding.ASCII.GetBytes("IHDR"));
        ms.Write(BitConverter.GetBytes(width).Reverse().ToArray()); // width (big-endian)
        ms.Write(BitConverter.GetBytes(height).Reverse().ToArray()); // height (big-endian)
        ms.Write(new byte[] { 0x08, 0x02, 0x00, 0x00, 0x00 }); // bit depth, color type, etc.
        ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // CRC (simplified)
        // IEND chunk
        ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        ms.Write(Encoding.ASCII.GetBytes("IEND"));
        ms.Write(new byte[] { 0xAE, 0x42, 0x60, 0x82 }); // CRC

        File.WriteAllBytes(fullPath, ms.ToArray());
        return fullPath;
    }

    /// <summary>
    /// 다양한 확장자의 파일들 생성
    /// </summary>
    public Dictionary<string, string> CreateFilesWithExtensions(string[] extensions, int sizeInBytes = 1024)
    {
        var files = new Dictionary<string, string>();
        foreach (var ext in extensions)
        {
            var fileName = $"test_file{ext}";
            var path = CreateTextFile(fileName, sizeInBytes);
            files[ext] = path;
        }
        return files;
    }

    /// <summary>
    /// 하위 폴더 구조 생성
    /// </summary>
    public string CreateSubDirectory(string relativePath)
    {
        var fullPath = Path.Combine(_testRootPath, relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    /// <summary>
    /// 다양한 크기의 파일들 생성
    /// </summary>
    public List<(string Path, long Size)> CreateFilesWithSizes(params int[] sizes)
    {
        var files = new List<(string Path, long Size)>();
        for (int i = 0; i < sizes.Length; i++)
        {
            var path = CreateTextFile($"size_test_{i}.dat", sizes[i]);
            files.Add((path, sizes[i]));
        }
        return files;
    }

    /// <summary>
    /// 숨김 파일 생성 (Windows)
    /// </summary>
    public string CreateHiddenFile(string relativePath, int sizeInBytes = 100)
    {
        var path = CreateTextFile(relativePath, sizeInBytes);
        File.SetAttributes(path, FileAttributes.Hidden);
        return path;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            try
            {
                if (Directory.Exists(_testRootPath))
                {
                    Directory.Delete(_testRootPath, recursive: true);
                }
            }
            catch
            {
                // 테스트 정리 실패는 무시
            }
        }

        _disposed = true;
    }

    ~TestFileGenerator()
    {
        Dispose(false);
    }
}
