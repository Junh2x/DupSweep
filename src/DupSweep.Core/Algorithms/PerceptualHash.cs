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

    /// <summary>
    /// 구조 해시(dHash)와 색상 해시(ColorHash)를 결합한 유사도 계산
    /// 구조 60% + 색상 40% 가중 평균으로 색상이 다른 이미지의 오탐 방지
    /// </summary>
    public static double CombinedSimilarityPercent(
        ulong structureHash1, ulong structureHash2,
        ulong? colorHash1, ulong? colorHash2)
    {
        var details = CombinedSimilarityDetails(structureHash1, structureHash2, colorHash1, colorHash2);
        return details.Combined;
    }

    /// <summary>
    /// 구조/색상/결합 유사도를 함께 반환
    /// </summary>
    public static (double Structure, double Color, double Combined) CombinedSimilarityDetails(
        ulong structureHash1, ulong structureHash2,
        ulong? colorHash1, ulong? colorHash2)
    {
        double structureSim = SimilarityPercent(structureHash1, structureHash2);

        if (!colorHash1.HasValue || !colorHash2.HasValue)
        {
            return (structureSim, structureSim, structureSim);
        }

        double colorSim = SimilarityPercent(colorHash1.Value, colorHash2.Value);
        double combined = structureSim * 0.6 + colorSim * 0.4;
        return (structureSim, colorSim, combined);
    }
}
