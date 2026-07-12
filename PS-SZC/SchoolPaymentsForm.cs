using Hexa.NET.ImGui;
using Microsoft.EntityFrameworkCore;
using PS.APP;
using PS.APP.Localization;
using PS.APP.Menus;
using PS.APP.Windows;
using PS_SZC.Data;
using PS_SZC.Project;
using PS_SZC.Services;
using PS_SZC.UI;
using System.Globalization;
using System.Numerics;

namespace PS_SZC;

internal sealed class SchoolPaymentsForm : Form
{
    private enum AppTab
    {
        Families,
        Transfers,
        Summary,
        Reports
    }

    private enum FamilyEditorTab
    {
        Overview,
        Parents,
        Children,
        Prices,
        Discounts,
        Breakdown
    }

    private readonly SchoolPaymentsProjectSession _session = new();
    private AppTab _activeTab = AppTab.Families;
    private AppTab? _pendingTab;
    private FamilyEditorTab _familyEditorTab = FamilyEditorTab.Overview;
    private int _selectedFamilyId;
    private int _viewYear = DateTime.Today.Year;
    private int _viewMonth = DateTime.Today.Month;
    private string _statusMessage = string.Empty;

    private string _newFamilyPriceAmount = "500";
    private int _newFamilyPriceYear = DateTime.Today.Year;
    private int _newFamilyPriceMonth = DateTime.Today.Month;

    private string _newDiscountAmount = "50";
    private int _newDiscountYear = DateTime.Today.Year;
    private int _newDiscountMonth = DateTime.Today.Month;

    private string _newTransferAmount = "500";
    private string _newTransferNote = string.Empty;
    private int _newTransferYear = DateTime.Today.Year;
    private int _newTransferMonth = DateTime.Today.Month;
    private int _newTransferDay = DateTime.Today.Day;
    private int _newTransferFamilyId;

    private string _parent1FirstName = string.Empty;
    private string _parent1LastName = string.Empty;
    private string _parent1Pesel = string.Empty;
    private string _parent2FirstName = string.Empty;
    private string _parent2LastName = string.Empty;
    private string _parent2Pesel = string.Empty;

    private string _newChildFirstName = string.Empty;
    private string _newChildLastName = string.Empty;
    private string _newChildPesel = string.Empty;
    private int _newChildBirthYear = DateTime.Today.Year;
    private int _newChildBirthMonth = 1;
    private int _newChildBirthDay = 1;

    private string _startingBalanceText = "0";

    private string _familyListSearch = string.Empty;
    private string _transferFamilySearch = string.Empty;
    private string _summarySearch = string.Empty;

    private enum SummaryBalanceFilter
    {
        All,
        Overpaid,
        Underpaid,
        Settled
    }

    private SummaryBalanceFilter _summaryBalanceFilter = SummaryBalanceFilter.All;

    private bool _reportAllFamilies = true;
    private int _reportFamilyId;
    private string _reportFamilySearch = string.Empty;
    private int _reportFromYear = DateTime.Today.Year;
    private int _reportFromMonth = 1;
    private int _reportToYear = DateTime.Today.Year;
    private int _reportToMonth = DateTime.Today.Month;
    private ReportKind _reportKind = ReportKind.DuesByMonth;
    private ReportBalanceFilter _reportBalanceFilter = ReportBalanceFilter.All;
    private bool _reportHideZeroRows;
    private bool _reportOnlyWithDiscounts;
    private string _reportMinPaymentAmount = string.Empty;

    private int _editingChildId;
    private int _editingPriceId;
    private int _editingDiscountId;
    private int _editingTransferId;

    private bool _showExitConfirmation;
    private bool _exitPopupOpened;

    private const string ExitUnsavedPopupId = "##ExitUnsavedPopup";

    private string ExitUnsavedPopupName =>
        $"{LocalizedString.FromId("Exit.Unsaved.Title")}{ExitUnsavedPopupId}";

    public SchoolPaymentsForm()
    {
        Title = LocalizedString.FromId("App.Title");
        Size = new Vector2(1440, 900);
        Padding = new Vector2(16, 16);
    }

    public override void Draw()
    {
        var statusFooterHeight = GetStatusFooterHeight();

        if (ImGui.BeginChild(
                "AppMainContent",
                new Vector2(0, statusFooterHeight > 0 ? -statusFooterHeight : 0),
                ImGuiChildFlags.None))
        {
            DrawHeader();

            if (!_session.IsOpen)
            {
                ImGui.TextWrapped(LocalizedString.FromId("App.NoProjectHint"));
            }
            else if (ImGui.BeginTabBar("MainTabs"))
            {
                DrawTabItem(AppTab.Families, LocalizedString.FromId("Menu.View.Families"), DrawFamiliesTab);
                DrawTabItem(AppTab.Transfers, LocalizedString.FromId("Menu.View.Transfers"), DrawTransfersTab);
                DrawTabItem(AppTab.Summary, LocalizedString.FromId("Menu.View.Summary"), DrawSummaryTab);
                DrawTabItem(AppTab.Reports, LocalizedString.FromId("Menu.View.Reports"), DrawReportsTab);
                ImGui.EndTabBar();
            }

            ImGui.EndChild();
        }

        DrawStatus();
        DrawExitConfirmationDialog();
    }

    public override bool TryRequestClose()
    {
        if (!_session.HasUnsavedChanges)
            return true;

        if (Application.Instance.ActiveForm != this)
            Application.Instance.ReturnToMainForm();

        _showExitConfirmation = true;
        _exitPopupOpened = false;
        return false;
    }

