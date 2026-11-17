using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using FinalProjectFleetLink.Services;
using ScottPlot.WinForms;

namespace FinalProjectFleetLink.Controls;

public sealed class MaintenanceDashboardControl : UserControl
{
    private readonly MaintenanceDashboardService service = new();
    private readonly ComboBox branchComboBox = new();
    private readonly Button refreshButton = new();
    private readonly FlowLayoutPanel cardsPanel = new();
    private readonly DataGridView serviceGrid = new();
    private readonly DataGridView workshopGrid = new();
    private readonly FormsPlot costChart = new();
    private readonly Dictionary<string, Label> values = new();
    private bool loading;

    public MaintenanceDashboardControl()
    {
        Dock = DockStyle.Fill;
        BackColor = UiStyles.ContentBackground;
        InitializeControls();
        Load += async (_, _) => await LoadDashboardAsync();
    }

    private void InitializeControls()
    {
        var title = new Label { Text = "Maintenance Dashboard", Dock = DockStyle.Top, Height = 36, Font = new Font(UiStyles.DefaultFont, FontStyle.Bold), ForeColor = UiStyles.Text };
        var filters = new Panel { Dock = DockStyle.Top, Height = 48 };
        filters.Controls.Add(new Label { Text = "Branch:", AutoSize = true, Location = new Point(0, 9) });
        branchComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        branchComboBox.Width = 210;
        branchComboBox.Location = new Point(58, 4);
        branchComboBox.SelectedIndexChanged += async (_, _) => { if (!loading) await LoadDashboardAsync(); };
        refreshButton.Text = "Refresh";
        refreshButton.Width = 95;
        refreshButton.Height = 30;
        refreshButton.Location = new Point(280, 3);
        refreshButton.Click += async (_, _) => await LoadDashboardAsync();
        filters.Controls.AddRange([branchComboBox, refreshButton]);

        cardsPanel.Dock = DockStyle.Top;
        cardsPanel.Height = 92;
        cardsPanel.WrapContents = false;
        cardsPanel.AutoScroll = true;
        AddCard("Overdue", nameof(MaintenanceDashboardKpis.Overdue));
        AddCard("Due within 7 days", nameof(MaintenanceDashboardKpis.DueWithinSevenDays));
        AddCard("Due within 500 km", nameof(MaintenanceDashboardKpis.DueWithinFiveHundredKm));
        AddCard("In Progress", nameof(MaintenanceDashboardKpis.InProgress));
        AddCard("Completed this month", nameof(MaintenanceDashboardKpis.CompletedThisMonth));
        AddCard("Cost this month", nameof(MaintenanceDashboardKpis.CostThisMonth));

        ConfigureGrid(serviceGrid);
        ConfigureGrid(workshopGrid);
        serviceGrid.AutoGenerateColumns = false;
        AddColumn(serviceGrid, "Plate", nameof(MaintenanceUrgencyRow.Plate));
        AddColumn(serviceGrid, "Type", nameof(MaintenanceUrgencyRow.Type));
        AddColumn(serviceGrid, "Due date", nameof(MaintenanceUrgencyRow.DueDate));
        AddColumn(serviceGrid, "Due odometer", nameof(MaintenanceUrgencyRow.DueOdometer));
        AddColumn(serviceGrid, "Current odometer", nameof(MaintenanceUrgencyRow.CurrentOdometer));
        AddColumn(serviceGrid, "Due state", nameof(MaintenanceUrgencyRow.DueState));
        AddColumn(serviceGrid, "Days overdue", nameof(MaintenanceUrgencyRow.DaysOverdue));
        AddColumn(serviceGrid, "Km overdue", nameof(MaintenanceUrgencyRow.KmOverdue));
        workshopGrid.AutoGenerateColumns = false;
        AddColumn(workshopGrid, "Plate", nameof(WorkshopRow.Plate));
        AddColumn(workshopGrid, "Type", nameof(WorkshopRow.Type));
        AddColumn(workshopGrid, "Start date", nameof(WorkshopRow.StartDate));
        serviceGrid.CellFormatting += (_, e) => { if (e.RowIndex >= 0 && serviceGrid.Rows[e.RowIndex].DataBoundItem is MaintenanceUrgencyRow row) e.CellStyle.BackColor = MaintenanceDueHelper.GetRowColor(row.DueState); };

        var lists = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 8, 0, 8) };
        lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        lists.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        lists.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        lists.Controls.Add(LabelFor("Vehicles to service"), 0, 0);
        lists.Controls.Add(LabelFor("Currently in the workshop"), 1, 0);
        lists.Controls.Add(serviceGrid, 0, 1);
        lists.Controls.Add(workshopGrid, 1, 1);

        costChart.Dock = DockStyle.Bottom;
        costChart.Height = 190;
        costChart.BackColor = Color.White;

        Controls.Add(lists);
        Controls.Add(costChart);
        Controls.Add(cardsPanel);
        Controls.Add(filters);
        Controls.Add(title);
    }

    private async Task LoadDashboardAsync()
    {
        try
        {
            loading = true;
            var options = await BranchFilterService.GetOptionsAsync(Module.Maintenance);
            branchComboBox.DataSource = options.ToList();
            branchComboBox.DisplayMember = nameof(BranchFilterOption.DisplayName);
            branchComboBox.ValueMember = nameof(BranchFilterOption.BranchId);
            loading = false;
            if (branchComboBox.SelectedItem is BranchFilterOption option)
            {
                refreshButton.Enabled = false;
                var snapshot = await service.GetSnapshotAsync(option.BranchId);
                UpdateCards(snapshot.Kpis);
                serviceGrid.DataSource = snapshot.VehiclesToService.Select(item => new MaintenanceUrgencyRow(item)).ToList();
                workshopGrid.DataSource = snapshot.CurrentlyInWorkshop.Select(item => new WorkshopRow(item)).ToList();
                UpdateChart(snapshot.MonthlyCosts);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Maintenance dashboard could not be loaded.\n" + ex.Message, "Maintenance Dashboard", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            loading = false;
            refreshButton.Enabled = true;
        }
    }

    private void UpdateCards(MaintenanceDashboardKpis kpis)
    {
        values[nameof(kpis.Overdue)].Text = kpis.Overdue.ToString();
        values[nameof(kpis.DueWithinSevenDays)].Text = kpis.DueWithinSevenDays.ToString();
        values[nameof(kpis.DueWithinFiveHundredKm)].Text = kpis.DueWithinFiveHundredKm.ToString();
        values[nameof(kpis.InProgress)].Text = kpis.InProgress.ToString();
        values[nameof(kpis.CompletedThisMonth)].Text = kpis.CompletedThisMonth.ToString();
        values[nameof(kpis.CostThisMonth)].Text = kpis.CostThisMonth.ToString("0.##");
    }

    private void UpdateChart(IReadOnlyList<MaintenanceCostMonth> months)
    {
        costChart.Plot.Clear();
        costChart.Plot.Add.Bars(months.Select((item, index) => new ScottPlot.Bar { Position = index, Value = (double)item.Cost }).ToArray());
        costChart.Plot.Axes.Bottom.SetTicks(months.Select((_, index) => (double)index).ToArray(), months.Select(item => item.Month).ToArray());
        costChart.Plot.Title("Maintenance cost per month - last 6 months");
        costChart.Refresh();
    }

    private void AddCard(string title, string key)
    {
        var card = new Panel { Width = 155, Height = 72, BackColor = Color.White, Margin = new Padding(0, 0, 8, 8), Padding = new Padding(10) };
        card.Controls.Add(new Label { Text = title, Width = 135, Height = 28, ForeColor = Color.DimGray });
        var value = new Label { Text = "-", Width = 135, Height = 30, Top = 30, Font = new Font(UiStyles.DefaultFont, FontStyle.Bold), ForeColor = UiStyles.Text };
        card.Controls.Add(value);
        cardsPanel.Controls.Add(card);
        values[key] = value;
    }

    private static void ConfigureGrid(DataGridView grid) { grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.RowHeadersVisible = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; grid.BackgroundColor = Color.White; }
    private static void AddColumn(DataGridView grid, string header, string property) => grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header, DataPropertyName = property });
    private static Label LabelFor(string text) => new() { Text = text, Dock = DockStyle.Fill, Font = new Font(UiStyles.DefaultFont, FontStyle.Bold), ForeColor = UiStyles.Text, TextAlign = ContentAlignment.MiddleLeft };

    private sealed class MaintenanceUrgencyRow(MaintenanceUrgencyItem item)
    {
        public string Plate { get; } = item.PlateNo;
        public string Type { get; } = item.MaintenanceType.ToString();
        public string DueDate { get; } = item.ScheduledDate.ToString("yyyy-MM-dd");
        public string DueOdometer { get; } = item.DueOdometer?.ToString("N0") ?? "";
        public string CurrentOdometer { get; } = item.CurrentOdometer.ToString("N0");
        public MaintenanceDueState DueState { get; } = item.DueState;
        public int DaysOverdue { get; } = item.DaysOverdue;
        public int KmOverdue { get; } = item.KmOverdue;
    }

    private sealed class WorkshopRow(MaintenanceWorkshopItem item)
    {
        public string Plate { get; } = item.PlateNo;
        public string Type { get; } = item.MaintenanceType.ToString();
        public string StartDate { get; } = item.ScheduledDate.ToString("yyyy-MM-dd");
    }
}
