using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using FinalProjectFleetLink.Services;
using ScottPlot.WinForms;

namespace FinalProjectFleetLink.Controls;

public sealed class DashboardControl : UserControl
{
    private readonly DashboardService dashboardService = new();
    private readonly ComboBox branchComboBox = new();
    private readonly Button refreshButton = new();
    private readonly FlowLayoutPanel cardsPanel = new();
    private readonly DataGridView expiringGrid = new();
    private readonly DataGridView abnormalFuelGrid = new();
    private readonly FormsPlot statusChart = new();
    private readonly Dictionary<string, Label> kpiValues = new();
    private bool loading;

    public DashboardControl()
    {
        Dock = DockStyle.Fill;
        BackColor = UiStyles.ContentBackground;
        InitializeControls();
        Load += DashboardControl_Load;
    }

    private void InitializeControls()
    {
        var filterPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48
        };
        var branchLabel = new Label
        {
            Text = "Branch:",
            AutoSize = true,
            Location = new Point(0, 9)
        };
        branchComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        branchComboBox.Width = 210;
        branchComboBox.Location = new Point(58, 4);
        branchComboBox.SelectedIndexChanged += async (_, _) =>
        {
            if (!loading)
            {
                await RefreshAsync();
            }
        };
        refreshButton.Text = "Refresh";
        refreshButton.Width = 95;
        refreshButton.Height = 30;
        refreshButton.Location = new Point(280, 3);
        refreshButton.Click += async (_, _) => await RefreshAsync();
        filterPanel.Controls.AddRange([branchLabel, branchComboBox, refreshButton]);

        cardsPanel.Dock = DockStyle.Top;
        cardsPanel.Height = 174;
        cardsPanel.AutoScroll = true;
        cardsPanel.WrapContents = true;
        cardsPanel.Padding = new Padding(0, 0, 0, 8);
        AddKpiCard("Total Vehicles", "TotalVehicles");
        AddKpiCard("Available", "AvailableVehicles");
        AddKpiCard("On Trip", "VehiclesOnTrip");
        AddKpiCard("In Maintenance", "VehiclesInMaintenance");
        AddKpiCard("Active Drivers", "ActiveDrivers");
        AddKpiCard("Drivers On Leave", "DriversOnLeave");
        AddKpiCard("Vehicles Without Driver", "VehiclesWithoutCurrentDriver");
        AddKpiCard("Drivers Without Vehicle", "DriversWithoutCurrentVehicle");
        AddKpiCard("Trips Today", "TripsToday");
        AddKpiCard("Trips In Transit", "TripsInTransit");
        AddKpiCard("Abnormal Fuel Flags", "AbnormalFuelFlags");
        AddKpiCard("Maintenance Due", "MaintenanceDue");
        AddKpiCard("Registrations Expiring", "RegistrationsExpiring");
        AddKpiCard("Licenses Expiring", "LicensesExpiring");