    private void DrawExitConfirmationDialog()
    {
        if (!_showExitConfirmation)
            return;

        if (!_exitPopupOpened)
        {
            ImGui.OpenPopup(ExitUnsavedPopupName);
            _exitPopupOpened = true;
        }

        var viewport = ImGui.GetMainViewport();
        var center = viewport.Pos + viewport.Size * 0.5f;
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        if (!ImGui.BeginPopupModal(
                ExitUnsavedPopupName,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            return;

        ImGui.TextWrapped(LocalizedString.FromId("Exit.Unsaved.Message"));
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var buttonWidth = 120f;
        if (ImGui.Button(LocalizedString.FromId("Exit.Unsaved.Save"), new Vector2(buttonWidth, 0)))
        {
            _showExitConfirmation = false;
            ImGui.CloseCurrentPopup();
            _session.SaveProjectForExit(
                () => Application.Instance.Exit(),
                SetStatus);
        }

        ImGui.SameLine();
        if (ImGui.Button(LocalizedString.FromId("Exit.Unsaved.Discard"), new Vector2(buttonWidth, 0)))
        {
            _showExitConfirmation = false;
            ImGui.CloseCurrentPopup();
            Application.Instance.Exit();
        }

        ImGui.SameLine();
        if (ImGui.Button(LocalizedString.FromId("Exit.Unsaved.Cancel"), new Vector2(buttonWidth, 0)))
        {
            _showExitConfirmation = false;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void DrawTabItem(AppTab tab, string label, Action drawContent)
    {
        var selectFlags = _pendingTab == tab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        if (!ImGui.BeginTabItem(label, selectFlags))
            return;

        _activeTab = tab;
        if (_pendingTab == tab)
            _pendingTab = null;

        ImGui.BeginChild($"TabContent_{tab}", Vector2.Zero, ImGuiChildFlags.None);
        drawContent();
        ImGui.EndChild();
        ImGui.EndTabItem();
    }

    private void SelectTab(AppTab tab) => _pendingTab = tab;

    protected override void BuildMainMenu(MainMenuBuilder menu)
    {
        var file = menu.AddMenu(LocalizedString.FromId("Menu.File"));
        file.AddItem(
            LocalizedString.FromId("Menu.File.New"),
            () => _session.PromptCreateNewProject(SetStatus),
            "Ctrl+N",
            _session.CanUseFileDialogs);
        file.AddItem(
            LocalizedString.FromId("Menu.File.Open"),
            () => _session.PromptOpenProject(SetStatus),
            "Ctrl+O",
            _session.CanUseFileDialogs);
        file.AddSeparator();
        file.AddItem(
            LocalizedString.FromId("Menu.File.Save"),
            () => _session.SaveProject(SetStatus),
            "Ctrl+S",
            _session.IsOpen);
        file.AddItem(
            LocalizedString.FromId("Menu.File.SaveAs"),
            () => _session.PromptSaveProjectAs(SetStatus),
            "Ctrl+Shift+S",
            _session.IsOpen && _session.CanUseFileDialogs);
        file.AddItem(
            LocalizedString.FromId("Menu.File.CloseProject"),
            () => _session.CloseProject(SetStatus),
            enabled: _session.IsOpen);
        file.AddSeparator();

        if (Application.Instance.CanOpenSettings)
        {
            file.AddItem(
                LocalizedString.FromId("Menu.File.Settings"),
                OpenSettings,
                "Ctrl+,");
            file.AddSeparator();
        }

        file.AddItem(LocalizedString.FromId("Menu.File.Exit"), Close, "Alt+F4");

        var view = menu.AddMenu(LocalizedString.FromId("Menu.View"));
        view.AddItem(
            LocalizedString.FromId("Menu.View.Families"),
            () => SelectTab(AppTab.Families),
            isChecked: _activeTab == AppTab.Families);
        view.AddItem(
            LocalizedString.FromId("Menu.View.Transfers"),
            () => SelectTab(AppTab.Transfers),
            isChecked: _activeTab == AppTab.Transfers);
        view.AddItem(
            LocalizedString.FromId("Menu.View.Summary"),
            () => SelectTab(AppTab.Summary),
            isChecked: _activeTab == AppTab.Summary);
        view.AddItem(
            LocalizedString.FromId("Menu.View.Reports"),
            () => SelectTab(AppTab.Reports),
            isChecked: _activeTab == AppTab.Reports);

        var help = menu.AddMenu(LocalizedString.FromId("Menu.Help"));
        help.AddItem(LocalizedString.FromId("Menu.Help.About"), ShowAboutDialog);
    }

    private void ShowAboutDialog() =>
        SetStatus(LocalizedString.FromId("Menu.Help.AboutText"));

    private void DrawHeader()
    {
        if (_session.IsOpen && !string.IsNullOrWhiteSpace(_session.ProjectPath))
            ImGui.TextDisabled(_session.ProjectPath);

        ImGui.Text(LocalizedString.FromId("App.BalanceThrough"));
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        ImGui.InputInt($"{LocalizedString.FromId("App.View.Year")}##view", ref _viewYear);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(60);
        ImGui.InputInt($"{LocalizedString.FromId("App.View.Month")}##view", ref _viewMonth, 0, 0);
        _viewMonth = Math.Clamp(_viewMonth, 1, 12);
        ImGui.Separator();
    }

    private void DrawFamiliesTab()
    {
        var context = _session.Context!;
        var throughMonth = new BillingMonth(_viewYear, _viewMonth);
        var familyPrices = context.FamilyPrices.AsNoTracking().ToList();
        var discounts = context.FamilyDiscounts.AsNoTracking().ToList();
        var transfers = context.Transfers.AsNoTracking().ToList();

        if (ImGui.Button(LocalizedString.FromId("Family.AddFamily")))
        {
            var family = new Family();
            context.Families.Add(family);
            _session.SaveChanges();
            EnsureParents(context, family);
            _session.SaveChanges();
            _selectedFamilyId = family.Id;
            LoadFamilyEditor(family.Id);
        }

        ImGui.SetNextItemWidth(Math.Min(420f, ImGui.GetContentRegionAvail().X));
        ImGui.InputTextWithHint(
            "Search##familyList",
            LocalizedString.FromId("Family.SearchHint"),
            ref _familyListSearch,
            128);

        var availableHeight = ImGui.GetContentRegionAvail().Y;
        var listHeight = Math.Clamp(availableHeight * 0.32f, 180f, 280f);

        ImGui.BeginChild("FamiliesList", new Vector2(0, listHeight), ImGuiChildFlags.Borders);
        var families = context.Families
            .Include(x => x.Parents)
            .Include(x => x.Children)
            .AsNoTracking()
            .ToList();
        var familyRows = families
            .Select(family => (
                Family: family,
                Summary: PaymentBalanceService.CalculateFamilyBalance(
                    family, familyPrices, discounts, transfers, throughMonth)))
            .ToList();

        var filteredFamilyRows = familyRows
            .Where(row => FamilySearchService.Matches(row.Family, _familyListSearch))
            .ToList();

        if (ImGui.BeginTable("FamiliesTable", 3, SortableTable.BaseFlags | ImGuiTableFlags.ScrollY))
        {
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Family"), ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultSort);
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Balance"), ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Status"), ImGuiTableColumnFlags.WidthFixed, 160);
            ImGui.TableHeadersRow();

            SortableTable.Sort(filteredFamilyRows, ImGui.TableGetSortSpecs(), (row, column) => column switch
            {
                0 => row.Summary.DisplayName,
                1 => row.Summary.CurrentBalance,
                2 => GetBalanceStatus(row.Summary),
                _ => null
            });

            if (filteredFamilyRows.Count == 0)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextDisabled(LocalizedString.FromId("Family.SearchNoResults"));
            }

            foreach (var (family, summary) in filteredFamilyRows)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                var selected = _selectedFamilyId == family.Id;
                if (ImGui.Selectable(
                        $"{summary.DisplayName}##family{family.Id}",
                        selected,
                        ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap))
                {
                    _selectedFamilyId = family.Id;
                    LoadFamilyEditor(family.Id);
                }

                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(summary.CurrentBalance));
                ImGui.TableNextColumn();
                ImGui.Text(GetBalanceStatus(summary));
            }

            ImGui.EndTable();
        }

        ImGui.EndChild();

