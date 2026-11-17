using FinalProjectFleetLink.Forms;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Controls;

public sealed class DispatcherDashboardControl : UserControl
{
    private readonly DispatcherDashboardService service = new();
    private readonly ComboBox branchComboBox = new();
    private readonly Button refreshButton = new();
    private readonly FlowLayoutPanel cardsPanel = new();
    private readonly DataGridView dispatchGrid = new();
    private readonly DataGridView lateGrid = new();
    private readonly Dictionary<string, Label> values = new();
    private bool loading;

    public DispatcherDashboardControl()
    {
        Dock = DockStyle.Fill;
        BackColor = UiStyles.ContentBackground;
        InitializeControls();
        Load += Dashboard_Load;
    }

    private void InitializeControls()
    {
        var title = new Label { Text = "Dispatcher Dashboard", Dock = DockStyle.Top, Height = 36, Font = new Font(UiStyles.DefaultFont, FontStyle.Bold), ForeColor = UiStyles.Text };
        var filterPanel = new Panel { Dock = DockStyle.Top, Height = 48 };
        var branchLabel = new Label { Text = "Branch:", AutoSize = true, Location = new Point(0, 9) };
        branchComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        branchComboBox.Width = 210;
        branchComboBox.Location = new Point(58, 4);
        branchComboBox.SelectedIndexChanged += async (_, _) => { if (!loading) await RefreshAsync(); };
        refreshButton.Text = "Refresh";
        refreshButton.Width = 95;
        refreshButton.Height = 30;
        refreshButton.Location = new Point(280, 3);
        refreshButton.Click += async (_, _) => await RefreshAsync();
        filterPanel.Controls.AddRange([branchLabel, branchComboBox, refreshButton]);

        cardsPanel.Dock = DockStyle.Top;
        cardsPanel.Height = 92;
        cardsPanel.AutoScroll = true;
        cardsPanel.WrapContents = false;
        AddCard("Pending Shipments", nameof(DispatcherDashboardKpis.PendingShipments));
        AddCard("Scheduled Today", nameof(DispatcherDashboardKpis.TripsScheduledToday));
        AddCard("In Transit", nameof(DispatcherDashboardKpis.TripsInTransit));
        AddCard("Completed Today", nameof(DispatcherDashboardKpis.CompletedToday));
        AddCard("Vehicles Available", nameof(DispatcherDashboardKpis.VehiclesAvailable));
        AddCard("Drivers Active", nameof(DispatcherDashboardKpis.DriversActive));

        ConfigureGrid(dispatchGrid);
        ConfigureGrid(lateGrid);
        dispatchGrid.AutoGenerateColumns = false;
        dispatchGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Trip", DataPropertyName = nameof(DispatchRow.TripId), FillWeight = 12 });
        dispatchGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Vehicle", DataPropertyName = nameof(DispatchRow.Vehicle), FillWeight = 18 });
        dispatchGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Driver", DataPropertyName = nameof(DispatchRow.Driver), FillWeight = 18 });
        dispatchGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Departure", DataPropertyName = nameof(DispatchRow.Departure), FillWeight = 22 });
        dispatchGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Shipments", DataPropertyName = nameof(DispatchRow.Shipments), FillWeight = 12 });
        var dispatchColumn = new DataGridViewButtonColumn { HeaderText = "Action", Text = "Dispatch", UseColumnTextForButtonValue = true, FillWeight = 18 };
        dispatchGrid.Columns.Add(dispatchColumn);
        dispatchGrid.CellContentClick += DispatchGrid_CellContentClick;

        lateGrid.AutoGenerateColumns = false;
        lateGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Trip", DataPropertyName = nameof(LateRow.TripId), FillWeight = 15 });
        lateGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Vehicle", DataPropertyName = nameof(LateRow.Vehicle), FillWeight = 20 });
        lateGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Driver", DataPropertyName = nameof(LateRow.Driver), FillWeight = 20 });
        lateGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Planned Arrival", DataPropertyName = nameof(LateRow.PlannedArrival), FillWeight = 25 });
        lateGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Shipments", DataPropertyName = nameof(LateRow.Shipments), FillWeight = 20 });

        var lists = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(0, 8, 0, 0) };
        lists.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        lists.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        lists.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        lists.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        lists.Controls.Add(CreateSectionLabel("Trips needing dispatch"), 0, 0);
        lists.Controls.Add(dispatchGrid, 0, 1);
        lists.Controls.Add(CreateSectionLabel("Late trips"), 0, 2);
        lists.Controls.Add(lateGrid, 0, 3);

