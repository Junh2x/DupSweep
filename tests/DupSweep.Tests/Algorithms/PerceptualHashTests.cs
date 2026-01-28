using DupSweep.Core.Algorithms;

namespace DupSweep.Tests.Algorithms;

public class PerceptualHashTests
{
    [Theory]
    [InlineData(0UL, 0UL, 0)]
    [InlineData(0UL, 1UL, 1)]
    [InlineData(1UL, 0UL, 1)]
    [InlineData(0b1111UL, 0b0000UL, 4)]
    [InlineData(0xFFFFFFFFFFFFFFFFUL, 0x0UL, 64)]
    [InlineData(0xAAAAAAAAAAAAAAAAUL, 0x5555555555555555UL, 64)]
    public void HammingDistance_ReturnsCorrectDistance(ulong left, ulong right, int expected)
    {
        // Act
        var result = PerceptualHash.HammingDistance(left, right);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void HammingDistance_IsCommunicative()
    {
        // Arrange
        ulong a = 0x123456789ABCDEF0UL;
        ulong b = 0xFEDCBA9876543210UL;

        // Act
        var distance1 = PerceptualHash.HammingDistance(a, b);
        var distance2 = PerceptualHash.HammingDistance(b, a);

        // Assert
        Assert.Equal(distance1, distance2);
    }

    [Theory]
    [InlineData(0UL, 0UL, 100.0)]
    [InlineData(0xFFFFFFFFFFFFFFFFUL, 0x0UL, 0.0)]
    public void SimilarityPercent_ReturnsCorrectPercentage(ulong left, ulong right, double expected)
    {
        // Act
        var result = PerceptualHash.SimilarityPercent(left, right);

        // Assert
        Assert.Equal(expected, result, precision: 2);
    }

    [Fact]
    public void SimilarityPercent_IdenticalHashes_Returns100()
    {
        // Arrange
        ulong hash = 0x123456789ABCDEF0UL;

        // Act
        var similarity = PerceptualHash.SimilarityPercent(hash, hash);

        // Assert
        Assert.Equal(100.0, similarity);
    }

    [Fact]
    public void SimilarityPercent_SimilarHashes_ReturnsHighPercentage()
    {
        // Arrange - 해시가 1비트만 다름
        ulong hash1 = 0x123456789ABCDEF0UL;
        ulong hash2 = 0x123456789ABCDEF1UL;

        // Act
        var similarity = PerceptualHash.SimilarityPercent(hash1, hash2);

        // Assert
        Assert.True(similarity > 98.0);
    }

    [Fact]
    public void SimilarityPercent_AlwaysBetween0And100()
    {
        // Arrange
        var random = new Random(42);

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            var bytes1 = new byte[8];
            var bytes2 = new byte[8];
            random.NextBytes(bytes1);
            random.NextBytes(bytes2);

            ulong hash1 = BitConverter.ToUInt64(bytes1);
            ulong hash2 = BitConverter.ToUInt64(bytes2);

            var similarity = PerceptualHash.SimilarityPercent(hash1, hash2);

            Assert.InRange(similarity, 0.0, 100.0);
        }
    }
}
