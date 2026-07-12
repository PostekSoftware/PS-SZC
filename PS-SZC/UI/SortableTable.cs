using Hexa.NET.ImGui;

namespace PS_SZC.UI;

internal static class SortableTable
{
    public const ImGuiTableFlags BaseFlags =
        ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable;

    public static void Sort<T>(List<T> items, ImGuiTableSortSpecsPtr sortSpecs, Func<T, int, IComparable?> getKey)
    {
        if (sortSpecs.SpecsCount <= 0)
            return;

        items.Sort((left, right) =>
        {
            for (var i = 0; i < sortSpecs.SpecsCount; i++)
            {
                var spec = sortSpecs.Specs[i];
                var keyLeft = getKey(left, spec.ColumnIndex) ?? string.Empty;
                var keyRight = getKey(right, spec.ColumnIndex) ?? string.Empty;
                var compare = Comparer<IComparable>.Default.Compare(keyLeft, keyRight);
                if (compare != 0)
                    return spec.SortDirection == ImGuiSortDirection.Ascending ? compare : -compare;
            }

            return 0;
        });

        if (sortSpecs.SpecsDirty)
            sortSpecs.SpecsDirty = false;
    }
}
