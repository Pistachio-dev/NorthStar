namespace NorthStar.Util;

internal static class IReadOnlyListExt
{
    internal static T? Get<T>(this IReadOnlyList<T> list, int idx)
    {
        if (idx < 0 || idx >= list.Count)
        {
            return default;
        }

        return list[idx];
    }
}