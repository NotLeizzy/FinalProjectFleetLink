using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Controls;

public sealed class FuelMonitorControl : UserControl
{
    private readonly FuelMonitorService service = new();
    private readonly ComboBox branchComboBox = new();
    private readonly ComboBox vehicleComboBox = new();
    private readonly DateTimePicker fromPicker = new();
    private readonly DateTimePicker toPicker = new();
    private readonly CheckBox abnormalOnlyCheckBox = new();
    private readonly Button applyButton = new();
    private readonly DataGridView grid = new();
    private readonly Label summaryLabel = new();
    private bool loading;

    public FuelMonitorControl()
    {
        Dock = DockStyle.Fill;
        BackColor = UiStyles.ContentBackground;
        InitializeControls();
        Load += FuelMonitorControl_Load;
    }

    private void InitializeControls()
    {
        var title = new Label { Text = "Fuel Monitor", Dock = DockStyle.Top, Height = 36, Font = new Font(UiStyles.DefaultFont, FontStyle.Bold), ForeColor = UiStyles.Text };
        var filters = new Panel { Dock = DockStyle.Top, Height = 82 };
        AddLabel(filters, "Branch", 0, 6);
        branchComboBox.DropDownStyle = ComboBoxStyle.DropDownList; branchComboBox.Width = 150; branchComboBox.Location = new Point(50, 2);
        branchComboBox.SelectedIndexChanged += async (_, _) => { if (!loading) await LoadVehiclesAndDataAsync(); };
        filters.Controls.Add(branchComboBox);
        AddLabel(filters, "Vehicle", 215, 6);
        vehicleComboBox.DropDownStyle = ComboBoxStyle.DropDownList; vehicleComboBox.Width = 130; vehicleComboBox.Location = new Point(270, 2);
        filters.Controls.Add(vehicleComboBox);
        AddLabel(filters, "From", 415, 6); ConfigureDatePicker(fromPicker, new Point(455, 2)); filters.Controls.Add(fromPicker);
        AddLabel(filters, "To", 570, 6); ConfigureDatePicker(toPicker, new Point(595, 2)); filters.Controls.Add(toPicker);
        abnormalOnlyCheckBox.Text = "Abnormal only"; abnormalOnlyCheckBox.AutoSize = true; abnormalOnlyCheckBox.Location = new Point(730, 6); filters.Controls.Add(abnormalOnlyCheckBox);
        applyButton.Text = "Apply"; applyButton.Width = 85; applyButton.Height = 30; applyButton.Location = new Point(0, 43); applyButton.Click += async (_, _) => await LoadDataAsync(); filters.Controls.Add(applyButton);
        summaryLabel.Dock = DockStyle.Bottom; summaryLabel.Height = 34; summaryLabel.Padding = new Padding(5); summaryLabel.BackColor = Color.White;
        ConfigureGrid();
        Controls.Add(grid); Controls.Add(summaryLabel); Controls.Add(filters); Controls.Add(title);
    }

    private static void AddLabel(Control parent, string text, int left, int top) => parent.Controls.Add(new Label { Text = text, AutoSize = true, Location = new Point(left, top) });
    private static void ConfigureDatePicker(DateTimePicker picker, Point location) { picker.Format = DateTimePickerFormat.Short; picker.Width = 105; picker.Location = location; picker.Value = DateTime.Today.AddDays(-30); }

    private void ConfigureGrid()
    {
        grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AllowUserToResizeRows = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.MultiSelect = false; grid.RowHeadersVisible = false; grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None;
        grid.CellFormatting += Grid_CellFormatting;
    }

    private async void FuelMonitorControl_Load(object? sender, EventArgs e)
    {
        try
        {
            loading = true;
            var branches = await BranchFilterService.GetOptionsAsync(Module.FuelMonitor);
            branchComboBox.DataSource = branches.ToList(); branchComboBox.DisplayMember = nameof(BranchFilterOption.DisplayName); branchComboBox.ValueMember = nameof(BranchFilterOption.BranchId);
            await LoadVehiclesAndDataAsync();
        }
        catch (Exception ex) { MessageBox.Show("Fuel monitor filters could not be loaded.\n" + ex.Message, "Fuel Monitor Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { loading = false; }
    }

    private async Task LoadVehiclesAndDataAsync()
    {
        var options = await service.GetVehicleOptionsAsync(GetBranchId());
        var vehicleOptions = options.ToList();
        vehicleOptions.Insert(0, new FuelMonitorVehicleOption(0, "All vehicles"));
        vehicleComboBox.DataSource = vehicleOptions;
        vehicleComboBox.DisplayMember = nameof(FuelMonitorVehicleOption.PlateNo); vehicleComboBox.ValueMember = nameof(FuelMonitorVehicleOption.VehicleId);
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        if (vehicleComboBox.SelectedItem is not FuelMonitorVehicleOption vehicle) return;
        applyButton.Enabled = false;
        try
        {
            var result = await service.GetAsync(GetBranchId(), vehicle.VehicleId == 0 ? null : vehicle.VehicleId, fromPicker.Value.Date, toPicker.Value.Date, abnormalOnlyCheckBox.Checked);
            grid.DataSource = result.Rows.Select(row => new FuelMonitorDisplayRow(row)).ToList();
            summaryLabel.Text = $"Total liters: {result.Summary.TotalLiters:0.##}    Total cost: {result.Summary.TotalCost:0.##}    Average Km/L: {result.Summary.AverageKmPerLiter:0.##}";
        }
        catch (Exception ex) { MessageBox.Show("Fuel data could not be loaded.\n" + ex.Message, "Fuel Monitor Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { applyButton.Enabled = true; }
    }

    private int? GetBranchId() => branchComboBox.SelectedItem is BranchFilterOption option ? option.BranchId : null;

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].DataBoundItem is FuelMonitorDisplayRow row && row.Abnormal == "Yes")
        {
            e.CellStyle.BackColor = Color.MistyRose;
        }
    }

    private sealed class FuelMonitorDisplayRow
    {
        public string Date { get; }
        public string PlateNo { get; }
        public string Trip { get; }
        public string Liters { get; }
        public string TotalCost { get; }
        public string Odometer { get; }
        public string KmPerLiter { get; }
        public string ExpectedKmPerLiter { get; }
        public string Abnormal { get; }
        public FuelMonitorDisplayRow(FuelMonitorRow row)
        {
            Date = row.FuelDate.ToString("yyyy-MM-dd HH:mm"); PlateNo = row.PlateNo; Trip = row.TripId?.ToString() ?? "-"; Liters = row.Liters.ToString("0.##"); TotalCost = row.TotalCost.ToString("0.##"); Odometer = row.Odometer.ToString(); KmPerLiter = row.KmPerLiter?.ToString("0.##") ?? "-"; ExpectedKmPerLiter = row.ExpectedKmPerLiter.ToString("0.##"); Abnormal = row.IsAbnormal ? "Yes" : "No";
        }
    }
}
