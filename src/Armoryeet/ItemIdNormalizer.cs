namespace Armoryeet;

public static class ItemIdNormalizer
{
    private const uint HighQualityOffset = 1_000_000;

    public static uint Normalize(uint itemId)
    {
        return itemId > HighQualityOffset ? itemId - HighQualityOffset : itemId;
    }
}
