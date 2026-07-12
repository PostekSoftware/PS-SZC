using Hexa.NET.ImGui;
using PS.APP.Localization;
using PS_SZC.Data;
using PS_SZC.Services;

namespace PS_SZC.UI;

internal static class FamilyPicker
{
    public static void DrawCombo(
        string id,
        IReadOnlyList<Family> families,
        ref int selectedFamilyId,
        ref string searchText)
    {
        var currentSelection = selectedFamilyId;
        var selected = families.FirstOrDefault(x => x.Id == currentSelection);
        var preview = selected == null
            ? LocalizedString.FromId("Family.SelectFamily").ToString()
            : PaymentBalanceService.BuildFamilyDisplayName(selected);

        if (!ImGui.BeginCombo($"Family##{id}", preview))
            return;

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint(
            $"Search##{id}",
            LocalizedString.FromId("Family.SearchHint"),
            ref searchText,
            128);

        var filtered = FamilySearchService.Filter(families, searchText).ToList();
        if (filtered.Count == 0)
        {
            ImGui.TextDisabled(LocalizedString.FromId("Family.SearchNoResults"));
        }
        else
        {
            foreach (var family in filtered)
            {
                if (!ImGui.Selectable(
                        $"{PaymentBalanceService.BuildFamilyDisplayName(family)}##familyPick{family.Id}",
                        family.Id == currentSelection))
                {
                    continue;
                }

                selectedFamilyId = family.Id;
                searchText = string.Empty;
            }
        }

        ImGui.EndCombo();
    }
}
