namespace DupSweep.Core.Algorithms;

/// <summary>
/// 지각 해시 유틸리티 클래스
/// 64비트 해시 간의 해밍 거리 및 유사도 계산
/// </summary>
public static class PerceptualHash
{
    /// <summary>
    /// 두 해시 간의 해밍 거리 계산
    /// 해밍 거리는 서로 다른 비트 수를 의미 (0-64)
    /// </summary>
    public static int HammingDistance(ulong left, ulong right)
    {
        ulong value = left ^ right;
        int count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }
        return count;
    }

    /// <summary>
    /// 두 해시 간의 유사도를 백분율로 반환 (0-100)
    /// 해밍 거리가 0이면 100%, 64이면 0%
    /// </summary>
    public static double SimilarityPercent(ulong left, ulong right)
    {
        var distance = HammingDistance(left, right);
        return 100d - (distance / 64d * 100d);
    }
}