        ConfigureGrid(expiringGrid);
        ConfigureGrid(abnormalFuelGrid);
        var listsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0, 0, 0, 8)
        };
        listsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        listsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        listsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        listsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        listsPanel.Controls.Add(CreateSectionLabel("Expiring soon"), 0, 0);
        listsPanel.Controls.Add(CreateSectionLabel("Recent abnormal fuel logs"), 1, 0);
        listsPanel.Controls.Add(expiringGrid, 0, 1);
        listsPanel.Controls.Add(abnormalFuelGrid, 1, 1);

        statusChart.Dock = DockStyle.Fill;
        statusChart.BackColor = Color.White;

        var bottomPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 0, 0, 8)
        };
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        bottomPanel.Controls.Add(listsPanel, 0, 0);
        bottomPanel.Controls.Add(statusChart, 1, 0);

        Controls.Add(bottomPanel);
        Controls.Add(cardsPanel);
        Controls.Add(filterPanel);
    }

    private void AddKpiCard(string title, string key)
    {
        var card = new Panel
        {
            Width = 155,
            Height = 72,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 8, 8),
            Padding = new Padding(10)
        };
        var titleLabel = new Label
        {
            Text = title,
            AutoSize = false,
            Width = 135,
            Height = 28,
            ForeColor = Color.DimGray
        };
        var valueLabel = new Label
        {
            Text = "-",
            AutoSize = false,
            Width = 135,
            Height = 30,
            Top = 30,
            Font = new Font(UiStyles.DefaultFont, FontStyle.Bold),
            ForeColor = UiStyles.Text
        };
        card.Controls.Add(titleLabel);
        card.Controls.Add(valueLabel);
        cardsPanel.Controls.Add(card);
        kpiValues[key] = valueLabel;
    }

    private static Label CreateSectionLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        Font = new Font(UiStyles.DefaultFont, FontStyle.Bold),
        ForeColor = UiStyles.Text,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AutoGenerateColumns = true;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.RowHeadersVisible = false;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
    }

    private async void DashboardControl_Load(object? sender, EventArgs e)
    {
        try
        {
            loading = true;
            var options = await BranchFilterService.GetOptionsAsync(Module.Dashboard);
            branchComboBox.DataSource = options.ToList();
            branchComboBox.DisplayMember = nameof(BranchFilterOption.DisplayName);
            branchComboBox.ValueMember = nameof(BranchFilterOption.BranchId);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Dashboard filters could not be loaded.\n" + ex.Message,
                "Dashboard Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            loading = false;
        }

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (IsDisposed || !IsHandleCreated || branchComboBox.SelectedItem is not BranchFilterOption option)
        {
            return;
        }

        refreshButton.Enabled = false;
        try
        {
            var snapshot = await dashboardService.GetSnapshotAsync(option.BranchId);
            UpdateKpis(snapshot.Kpis);
            expiringGrid.DataSource = snapshot.ExpiringItems.Select(item => new
            {
                Type = item.RecordType,
                Name = item.Name,
                item.Branch,
                Expiry = item.ExpiryDate.ToString("yyyy-MM-dd")
            }).ToList();
            abnormalFuelGrid.DataSource = snapshot.RecentAbnormalFuelLogs.Select(log => new
            {
                Date = log.FuelDate.ToString("yyyy-MM-dd HH:mm"),
                log.PlateNo,
                log.Branch,
                Liters = log.Liters.ToString("0.##"),
                Cost = log.TotalCost.ToString("0.##"),
                log.Odometer
            }).ToList();
            UpdateChart(snapshot.VehicleStatuses);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Dashboard data could not be loaded.\n" + ex.Message,
                "Dashboard Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            refreshButton.Enabled = true;
        }
    }

    private void UpdateKpis(DashboardKpis kpis)
    {
        kpiValues[nameof(kpis.TotalVehicles)].Text = kpis.TotalVehicles.ToString();
        kpiValues[nameof(kpis.AvailableVehicles)].Text = kpis.AvailableVehicles.ToString();
        kpiValues[nameof(kpis.VehiclesOnTrip)].Text = kpis.VehiclesOnTrip.ToString();
        kpiValues[nameof(kpis.VehiclesInMaintenance)].Text = kpis.VehiclesInMaintenance.ToString();
        kpiValues[nameof(kpis.ActiveDrivers)].Text = kpis.ActiveDrivers.ToString();
        kpiValues[nameof(kpis.DriversOnLeave)].Text = kpis.DriversOnLeave.ToString();
        kpiValues[nameof(kpis.VehiclesWithoutCurrentDriver)].Text = kpis.VehiclesWithoutCurrentDriver.ToString();
        kpiValues[nameof(kpis.DriversWithoutCurrentVehicle)].Text = kpis.DriversWithoutCurrentVehicle.ToString();
        kpiValues[nameof(kpis.TripsToday)].Text = kpis.TripsToday.ToString();
        kpiValues[nameof(kpis.TripsInTransit)].Text = kpis.TripsInTransit.ToString();
        kpiValues[nameof(kpis.AbnormalFuelFlags)].Text = kpis.AbnormalFuelFlags.ToString();
        kpiValues[nameof(kpis.MaintenanceDue)].Text = kpis.MaintenanceDue.ToString();
        kpiValues[nameof(kpis.RegistrationsExpiring)].Text = kpis.RegistrationsExpiring.ToString();
        kpiValues[nameof(kpis.LicensesExpiring)].Text = kpis.LicensesExpiring.ToString();
    }

    private void UpdateChart(IReadOnlyList<VehicleStatusCount> statuses)
    {
        statusChart.Plot.Clear();
        var bars = statuses
            .Select((status, index) => new ScottPlot.Bar
            {
                Position = index,
                Value = status.Count
            })
            .ToArray();
        statusChart.Plot.Add.Bars(bars);
        statusChart.Plot.Axes.Bottom.SetTicks(
            statuses.Select((_, index) => (double)index).ToArray(),
            statuses.Select(status => status.Status).ToArray());
        statusChart.Plot.Title("Vehicles by status");
        statusChart.Plot.Axes.Margins(bottom: 0, top: 0.1);
        statusChart.Refresh();
    }
}