        if (_selectedFamilyId > 0)
        {
            var detailsHeight = Math.Max(120f, ImGui.GetContentRegionAvail().Y);
            ImGui.BeginChild(
                "FamilyDetails",
                new Vector2(0, detailsHeight),
                ImGuiChildFlags.Borders);
            DrawFamilyDetails(throughMonth);
            ImGui.EndChild();
        }
        else
        {
            ImGui.Spacing();
            ImGui.TextDisabled(LocalizedString.FromId("Family.SelectFromList"));
        }
    }

    private void DrawFamilyDetails(BillingMonth throughMonth)
    {
        var context = _session.Context!;
        var family = _session.LoadFamilyDetails(_selectedFamilyId);
        var familyPrices = context.FamilyPrices.AsNoTracking().ToList();
        var discounts = context.FamilyDiscounts.AsNoTracking().ToList();
        var transfers = context.Transfers.AsNoTracking().ToList();
        var summary = PaymentBalanceService.CalculateFamilyBalance(
            family, familyPrices, discounts, transfers, throughMonth);

        ImGui.Spacing();
        ImGui.SeparatorText(LocalizedString.FromId("Family.Header", () => summary.DisplayName));
        ImGui.TextDisabled(LocalizedString.FromId(
            "Family.SummaryLine",
            () => FormatMoney(summary.CurrentBalance),
            () => throughMonth.ToString(),
            () => GetBalanceStatus(summary)));

        if (ImGui.BeginTabBar("FamilyEditorTabs"))
        {
            DrawFamilyEditorTab(FamilyEditorTab.Overview, LocalizedString.FromId("Family.Editor.Overview"), () =>
                DrawFamilyOverviewTab(family, summary, throughMonth));
            DrawFamilyEditorTab(FamilyEditorTab.Parents, LocalizedString.FromId("Family.Editor.Parents"), () =>
            {
                DrawParentEditor(context, family, 1, ref _parent1FirstName, ref _parent1LastName, ref _parent1Pesel);
                DrawParentEditor(context, family, 2, ref _parent2FirstName, ref _parent2LastName, ref _parent2Pesel);
            });
            DrawFamilyEditorTab(FamilyEditorTab.Children, LocalizedString.FromId("Family.Editor.Children"), () =>
                DrawFamilyChildrenTab(context, family));
            DrawFamilyEditorTab(FamilyEditorTab.Prices, LocalizedString.FromId("Family.Editor.Prices"), () =>
                DrawFamilyPricingSection(family));
            DrawFamilyEditorTab(FamilyEditorTab.Discounts, LocalizedString.FromId("Family.Editor.Discounts"), () =>
                DrawFamilyDiscountSection(family));
            DrawFamilyEditorTab(FamilyEditorTab.Breakdown, LocalizedString.FromId("Family.Editor.Breakdown"), () =>
                DrawFamilyMonthlyBreakdown(summary));
            ImGui.EndTabBar();
        }
    }

    private void DrawFamilyEditorTab(FamilyEditorTab tab, string label, Action drawContent)
    {
        if (!ImGui.BeginTabItem(label))
            return;

        _familyEditorTab = tab;
        ImGui.BeginChild(
            $"FamilyEditorTab_{tab}",
            Vector2.Zero,
            ImGuiChildFlags.None,
            ImGuiWindowFlags.AlwaysVerticalScrollbar);
        drawContent();
        ImGui.EndChild();
        ImGui.EndTabItem();
    }

    private void DrawFamilyOverviewTab(Family family, FamilyBalanceSummary summary, BillingMonth throughMonth)
    {
        ImGui.Text(LocalizedString.FromId(
            "Family.BalanceThroughDetail",
            () => throughMonth.ToString(),
            () => FormatMoney(summary.CurrentBalance),
            () => GetBalanceStatus(summary)));
        ImGui.Text($"{LocalizedString.FromId("Family.StartingBalance")}: {FormatMoney(summary.StartingBalance)}");
        ImGui.Text($"{LocalizedString.FromId("Family.TotalTransfers")}: {FormatMoney(summary.TotalTransfers)}");
        ImGui.Text($"{LocalizedString.FromId("Family.TotalCharges")}: {FormatMoney(summary.TotalNetCharges)}");
        ImGui.Text($"{LocalizedString.FromId("Family.TotalDiscounts")}: {FormatMoney(summary.TotalDiscounts)}");

        ImGui.Spacing();
        ImGui.InputText($"{LocalizedString.FromId("Family.StartingBalance")}##family", ref _startingBalanceText, 32);
        if (ImGui.Button(LocalizedString.FromId("Family.SaveStartingBalance")))
        {
            if (decimal.TryParse(_startingBalanceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var balance)
                || decimal.TryParse(_startingBalanceText, NumberStyles.Number, CultureInfo.CurrentCulture, out balance))
            {
                family.StartingBalance = balance;
                _session.SaveChanges();
                SetStatus(LocalizedString.FromId("Family.StartingBalanceSaved"));
            }
            else
            {
                SetStatus(LocalizedString.FromId("Family.InvalidStartingBalance"));
            }
        }
    }

    private void DrawFamilyChildrenTab(SchoolPaymentsContext context, Family family)
    {
        foreach (var child in family.Children.OrderBy(x => x.Id).ToList())
        {
            ImGui.BulletText($"{child.FirstName} {child.LastName}, born {child.BirthDate:yyyy-MM-dd}{(string.IsNullOrWhiteSpace(child.Pesel) ? string.Empty : $", PESEL: {child.Pesel}")}");
            ImGui.SameLine();
            if (ImGui.SmallButton($"{LocalizedString.FromId("Family.Edit")}##child{child.Id}"))
                BeginEditChild(child);
            ImGui.SameLine();
            if (ImGui.SmallButton($"{LocalizedString.FromId("Family.Delete")}##child{child.Id}"))
            {
                if (_editingChildId == child.Id)
                    ClearChildForm();

                context.Children.Remove(child);
                _session.SaveChanges();
            }
        }

        if (family.Children.Count == 0)
            ImGui.TextDisabled(LocalizedString.FromId("Family.NoChildren"));

        ImGui.Spacing();
        ImGui.SeparatorText(_editingChildId > 0
            ? LocalizedString.FromId("Family.EditChild")
            : LocalizedString.FromId("Family.AddChild"));
        ImGui.InputText($"{LocalizedString.FromId("Field.FirstName")}##newChild", ref _newChildFirstName, 64);
        ImGui.InputText($"{LocalizedString.FromId("Field.LastName")}##newChild", ref _newChildLastName, 64);
        ImGui.InputInt($"{LocalizedString.FromId("Field.BirthYear")}##child", ref _newChildBirthYear);
        ImGui.InputInt($"{LocalizedString.FromId("Field.BirthMonth")}##child", ref _newChildBirthMonth);
        ImGui.InputInt($"{LocalizedString.FromId("Field.BirthDay")}##child", ref _newChildBirthDay);
        ImGui.InputText($"{LocalizedString.FromId("Field.PeselOptional")}##newChild", ref _newChildPesel, 11);
        if (ImGui.Button(_editingChildId > 0
                ? LocalizedString.FromId("Family.Save")
                : LocalizedString.FromId("Family.AddChild")))
        {
            SaveChild(context, family);
        }

        if (_editingChildId > 0 && ImGui.Button(LocalizedString.FromId("Family.Cancel")))
            ClearChildForm();
    }

    private void DrawParentEditor(
        SchoolPaymentsContext context,
        Family family,
        int parentIndex,
        ref string firstName,
        ref string lastName,
        ref string pesel)
    {
        ImGui.SeparatorText(LocalizedString.FromId("Family.ParentTitle", () => parentIndex));
        ImGui.InputText($"{LocalizedString.FromId("Field.FirstName")}##p{parentIndex}", ref firstName, 64);
        ImGui.InputText($"{LocalizedString.FromId("Field.LastName")}##p{parentIndex}", ref lastName, 64);
        ImGui.InputText($"{LocalizedString.FromId("Field.PeselOptional")}##p{parentIndex}", ref pesel, 11);
        if (ImGui.Button(LocalizedString.FromId("Field.SaveParent", () => parentIndex)))
        {
            var parent = family.Parents.FirstOrDefault(x => x.ParentIndex == parentIndex)
                         ?? new Parent { FamilyId = family.Id, ParentIndex = parentIndex };

            parent.FirstName = firstName.Trim();
            parent.LastName = lastName.Trim();
            parent.Pesel = string.IsNullOrWhiteSpace(pesel) ? null : pesel.Trim();

            if (parent.Id == 0)
            {
                context.Parents.Add(parent);
                family.Parents.Add(parent);
            }

            _session.SaveChanges();
            SetStatus(LocalizedString.FromId("Family.ParentSaved", () => parentIndex));
        }
    }

    private void DrawFamilyPricingSection(Family family)
    {
        ImGui.TextWrapped(LocalizedString.FromId("Family.PriceScheduleHint"));

        var orderedPrices = family.Prices.ToList();
        if (ImGui.BeginTable("FamilyPrices", 4, SortableTable.BaseFlags))
        {
            ImGui.TableSetupColumn(
                LocalizedString.FromId("Family.PriceStarts"),
                ImGuiTableColumnFlags.DefaultSort);
            ImGui.TableSetupColumn(LocalizedString.FromId("Family.PriceAmount"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Family.PriceUntil"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Family.Actions"), ImGuiTableColumnFlags.NoSort);
            ImGui.TableHeadersRow();

            SortableTable.Sort(orderedPrices, ImGui.TableGetSortSpecs(), (price, column) => column switch
            {
                0 => price.EffectiveYear * 100 + price.EffectiveMonth,
                1 => price.Amount,
                2 => GetPriceUntilSortKey(price, orderedPrices),
                _ => null
            });

            foreach (var price in orderedPrices)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text($"{price.EffectiveYear}-{price.EffectiveMonth:D2}");
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(price.Amount));
                ImGui.TableNextColumn();
                ImGui.Text(PaymentBalanceService.FormatPriceAppliesUntil(price, orderedPrices));
                ImGui.TableNextColumn();
                if (ImGui.SmallButton($"{LocalizedString.FromId("Family.Edit")}##fp{price.Id}"))
                    BeginEditPrice(price);
                ImGui.SameLine();
                if (ImGui.SmallButton($"{LocalizedString.FromId("Family.Delete")}##fp{price.Id}"))
                {
                    if (_editingPriceId == price.Id)
                        ClearPriceForm();

                    _session.Context!.FamilyPrices.Remove(price);
                    _session.SaveChanges();
                }
            }

            ImGui.EndTable();
        }

        ImGui.SeparatorText(_editingPriceId > 0
            ? LocalizedString.FromId("Family.EditPrice")
            : LocalizedString.FromId("Family.AddPrice"));
        ImGui.InputInt($"{LocalizedString.FromId("Field.StartYear")}##familyPriceYear", ref _newFamilyPriceYear);
        ImGui.InputInt($"{LocalizedString.FromId("Field.StartMonth")}##familyPriceMonth", ref _newFamilyPriceMonth);
        ImGui.InputText(LocalizedString.FromId("Family.PriceAmount"), ref _newFamilyPriceAmount, 32);
        if (ImGui.Button(_editingPriceId > 0
                ? LocalizedString.FromId("Family.Save")
                : LocalizedString.FromId("Family.AddPrice")))
        {
            SaveFamilyPrice(family.Id);
        }

        if (_editingPriceId > 0 && ImGui.Button(LocalizedString.FromId("Family.Cancel")))
            ClearPriceForm();
    }

    private void DrawFamilyDiscountSection(Family family)
    {
        var discounts = family.Discounts.ToList();
        if (ImGui.BeginTable("FamilyDiscounts", 3, SortableTable.BaseFlags))
        {
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Month"), ImGuiTableColumnFlags.DefaultSort);
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Discount"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Family.Actions"), ImGuiTableColumnFlags.NoSort);
            ImGui.TableHeadersRow();

            SortableTable.Sort(discounts, ImGui.TableGetSortSpecs(), (discount, column) => column switch
            {
                0 => discount.Year * 100 + discount.Month,
                1 => discount.Amount,
                _ => null
            });

            foreach (var discount in discounts)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text($"{discount.Year}-{discount.Month:D2}");
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(discount.Amount));
                ImGui.TableNextColumn();
                if (ImGui.SmallButton($"{LocalizedString.FromId("Family.Edit")}##fd{discount.Id}"))
                    BeginEditDiscount(discount);
                ImGui.SameLine();
                if (ImGui.SmallButton($"{LocalizedString.FromId("Family.Delete")}##fd{discount.Id}"))
                {
                    if (_editingDiscountId == discount.Id)
                        ClearDiscountForm();

                    _session.Context!.FamilyDiscounts.Remove(discount);
                    _session.SaveChanges();
                }
            }

            ImGui.EndTable();
        }

        ImGui.SeparatorText(_editingDiscountId > 0
            ? LocalizedString.FromId("Family.EditDiscount")
            : LocalizedString.FromId("Family.AddDiscount"));
        ImGui.InputInt($"{LocalizedString.FromId("Field.DiscountYear")}##familyDiscount", ref _newDiscountYear);
        ImGui.InputInt($"{LocalizedString.FromId("Field.DiscountMonth")}##familyDiscount", ref _newDiscountMonth);
        ImGui.InputText($"{LocalizedString.FromId("Family.DiscountAmount")}##familyDiscount", ref _newDiscountAmount, 32);
        if (ImGui.Button(_editingDiscountId > 0
                ? LocalizedString.FromId("Family.Save")
                : LocalizedString.FromId("Family.AddDiscount")))
        {
            SaveFamilyDiscount(family.Id);
        }

        if (_editingDiscountId > 0 && ImGui.Button(LocalizedString.FromId("Family.Cancel")))
            ClearDiscountForm();
    }

    private void DrawFamilyMonthlyBreakdown(FamilyBalanceSummary summary)
    {
        if (summary.MonthlyCharges.Count == 0)
        {
            ImGui.TextDisabled(LocalizedString.FromId("Family.NoMonthlyCharges"));
            return;
        }

        var monthlyCharges = summary.MonthlyCharges.ToList();
        if (ImGui.BeginTable("MonthlyBreakdown", 4, SortableTable.BaseFlags | ImGuiTableFlags.ScrollY, new Vector2(0, 0)))
        {
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Month"), ImGuiTableColumnFlags.DefaultSort);
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Gross"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Discount"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Net"));
            ImGui.TableHeadersRow();

            SortableTable.Sort(monthlyCharges, ImGui.TableGetSortSpecs(), (charge, column) => column switch
            {
                0 => charge.Month.Year * 100 + charge.Month.Month,
                1 => charge.GrossAmount,
                2 => charge.DiscountAmount,
                3 => charge.NetAmount,
                _ => null
            });

            foreach (var charge in monthlyCharges)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(charge.Month.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(charge.GrossAmount));
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(charge.DiscountAmount));
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(charge.NetAmount));
            }

            ImGui.EndTable();
        }
    }

    private void DrawTransfersTab()
    {
        var context = _session.Context!;
        var families = context.Families
            .Include(x => x.Parents)
            .Include(x => x.Children)
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToList();

        var transferRows = context.Transfers
            .Include(x => x.Family).ThenInclude(x => x.Parents)
            .Include(x => x.Family).ThenInclude(x => x.Children)
            .AsNoTracking()
            .ToList();

        if (ImGui.BeginTable("Transfers", 5, SortableTable.BaseFlags | ImGuiTableFlags.ScrollY, new Vector2(0, 280)))
        {
            ImGui.TableSetupColumn(
                LocalizedString.FromId("Field.Date"),
                ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending);
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Family"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Amount"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Field.Note"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Family.Actions"), ImGuiTableColumnFlags.NoSort);
            ImGui.TableHeadersRow();

            SortableTable.Sort(transferRows, ImGui.TableGetSortSpecs(), (transfer, column) => column switch
            {
                0 => transfer.TransferDate.DayNumber,
                1 => PaymentBalanceService.BuildFamilyDisplayName(transfer.Family),
                2 => transfer.Amount,
                3 => transfer.Note ?? string.Empty,
                _ => null
            });

            foreach (var transfer in transferRows)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(transfer.TransferDate.ToString("yyyy-MM-dd"));
                ImGui.TableNextColumn();
                ImGui.Text(PaymentBalanceService.BuildFamilyDisplayName(transfer.Family));
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(transfer.Amount));
                ImGui.TableNextColumn();
                ImGui.Text(transfer.Note ?? string.Empty);
                ImGui.TableNextColumn();
                if (ImGui.SmallButton($"{LocalizedString.FromId("Family.Edit")}##tr{transfer.Id}"))
                    BeginEditTransfer(transfer.Id);
                ImGui.SameLine();
                if (ImGui.SmallButton($"{LocalizedString.FromId("Family.Delete")}##tr{transfer.Id}"))
                {
                    if (_editingTransferId == transfer.Id)
                        ClearTransferForm();

                    var tracked = context.Transfers.First(x => x.Id == transfer.Id);
                    context.Transfers.Remove(tracked);
                    _session.SaveChanges();
                }
            }

            ImGui.EndTable();
        }

        if (families.Count == 0)
        {
            ImGui.TextDisabled(LocalizedString.FromId("Family.TransferNoFamilies"));
            return;
        }

        if (_newTransferFamilyId == 0 || families.All(x => x.Id != _newTransferFamilyId))
            _newTransferFamilyId = families[0].Id;

        ImGui.SeparatorText(_editingTransferId > 0
            ? LocalizedString.FromId("Family.EditTransfer")
            : LocalizedString.FromId("Family.AddTransfer"));
        FamilyPicker.DrawCombo("transfer", families, ref _newTransferFamilyId, ref _transferFamilySearch);

        ImGui.InputInt($"{LocalizedString.FromId("Field.TransferYear")}##transfer", ref _newTransferYear);
        ImGui.InputInt($"{LocalizedString.FromId("Field.TransferMonth")}##transfer", ref _newTransferMonth);
        ImGui.InputInt($"{LocalizedString.FromId("Field.TransferDay")}##transfer", ref _newTransferDay);
        ImGui.InputText($"{LocalizedString.FromId("Field.Amount")}##transfer", ref _newTransferAmount, 32);
        ImGui.InputText($"{LocalizedString.FromId("Field.Note")}##transfer", ref _newTransferNote, 256);
        if (ImGui.Button(_editingTransferId > 0
                ? LocalizedString.FromId("Family.Save")
                : LocalizedString.FromId("Family.AddTransfer")))
        {
            SaveTransfer();
        }

        if (_editingTransferId > 0 && ImGui.Button(LocalizedString.FromId("Family.Cancel")))
            ClearTransferForm();
    }

    private void DrawSummaryTab()
    {
        var context = _session.Context!;
        var throughMonth = new BillingMonth(_viewYear, _viewMonth);
        var familyPrices = context.FamilyPrices.AsNoTracking().ToList();
        var discounts = context.FamilyDiscounts.AsNoTracking().ToList();
        var transfers = context.Transfers.AsNoTracking().ToList();

        ImGui.SetNextItemWidth(Math.Min(420f, ImGui.GetContentRegionAvail().X));
        ImGui.InputTextWithHint(
            "Search##summary",
            LocalizedString.FromId("Family.SearchHint"),
            ref _summarySearch,
            128);

        ImGui.Text(LocalizedString.FromId("Summary.Filter.Status"));
        ImGui.SameLine();
        if (ImGui.RadioButton(
                LocalizedString.FromId("Summary.Filter.All"),
                _summaryBalanceFilter == SummaryBalanceFilter.All))
            _summaryBalanceFilter = SummaryBalanceFilter.All;
        ImGui.SameLine();
        if (ImGui.RadioButton(
                LocalizedString.FromId("Summary.Filter.Overpaid"),
                _summaryBalanceFilter == SummaryBalanceFilter.Overpaid))
            _summaryBalanceFilter = SummaryBalanceFilter.Overpaid;
        ImGui.SameLine();
        if (ImGui.RadioButton(
                LocalizedString.FromId("Summary.Filter.Underpaid"),
                _summaryBalanceFilter == SummaryBalanceFilter.Underpaid))
            _summaryBalanceFilter = SummaryBalanceFilter.Underpaid;
        ImGui.SameLine();
        if (ImGui.RadioButton(
                LocalizedString.FromId("Summary.Filter.Settled"),
                _summaryBalanceFilter == SummaryBalanceFilter.Settled))
            _summaryBalanceFilter = SummaryBalanceFilter.Settled;

        var summaryRows = context.Families
            .Include(x => x.Parents)
            .Include(x => x.Children)
            .AsNoTracking()
            .AsEnumerable()
            .Select(family => (
                Family: family,
                Summary: PaymentBalanceService.CalculateFamilyBalance(
                    family, familyPrices, discounts, transfers, throughMonth)))
            .Where(row =>
                FamilySearchService.Matches(row.Family, _summarySearch)
                && MatchesSummaryBalanceFilter(row.Summary, _summaryBalanceFilter))
            .Select(row => row.Summary)
            .ToList();

        if (ImGui.BeginTable("Summary", 6, SortableTable.BaseFlags | ImGuiTableFlags.ScrollY))
        {
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Family"), ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultSort);
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.StartingBalance"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Transfers"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Charges"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Balance"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Status"));
            ImGui.TableHeadersRow();

            SortableTable.Sort(summaryRows, ImGui.TableGetSortSpecs(), (summary, column) => column switch
            {
                0 => summary.DisplayName,
                1 => summary.StartingBalance,
                2 => summary.TotalTransfers,
                3 => summary.TotalNetCharges,
                4 => summary.CurrentBalance,
                5 => GetBalanceStatus(summary),
                _ => null
            });

            if (summaryRows.Count == 0)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextDisabled(LocalizedString.FromId("Family.SearchNoResults"));
            }

            foreach (var summary in summaryRows)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(summary.DisplayName);
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(summary.StartingBalance));
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(summary.TotalTransfers));
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(summary.TotalNetCharges));
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(summary.CurrentBalance));
                ImGui.TableNextColumn();
                ImGui.Text(GetBalanceStatus(summary));
            }

            ImGui.EndTable();
        }
    }

    private void DrawReportsTab()
    {
        ImGui.TextWrapped(LocalizedString.FromId("Report.Description"));
        ImGui.Spacing();

        ImGui.Text(LocalizedString.FromId("Report.FamilyFilter"));
        if (ImGui.Checkbox(LocalizedString.FromId("Report.AllFamilies"), ref _reportAllFamilies))
        {
            if (_reportAllFamilies)
                _reportFamilySearch = string.Empty;
        }

        if (!_reportAllFamilies)
        {
            var context = _session.Context!;
            var families = context.Families
                .Include(x => x.Parents)
                .Include(x => x.Children)
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToList();

            if (families.Count == 0)
            {
                ImGui.TextDisabled(LocalizedString.FromId("Report.NoFamilies"));
            }
            else
            {
                if (_reportFamilyId == 0 || families.All(x => x.Id != _reportFamilyId))
                    _reportFamilyId = families[0].Id;

                FamilyPicker.DrawCombo("report", families, ref _reportFamilyId, ref _reportFamilySearch);
            }
        }

        ImGui.Spacing();
        ImGui.Text(LocalizedString.FromId("Report.DateRange"));
        ImGui.SetNextItemWidth(90);
        ImGui.InputInt($"{LocalizedString.FromId("Report.FromYear")}##reportFromYear", ref _reportFromYear);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(70);
        ImGui.InputInt($"{LocalizedString.FromId("Report.FromMonth")}##reportFromMonth", ref _reportFromMonth);
        ImGui.SameLine();
        ImGui.Text("—");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90);
        ImGui.InputInt($"{LocalizedString.FromId("Report.ToYear")}##reportToYear", ref _reportToYear);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(70);
        ImGui.InputInt($"{LocalizedString.FromId("Report.ToMonth")}##reportToMonth", ref _reportToMonth);

        ImGui.Spacing();
        ImGui.Text(LocalizedString.FromId("Report.AdditionalFilters"));

        if (_reportAllFamilies)
        {
            ImGui.SetNextItemWidth(Math.Min(420f, ImGui.GetContentRegionAvail().X));
            ImGui.InputTextWithHint(
                "Search##reportFamilies",
                LocalizedString.FromId("Family.SearchHint"),
                ref _reportFamilySearch,
                128);
        }

        if (_reportKind == ReportKind.AccountStatus)
        {
            ImGui.Text(LocalizedString.FromId("Report.BalanceFilter"));
            DrawReportBalanceFilterRadio(ReportBalanceFilter.All, "Summary.Filter.All");
            ImGui.SameLine();
            DrawReportBalanceFilterRadio(ReportBalanceFilter.Overpaid, "Summary.Filter.Overpaid");
            ImGui.SameLine();
            DrawReportBalanceFilterRadio(ReportBalanceFilter.Underpaid, "Summary.Filter.Underpaid");
            ImGui.SameLine();
            DrawReportBalanceFilterRadio(ReportBalanceFilter.Settled, "Summary.Filter.Settled");
        }

        if (_reportKind is ReportKind.DuesByMonth or ReportKind.PaymentsByMonth)
            ImGui.Checkbox(LocalizedString.FromId("Report.HideZeroRows"), ref _reportHideZeroRows);

        if (_reportKind == ReportKind.DuesByMonth)
            ImGui.Checkbox(LocalizedString.FromId("Report.OnlyWithDiscounts"), ref _reportOnlyWithDiscounts);

        if (_reportKind == ReportKind.PaymentsByMonth)
        {
            ImGui.SetNextItemWidth(120);
            ImGui.InputText(LocalizedString.FromId("Report.MinPaymentAmount"), ref _reportMinPaymentAmount, 32);
        }

        ImGui.Spacing();
        ImGui.Text(LocalizedString.FromId("Report.DataSelection"));
        DrawReportKindRadio(ReportKind.DuesByMonth, "Report.Kind.DuesByMonth");
        DrawReportKindRadio(ReportKind.PaymentsByMonth, "Report.Kind.PaymentsByMonth");
        DrawReportKindRadio(ReportKind.AccountStatus, "Report.Kind.AccountStatus");

        ImGui.Spacing();
        var canGenerate = _reportAllFamilies || _reportFamilyId > 0;

        if (!canGenerate)
            ImGui.BeginDisabled();

        if (ImGui.Button(LocalizedString.FromId("Report.Generate")))
            OpenReportPreview();

        if (!canGenerate)
            ImGui.EndDisabled();
    }

    private void DrawReportKindRadio(ReportKind kind, string labelId)
    {
        if (ImGui.RadioButton(LocalizedString.FromId(labelId), _reportKind == kind))
            _reportKind = kind;
    }

    private void DrawReportBalanceFilterRadio(ReportBalanceFilter filter, string labelId)
    {
        if (ImGui.RadioButton(LocalizedString.FromId(labelId), _reportBalanceFilter == filter))
            _reportBalanceFilter = filter;
    }

    private void OpenReportPreview()
    {
        var context = _session.Context!;
        var families = context.Families
            .Include(x => x.Parents)
            .Include(x => x.Children)
            .AsNoTracking()
            .ToList();

        if (families.Count == 0)
        {
            SetStatus(LocalizedString.FromId("Report.NoFamilies"));
            return;
        }

        if (!_reportAllFamilies && _reportFamilyId <= 0)
        {
            SetStatus(LocalizedString.FromId("Family.SelectFamily"));
            return;
        }

        var fromMonth = new BillingMonth(_reportFromYear, Math.Clamp(_reportFromMonth, 1, 12));
        var toMonth = new BillingMonth(_reportToYear, Math.Clamp(_reportToMonth, 1, 12));
        var prices = context.FamilyPrices.AsNoTracking().ToList();
        var discounts = context.FamilyDiscounts.AsNoTracking().ToList();
        var transfers = context.Transfers.AsNoTracking().ToList();

        decimal? minPaymentAmount = null;
        if (_reportKind == ReportKind.PaymentsByMonth
            && !string.IsNullOrWhiteSpace(_reportMinPaymentAmount)
            && TryParseMoney(_reportMinPaymentAmount, out var parsedMinPayment))
        {
            minPaymentAmount = parsedMinPayment;
        }

        var filters = new ReportFilters(
            _reportAllFamilies ? null : _reportFamilyId,
            _reportAllFamilies ? _reportFamilySearch : string.Empty,
            _reportKind == ReportKind.AccountStatus ? _reportBalanceFilter : ReportBalanceFilter.All,
            _reportHideZeroRows,
            _reportOnlyWithDiscounts,
            minPaymentAmount);

        var document = ReportService.Build(
            _reportKind,
            families,
            prices,
            discounts,
            transfers,
            fromMonth,
            toMonth,
            filters,
            LocalizedString.FromId("Report.AllFamilies"));

        Application.Instance.ShowWindow(
            new ReportPreviewForm(document),
            new AppWindowOptions { Size = new Vector2(960, 640), ShowMenuBar = false });

        SetStatus(LocalizedString.FromId("Report.Opened"));
    }

    private static bool MatchesSummaryBalanceFilter(
        FamilyBalanceSummary summary,
        SummaryBalanceFilter filter) =>
        filter switch
        {
            SummaryBalanceFilter.Overpaid => summary.IsOverpaid,
            SummaryBalanceFilter.Underpaid => summary.IsUnderpaid,
            SummaryBalanceFilter.Settled => !summary.IsOverpaid && !summary.IsUnderpaid,
            _ => true
        };

    private void BeginEditChild(Child child)
    {
        _editingChildId = child.Id;
        _newChildFirstName = child.FirstName;
        _newChildLastName = child.LastName;
        _newChildBirthYear = child.BirthDate.Year;
        _newChildBirthMonth = child.BirthDate.Month;
        _newChildBirthDay = child.BirthDate.Day;
        _newChildPesel = child.Pesel ?? string.Empty;
    }

    private void ClearChildForm()
    {
        _editingChildId = 0;
        _newChildFirstName = string.Empty;
        _newChildLastName = string.Empty;
        _newChildPesel = string.Empty;
        _newChildBirthYear = DateTime.Today.Year;
        _newChildBirthMonth = 1;
        _newChildBirthDay = 1;
    }

    private void SaveChild(SchoolPaymentsContext context, Family family)
    {
        if (string.IsNullOrWhiteSpace(_newChildFirstName) || string.IsNullOrWhiteSpace(_newChildLastName))
        {
            SetStatus(LocalizedString.FromId("Status.ChildNameRequired"));
            return;
        }

        try
        {
            var birthDate = new DateOnly(
                _newChildBirthYear,
                Math.Clamp(_newChildBirthMonth, 1, 12),
                Math.Clamp(_newChildBirthDay, 1, 28));

            if (_editingChildId > 0)
            {
                var child = family.Children.First(x => x.Id == _editingChildId);
                child.FirstName = _newChildFirstName.Trim();
                child.LastName = _newChildLastName.Trim();
                child.BirthDate = birthDate;
                child.Pesel = string.IsNullOrWhiteSpace(_newChildPesel) ? null : _newChildPesel.Trim();
                SetStatus(LocalizedString.FromId("Status.ChildUpdated"));
            }
            else
            {
                context.Children.Add(new Child
                {
                    FamilyId = family.Id,
                    FirstName = _newChildFirstName.Trim(),
                    LastName = _newChildLastName.Trim(),
                    BirthDate = birthDate,
                    Pesel = string.IsNullOrWhiteSpace(_newChildPesel) ? null : _newChildPesel.Trim()
                });
                SetStatus(LocalizedString.FromId("Status.ChildAdded"));
            }

            _session.SaveChanges();
            ClearChildForm();
        }
        catch (Exception ex)
        {
            SetStatus(LocalizedString.FromId("Status.InvalidChildData", () => ex.Message));
        }
    }

    private void BeginEditPrice(FamilyPrice price)
    {
        _editingPriceId = price.Id;
        _newFamilyPriceYear = price.EffectiveYear;
        _newFamilyPriceMonth = price.EffectiveMonth;
        _newFamilyPriceAmount = price.Amount.ToString(CultureInfo.InvariantCulture);
    }

    private void ClearPriceForm()
    {
        _editingPriceId = 0;
        _newFamilyPriceYear = DateTime.Today.Year;
        _newFamilyPriceMonth = DateTime.Today.Month;
        _newFamilyPriceAmount = "500";
    }

    private void BeginEditDiscount(FamilyDiscount discount)
    {
        _editingDiscountId = discount.Id;
        _newDiscountYear = discount.Year;
        _newDiscountMonth = discount.Month;
        _newDiscountAmount = discount.Amount.ToString(CultureInfo.InvariantCulture);
    }

    private void ClearDiscountForm()
    {
        _editingDiscountId = 0;
        _newDiscountYear = DateTime.Today.Year;
        _newDiscountMonth = DateTime.Today.Month;
        _newDiscountAmount = "50";
    }

    private void BeginEditTransfer(int transferId)
    {
        var transfer = _session.Context!.Transfers.First(x => x.Id == transferId);
        _editingTransferId = transfer.Id;
        _newTransferFamilyId = transfer.FamilyId;
        _newTransferYear = transfer.TransferDate.Year;
        _newTransferMonth = transfer.TransferDate.Month;
        _newTransferDay = transfer.TransferDate.Day;
        _newTransferAmount = transfer.Amount.ToString(CultureInfo.InvariantCulture);
        _newTransferNote = transfer.Note ?? string.Empty;
        _transferFamilySearch = string.Empty;
    }

    private void ClearTransferForm()
    {
        _editingTransferId = 0;
        _newTransferAmount = "500";
        _newTransferNote = string.Empty;
        _newTransferYear = DateTime.Today.Year;
        _newTransferMonth = DateTime.Today.Month;
        _newTransferDay = DateTime.Today.Day;
        _transferFamilySearch = string.Empty;
    }

    private void SaveFamilyPrice(int familyId)
    {
        if (!TryParseMoney(_newFamilyPriceAmount, out var amount))
        {
            SetStatus(LocalizedString.FromId("Status.InvalidFamilyPrice"));
            return;
        }

        var context = _session.Context!;
        var month = Math.Clamp(_newFamilyPriceMonth, 1, 12);
        if (context.FamilyPrices.Any(x =>
                x.FamilyId == familyId
                && x.EffectiveYear == _newFamilyPriceYear
                && x.EffectiveMonth == month
                && x.Id != _editingPriceId))
        {
            SetStatus(LocalizedString.FromId("Status.FamilyPriceDuplicateMonth"));
            return;
        }

        if (_editingPriceId > 0)
        {
            var price = context.FamilyPrices.First(x => x.Id == _editingPriceId);
            price.EffectiveYear = _newFamilyPriceYear;
            price.EffectiveMonth = month;
            price.Amount = amount;
            SetStatus(LocalizedString.FromId("Status.FamilyPriceUpdated"));
        }
        else
        {
            context.FamilyPrices.Add(new FamilyPrice
            {
                FamilyId = familyId,
                EffectiveYear = _newFamilyPriceYear,
                EffectiveMonth = month,
                Amount = amount
            });
            SetStatus(LocalizedString.FromId("Status.FamilyPriceAdded"));
        }

        _session.SaveChanges();
        ClearPriceForm();
    }

    private void SaveFamilyDiscount(int familyId)
    {
        if (!TryParseMoney(_newDiscountAmount, out var amount))
        {
            SetStatus(LocalizedString.FromId("Status.InvalidDiscount"));
            return;
        }

        var context = _session.Context!;
        var month = Math.Clamp(_newDiscountMonth, 1, 12);
        if (context.FamilyDiscounts.Any(x =>
                x.FamilyId == familyId
                && x.Year == _newDiscountYear
                && x.Month == month
                && x.Id != _editingDiscountId))
        {
            SetStatus(LocalizedString.FromId("Status.DiscountDuplicateMonth"));
            return;
        }

        if (_editingDiscountId > 0)
        {
            var discount = context.FamilyDiscounts.First(x => x.Id == _editingDiscountId);
            discount.Year = _newDiscountYear;
            discount.Month = month;
            discount.Amount = amount;
            SetStatus(LocalizedString.FromId("Status.DiscountUpdated"));
        }
        else
        {
            context.FamilyDiscounts.Add(new FamilyDiscount
            {
                FamilyId = familyId,
                Year = _newDiscountYear,
                Month = month,
                Amount = amount
            });
            SetStatus(LocalizedString.FromId("Status.DiscountAdded"));
        }

        _session.SaveChanges();
        ClearDiscountForm();
    }

    private void SaveTransfer()
    {
        if (!TryParseMoney(_newTransferAmount, out var amount))
        {
            SetStatus(LocalizedString.FromId("Status.InvalidTransfer"));
            return;
        }

        try
        {
            var date = new DateOnly(
                _newTransferYear,
                Math.Clamp(_newTransferMonth, 1, 12),
                Math.Clamp(_newTransferDay, 1, 28));
            var context = _session.Context!;

            if (_editingTransferId > 0)
            {
                var transfer = context.Transfers.First(x => x.Id == _editingTransferId);
                transfer.FamilyId = _newTransferFamilyId;
                transfer.TransferDate = date;
                transfer.Amount = amount;
                transfer.Note = string.IsNullOrWhiteSpace(_newTransferNote) ? null : _newTransferNote.Trim();
                SetStatus(LocalizedString.FromId("Status.TransferUpdated"));
            }
            else
            {
                context.Transfers.Add(new Transfer
                {
                    FamilyId = _newTransferFamilyId,
                    TransferDate = date,
                    Amount = amount,
                    Note = string.IsNullOrWhiteSpace(_newTransferNote) ? null : _newTransferNote.Trim()
                });
                SetStatus(LocalizedString.FromId("Status.TransferAdded"));
            }

            _session.SaveChanges();
            ClearTransferForm();
        }
        catch (Exception ex)
        {
            SetStatus(LocalizedString.FromId("Status.InvalidTransferDate", () => ex.Message));
        }
    }

    private void LoadFamilyEditor(int familyId)
    {
        var context = _session.Context!;
        var family = _session.LoadFamilyDetails(familyId);
        EnsureParents(context, family);
        if (context.ChangeTracker.HasChanges())
            _session.SaveChanges();

        _startingBalanceText = family.StartingBalance.ToString(CultureInfo.InvariantCulture);

        var parent1 = family.Parents.FirstOrDefault(x => x.ParentIndex == 1);
        _parent1FirstName = parent1?.FirstName ?? string.Empty;
        _parent1LastName = parent1?.LastName ?? string.Empty;
        _parent1Pesel = parent1?.Pesel ?? string.Empty;

        var parent2 = family.Parents.FirstOrDefault(x => x.ParentIndex == 2);
        _parent2FirstName = parent2?.FirstName ?? string.Empty;
        _parent2LastName = parent2?.LastName ?? string.Empty;
        _parent2Pesel = parent2?.Pesel ?? string.Empty;

        ClearChildForm();
        ClearPriceForm();
        ClearDiscountForm();
    }

    private static void EnsureParents(SchoolPaymentsContext context, Family family)
    {
        if (family.Parents.All(x => x.ParentIndex != 1))
            context.Parents.Add(new Parent { Family = family, FamilyId = family.Id, ParentIndex = 1 });

        if (family.Parents.All(x => x.ParentIndex != 2))
            context.Parents.Add(new Parent { Family = family, FamilyId = family.Id, ParentIndex = 2 });
    }

    private static bool TryParseMoney(string text, out decimal amount) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
        || decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out amount);

    private static string FormatMoney(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string GetBalanceStatus(FamilyBalanceSummary summary)
    {
        if (summary.IsOverpaid)
            return LocalizedString.FromId("Report.Status.Overpaid", () => FormatMoney(summary.OverpaidAmount));

        if (summary.IsUnderpaid)
            return LocalizedString.FromId("Report.Status.Underpaid", () => FormatMoney(summary.UnderpaidAmount));

        return LocalizedString.FromId("Report.Status.Settled");
    }

    private static int GetPriceUntilSortKey(FamilyPrice price, IReadOnlyList<FamilyPrice> familyPrices)
    {
        var until = PaymentBalanceService.GetPriceAppliesUntilMonth(price, familyPrices);
        if (until == null)
            return int.MaxValue;

        return until.Value.Year * 100 + until.Value.Month;
    }

    private float GetStatusFooterHeight()
    {
        if (string.IsNullOrWhiteSpace(_statusMessage))
            return 0f;

        var style = ImGui.GetStyle();
        var wrapWidth = Math.Max(1f, ImGui.GetContentRegionAvail().X);
        var textHeight = ImGui.CalcTextSize(_statusMessage, wrapWidth).Y;
        return textHeight + style.ItemSpacing.Y * 2 + 1f;
    }

    private void DrawStatus()
    {
        if (string.IsNullOrWhiteSpace(_statusMessage))
            return;

        ImGui.Separator();
        ImGui.TextWrapped(_statusMessage);
    }

    private void SetStatus(string message) => _statusMessage = message;
}