        Controls.Add(lists);
        Controls.Add(cardsPanel);
        Controls.Add(filterPanel);
        Controls.Add(title);
    }

    private void AddCard(string title, string key)
    {
        var card = new Panel { Width = 145, Height = 72, BackColor = Color.White, Margin = new Padding(0, 0, 8, 8), Padding = new Padding(10) };
        card.Controls.Add(new Label { Text = title, AutoSize = false, Width = 125, Height = 28, ForeColor = Color.DimGray });
        var value = new Label { Text = "-", AutoSize = false, Width = 125, Height = 30, Top = 30, Font = new Font(UiStyles.DefaultFont, FontStyle.Bold), ForeColor = UiStyles.Text };
        card.Controls.Add(value);
        cardsPanel.Controls.Add(card);
        values[key] = value;
    }

    private static Label CreateSectionLabel(string text) => new() { Text = text, Dock = DockStyle.Fill, Font = new Font(UiStyles.DefaultFont, FontStyle.Bold), ForeColor = UiStyles.Text, TextAlign = ContentAlignment.MiddleLeft };

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.RowHeadersVisible = false;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
    }

    private async void Dashboard_Load(object? sender, EventArgs e)
    {
        try
        {
            loading = true;
            var options = await BranchFilterService.GetOptionsAsync(Models.Module.Dashboard);
            branchComboBox.DataSource = options.ToList();
            branchComboBox.DisplayMember = nameof(BranchFilterOption.DisplayName);
            branchComboBox.ValueMember = nameof(BranchFilterOption.BranchId);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Dashboard filters could not be loaded.\n" + ex.Message, "Dashboard Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            loading = false;
        }
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (IsDisposed || !IsHandleCreated || branchComboBox.SelectedItem is not BranchFilterOption option) return;
        refreshButton.Enabled = false;
        try
        {
            var snapshot = await service.GetSnapshotAsync(option.BranchId);
            foreach (var property in typeof(DispatcherDashboardKpis).GetProperties())
            {
                values[property.Name].Text = property.GetValue(snapshot.Kpis)?.ToString() ?? "0";
            }
            dispatchGrid.DataSource = snapshot.TripsNeedingDispatch.Select(item => new DispatchRow(item)).ToList();
            lateGrid.DataSource = snapshot.LateTrips.Select(item => new LateRow(item)).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Dispatcher dashboard data could not be loaded.\n" + ex.Message, "Dashboard Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            refreshButton.Enabled = true;
        }
    }

    private void DispatchGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != dispatchGrid.Columns["Action"]!.Index || dispatchGrid.Rows[e.RowIndex].DataBoundItem is not DispatchRow row) return;
        using var form = new DispatchForm(row.TripId);
        if (form.ShowDialog(this) == DialogResult.OK) _ = RefreshAsync();
    }

    private sealed class DispatchRow(DispatchQueueItem item)
    {
        public int TripId { get; } = item.TripId;
        public string Vehicle { get; } = item.VehiclePlate;
        public string Driver { get; } = item.DriverName;
        public string Departure { get; } = item.PlannedDeparture.ToString("yyyy-MM-dd HH:mm");
        public int Shipments { get; } = item.ShipmentCount;
    }

    private sealed class LateRow(LateTripItem item)
    {
        public int TripId { get; } = item.TripId;
        public string Vehicle { get; } = item.VehiclePlate;
        public string Driver { get; } = item.DriverName;
        public string PlannedArrival { get; } = item.PlannedArrival.ToString("yyyy-MM-dd HH:mm");
        public int Shipments { get; } = item.ShipmentCount;
    }
}
