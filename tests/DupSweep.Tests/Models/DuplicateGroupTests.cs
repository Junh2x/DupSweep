using DupSweep.Core.Models;

namespace DupSweep.Tests.Models;

public class DuplicateGroupTests
{
    [Fact]
    public void DuplicateGroup_DefaultValues_AreCorrect()
    {
        // Act
        var group = new DuplicateGroup();

        // Assert
        Assert.NotEqual(Guid.Empty, group.Id);
        Assert.Empty(group.Files);
        Assert.Equal(100, group.Similarity);
        Assert.Equal(0, group.FileCount);
        Assert.Equal(0, group.TotalSize);
        Assert.Equal(0, group.PotentialSavings);
    }

    [Fact]
    public void FileCount_ReturnsCorrectCount()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Files = new List<FileEntry>
            {
                new() { FilePath = "file1.txt", Size = 100 },
                new() { FilePath = "file2.txt", Size = 100 },
                new() { FilePath = "file3.txt", Size = 100 }
            }
        };

        // Act & Assert
        Assert.Equal(3, group.FileCount);
    }

    [Fact]
    public void TotalSize_SumsAllFileSizes()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Files = new List<FileEntry>
            {
                new() { Size = 100 },
                new() { Size = 200 },
                new() { Size = 300 }
            }
        };

        // Act & Assert
        Assert.Equal(600, group.TotalSize);
    }

    [Fact]
    public void PotentialSavings_SkipsFirstFile()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Files = new List<FileEntry>
            {
                new() { Size = 100 },
                new() { Size = 100 },
                new() { Size = 100 }
            }
        };

        // Act - 첫 번째 파일 유지, 나머지 삭제 가능
        var savings = group.PotentialSavings;

        // Assert
        Assert.Equal(200, savings); // 100 + 100 (skip first 100)
    }

    [Fact]
    public void GetOldest_ReturnsFileWithEarliestCreatedDate()
    {
        // Arrange
        var oldest = new FileEntry { FilePath = "oldest.txt", CreatedDate = new DateTime(2020, 1, 1) };
        var middle = new FileEntry { FilePath = "middle.txt", CreatedDate = new DateTime(2021, 1, 1) };
        var newest = new FileEntry { FilePath = "newest.txt", CreatedDate = new DateTime(2022, 1, 1) };

        var group = new DuplicateGroup
        {
            Files = new List<FileEntry> { middle, newest, oldest }
        };

        // Act
        var result = group.GetOldest();

        // Assert
        Assert.Equal("oldest.txt", result?.FilePath);
    }

    [Fact]
    public void GetNewest_ReturnsFileWithLatestModifiedDate()
    {
        // Arrange
        var oldest = new FileEntry { FilePath = "oldest.txt", ModifiedDate = new DateTime(2020, 1, 1) };
        var newest = new FileEntry { FilePath = "newest.txt", ModifiedDate = new DateTime(2022, 1, 1) };

        var group = new DuplicateGroup
        {
            Files = new List<FileEntry> { oldest, newest }
        };

        // Act
        var result = group.GetNewest();

        // Assert
        Assert.Equal("newest.txt", result?.FilePath);
    }

    [Fact]
    public void GetSmallest_ReturnsFileWithSmallestSize()
    {
        // Arrange
        var small = new FileEntry { FilePath = "small.txt", Size = 100 };
        var medium = new FileEntry { FilePath = "medium.txt", Size = 500 };
        var large = new FileEntry { FilePath = "large.txt", Size = 1000 };

        var group = new DuplicateGroup
        {
            Files = new List<FileEntry> { large, small, medium }
        };

        // Act
        var result = group.GetSmallest();

        // Assert
        Assert.Equal("small.txt", result?.FilePath);
    }

    [Fact]
    public void GetLargest_ReturnsFileWithLargestSize()
    {
        // Arrange
        var small = new FileEntry { FilePath = "small.txt", Size = 100 };
        var large = new FileEntry { FilePath = "large.txt", Size = 1000 };

        var group = new DuplicateGroup
        {
            Files = new List<FileEntry> { small, large }
        };

        // Act
        var result = group.GetLargest();

        // Assert
        Assert.Equal("large.txt", result?.FilePath);
    }

    [Fact]
    public void GetOldest_EmptyList_ReturnsNull()
    {
        // Arrange
        var group = new DuplicateGroup();

        // Act & Assert
        Assert.Null(group.GetOldest());
        Assert.Null(group.GetNewest());
        Assert.Null(group.GetSmallest());
        Assert.Null(group.GetLargest());
    }

    [Theory]
    [InlineData(DuplicateType.ExactMatch)]
    [InlineData(DuplicateType.SimilarImage)]
    [InlineData(DuplicateType.SimilarVideo)]
    [InlineData(DuplicateType.SimilarAudio)]
    public void DuplicateType_AllTypesAvailable(DuplicateType type)
    {
        // Arrange & Act
        var group = new DuplicateGroup { Type = type };

        // Assert
        Assert.Equal(type, group.Type);
    }
}
