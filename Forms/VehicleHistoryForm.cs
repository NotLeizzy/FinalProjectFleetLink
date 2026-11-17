using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Forms;

public sealed class VehicleHistoryForm : Form
{
    private readonly MaintenanceService service = new();
    private readonly int vehicleId;
    private readonly Label summaryLabel = new();
    private readonly DataGridView grid = new();

    public VehicleHistoryForm(int vehicleId)
    {
        this.vehicleId = vehicleId;
        Text = "Vehicle Maintenance History";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(920, 480);
        Font = UiStyles.DefaultFont;
        InitializeControls();
        Load += async (_, _) => await LoadHistoryAsync();
    }

    private void InitializeControls()
    {
        var title = new Label { Text = "Vehicle maintenance history", Dock = DockStyle.Top, Height = 36, Font = new Font(UiStyles.DefaultFont, FontStyle.Bold), ForeColor = UiStyles.Text };
        summaryLabel.Dock = DockStyle.Top;
        summaryLabel.Height = 62;
        summaryLabel.Padding = new Padding(0, 6, 0, 0);
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.AutoGenerateColumns = false;
        AddColumn(nameof(VehicleMaintenanceHistoryItem.MaintenanceType), "Type");
        AddColumn(nameof(VehicleMaintenanceHistoryItem.ScheduledDate), "Scheduled");
        AddColumn(nameof(VehicleMaintenanceHistoryItem.CompletedDate), "Completed");
        AddColumn(nameof(VehicleMaintenanceHistoryItem.OdometerAtService), "Odometer");
        AddColumn(nameof(VehicleMaintenanceHistoryItem.Cost), "Cost");
        AddColumn(nameof(VehicleMaintenanceHistoryItem.Notes), "Notes");
        AddColumn(nameof(VehicleMaintenanceHistoryItem.Status), "Status");
        Controls.Add(grid);
        Controls.Add(summaryLabel);
        Controls.Add(title);
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var history = await service.GetVehicleHistoryAsync(vehicleId);
            if (history is null)
            {
                summaryLabel.Text = "The vehicle was not found or is outside your branch scope.";
                return;
            }
            Text = $"Vehicle Maintenance History - {history.PlateNo}";
            summaryLabel.Text = $"Vehicle: {history.PlateNo} | Current odometer: {history.CurrentOdometer:N0} km | Services: {history.ServiceCount} | Total cost: {history.TotalCost:N2}\r\n"
                + $"Last completed: {history.LastCompletedServiceDate?.ToString("yyyy-MM-dd") ?? "Never"} | Next due date: {history.NextDueDate?.ToString("yyyy-MM-dd") ?? "None"} | Next due odometer: {(history.NextDueOdometer?.ToString("N0") ?? "None")}";
            grid.DataSource = history.Records.ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Vehicle history could not be loaded.\n" + ex.Message, "Maintenance", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddColumn(string property, string header) => grid.Columns.Add(new DataGridViewTextBoxColumn { Name = property, HeaderText = header, DataPropertyName = property });
}