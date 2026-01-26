namespace DupSweep.Core.Algorithms;

public static class PerceptualHash
{
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

    public static double SimilarityPercent(ulong left, ulong right)
    {
        var distance = HammingDistance(left, right);
        return 100d - (distance / 64d * 100d);
    }
}
