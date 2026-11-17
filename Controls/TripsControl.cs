using FinalProjectFleetLink.Forms;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Controls;

public sealed class TripsControl : UserControl
{
    private readonly TripService service = new();
    private readonly TripStatusService statusService = new();
    private readonly LookupService lookupService = new();
    private readonly ComboBox statusComboBox = new();
    private readonly DateTimePicker fromPicker = new();
    private readonly DateTimePicker toPicker = new();
    private readonly ComboBox vehicleComboBox = new();
    private readonly ComboBox branchComboBox = new();
    private readonly CheckBox completedCancelledCheckBox = new();
    private readonly DataGridView grid = new();
    private readonly Button applyButton = new();
    private readonly Button newButton = new();
    private readonly Button editButton = new();
    private readonly Button cancelButton = new();
    private readonly Button manageShipmentsButton = new();
    private readonly Button dispatchButton = new();
    private readonly Button updateStatusButton = new();
    private readonly RichTextBox detailsBox = new();
    private bool loading;

    public TripsControl()
    {
        Dock = DockStyle.Fill; BackColor = UiStyles.ContentBackground; InitializeControls(); Load += TripsControl_Load;
    }

    private void InitializeControls()
    {
        var title = new Label { Text = "Trips", Dock = DockStyle.Top, Height = 36, Font = new Font(UiStyles.DefaultFont, FontStyle.Bold), ForeColor = UiStyles.Text };
        var filters = new Panel { Dock = DockStyle.Top, Height = 118 };
        AddLabel(filters, "Status", 0, 7); ConfigureEnumFilter(statusComboBox, 55, 2, CreateAll<TripStatus>()); filters.Controls.Add(statusComboBox);
        AddLabel(filters, "From", 215, 7); ConfigureDate(fromPicker, new Point(255, 2), DateTime.Today); filters.Controls.Add(fromPicker);
        AddLabel(filters, "To", 375, 7); ConfigureDate(toPicker, new Point(405, 2), DateTime.Today); filters.Controls.Add(toPicker);
        AddLabel(filters, "Vehicle", 530, 7); vehicleComboBox.DropDownStyle = ComboBoxStyle.DropDownList; vehicleComboBox.Width = 130; vehicleComboBox.Location = new Point(585, 2); filters.Controls.Add(vehicleComboBox);
        AddLabel(filters, "Branch", 735, 7); branchComboBox.DropDownStyle = ComboBoxStyle.DropDownList; branchComboBox.Width = 140; branchComboBox.Location = new Point(785, 2); filters.Controls.Add(branchComboBox);
        completedCancelledCheckBox.Text = "Show completed/cancelled"; completedCancelledCheckBox.AutoSize = true; completedCancelledCheckBox.Location = new Point(0, 45); filters.Controls.Add(completedCancelledCheckBox);
        ConfigureButton(applyButton, "Apply", 180, 41, 90); ConfigureButton(newButton, "New Trip", 0, 82, 95); ConfigureButton(editButton, "Edit", 105, 82, 90); ConfigureButton(cancelButton, "Cancel Trip", 200, 82, 105); ConfigureButton(manageShipmentsButton, "Manage Shipments", 315, 82, 135); ConfigureButton(dispatchButton, "Dispatch", 460, 82, 95); ConfigureButton(updateStatusButton, "Update Status", 565, 82, 110);
        filters.Controls.AddRange([applyButton, newButton, editButton, cancelButton, manageShipmentsButton, dispatchButton, updateStatusButton]);
        applyButton.Click += async (_, _) => await LoadTripsAsync(); newButton.Click += NewButton_Click; editButton.Click += EditButton_Click; cancelButton.Click += CancelButton_Click; manageShipmentsButton.Click += ManageShipmentsButton_Click; dispatchButton.Click += DispatchButton_Click; updateStatusButton.Click += UpdateStatusButton_Click;
        ConfigureGrid();
        ConfigureDetails();
        grid.SelectionChanged += async (_, _) => await LoadSelectedDetailsAsync();
        Controls.Add(grid); Controls.Add(detailsBox); Controls.Add(filters); Controls.Add(title);
    }

    private async void TripsControl_Load(object? sender, EventArgs e)
    {
        try
        {
            loading = true;
            var branches = await lookupService.GetActiveBranchesAsync(); branchComboBox.DataSource = branches.Select(item => new BranchOption(item.BranchId, item.BranchName)).Prepend(new BranchOption(null, "All branches")).ToList(); branchComboBox.DisplayMember = nameof(BranchOption.DisplayName); branchComboBox.ValueMember = nameof(BranchOption.BranchId);
            var vehicles = await lookupService.GetAvailableVehiclesAsync(Session.IsHeadOffice ? null : Session.CurrentUser!.BranchId); vehicleComboBox.DataSource = vehicles.Select(item => new VehicleOption(item.VehicleId, item.PlateNo)).Prepend(new VehicleOption(null, "All vehicles")).ToList(); vehicleComboBox.DisplayMember = nameof(VehicleOption.DisplayName); vehicleComboBox.ValueMember = nameof(VehicleOption.VehicleId);
            await LoadTripsAsync();
        }
        catch (Exception ex) { MessageBox.Show("Trip filters could not be loaded.\n" + ex.Message, "Trip Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { loading = false; }
    }

    private async Task LoadTripsAsync()
    {
        if (loading || branchComboBox.SelectedItem is not BranchOption branch || vehicleComboBox.SelectedItem is not VehicleOption vehicle) return;
        TripStatus? status = statusComboBox.SelectedItem is EnumOption<TripStatus> option
            && statusComboBox.SelectedIndex > 0
            ? option.Value
            : null;
        applyButton.Enabled = false;
        try { var rows = await service.GetTripsAsync(status, fromPicker.Value.Date, toPicker.Value.Date, vehicle.VehicleId, branch.BranchId, completedCancelledCheckBox.Checked); grid.DataSource = rows.Select(row => new TripDisplayRow(row)).ToList(); await LoadSelectedDetailsAsync(); }
        catch (Exception ex) { MessageBox.Show("Trips could not be loaded.\n" + ex.Message, "Trip Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { applyButton.Enabled = true; }
    }

    private async void UpdateStatusButton_Click(object? sender, EventArgs e)
    {
        if (grid.CurrentRow?.DataBoundItem is not TripDisplayRow row
            || (row.Status != TripStatus.Dispatched && row.Status != TripStatus.InTransit))
        {
            return;
        }
        using var form = new TripStatusForm(row.TripId, row.Status);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadTripsAsync();
        }
    }

    private async Task LoadSelectedDetailsAsync()
    {
        if (grid.CurrentRow?.DataBoundItem is not TripDisplayRow row)
        {
            detailsBox.Clear();
            updateStatusButton.Enabled = false;
            return;
        }
        try
        {
            var details = await statusService.GetDetailsAsync(row.TripId);
            if (details is null)
            {
                detailsBox.Text = "Trip details are unavailable.";
                updateStatusButton.Enabled = false;
                return;
            }
            detailsBox.Text = FormatDetails(details);
            updateStatusButton.Enabled = details.Status is TripStatus.Dispatched or TripStatus.InTransit;
        }
        catch (Exception ex)
        {
            detailsBox.Text = "Trip details could not be loaded.\r\n" + ex.Message;
            updateStatusButton.Enabled = false;
        }
    }

    private void ConfigureDetails()
    {
        detailsBox.Dock = DockStyle.Bottom;
        detailsBox.Height = 205;
        detailsBox.ReadOnly = true;
        detailsBox.BackColor = Color.White;
        detailsBox.BorderStyle = BorderStyle.FixedSingle;
        detailsBox.Font = UiStyles.DefaultFont;
        detailsBox.ScrollBars = RichTextBoxScrollBars.Vertical;
    }

    private static string FormatDetails(TripDetails details)
    {
        var text = $"Trip details - Trip {details.TripId}\r\n"
            + $"Status: {EnumDisplay.ToDisplayName(details.Status)}\r\n"
            + $"Planned departure: {details.PlannedDeparture:yyyy-MM-dd HH:mm}    Planned arrival: {details.PlannedArrival:yyyy-MM-dd HH:mm}\r\n"
            + $"Actual departure: {FormatDate(details.ActualDeparture)}    Actual arrival: {FormatDate(details.ActualArrival)}    Distance: {(details.DistanceKm.HasValue ? $"{details.DistanceKm:N2} km" : "Not recorded")}\r\n"
            + $"Vehicle: {details.VehiclePlate}    Driver: {details.DriverName}\r\n\r\nShipments ({details.Shipments.Count}):\r\n";
        text += details.Shipments.Count == 0
            ? "  None\r\n"
            : string.Join("\r\n", details.Shipments.Select(shipment => $"  {shipment.ReferenceNo} | {shipment.CargoType} | {shipment.WeightKg:N2} kg | {shipment.Status}")) + "\r\n";
        text += "\r\nDispatch:\r\n";
        text += details.DispatchUser is null
            ? "  No dispatch record\r\n"
            : $"  Dispatched by: {details.DispatchUser} at {details.DispatchedAt:yyyy-MM-dd HH:mm:ss}\r\n  Check summary: {details.CheckSummary ?? string.Empty}";
        return text;
    }

    private static string FormatDate(DateTime? value) => value?.ToString("yyyy-MM-dd HH:mm") ?? "Not recorded";

    private async void NewButton_Click(object? sender, EventArgs e) { using var form = new TripForm(service); if (form.ShowDialog(this) == DialogResult.OK) await LoadTripsAsync(); }
    private void ManageShipmentsButton_Click(object? sender, EventArgs e)
    {
        if (grid.CurrentRow?.DataBoundItem is not TripDisplayRow row)
        {
            return;
        }
        using var form = new TripShipmentsForm(row.TripId);
        form.ShowDialog(this);
    }
    private async void EditButton_Click(object? sender, EventArgs e) { if (grid.CurrentRow?.DataBoundItem is not TripDisplayRow row || row.Status != TripStatus.Scheduled) return; var item = await service.GetByIdAsync(row.TripId); if (item is null) return; using var form = new TripForm(service, item); if (form.ShowDialog(this) == DialogResult.OK) await LoadTripsAsync(); }
    private async void CancelButton_Click(object? sender, EventArgs e)
    {
        if (grid.CurrentRow?.DataBoundItem is not TripDisplayRow row || (row.Status != TripStatus.Scheduled && row.Status != TripStatus.Dispatched)) return;
        if (MessageBox.Show("Cancel the selected trip? Shipments will return to Pending.", "Confirm Cancellation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var result = await service.CancelAsync(row.TripId); if (!result.Success) MessageBox.Show(result.ErrorMessage, "Trip Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); else await LoadTripsAsync();
    }

    private async void DispatchButton_Click(object? sender, EventArgs e)
    {
        if (grid.CurrentRow?.DataBoundItem is not TripDisplayRow row || row.Status != TripStatus.Scheduled)
        {
            return;
        }
        using var form = new DispatchForm(row.TripId);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadTripsAsync();
        }
    }

    private void ConfigureGrid() { grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.MultiSelect = false; grid.RowHeadersVisible = false; grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None; }
    private static void AddLabel(Control parent, string text, int left, int top) => parent.Controls.Add(new Label { Text = text, AutoSize = true, Location = new Point(left, top) });
    private static void ConfigureEnumFilter<TEnum>(ComboBox combo, int left, int top, List<EnumOption<TEnum>> options) where TEnum : struct, Enum { combo.DropDownStyle = ComboBoxStyle.DropDownList; combo.Width = 130; combo.Location = new Point(left, top); combo.DataSource = options; combo.DisplayMember = nameof(EnumOption<TEnum>.DisplayName); }
    private static List<EnumOption<TEnum>> CreateAll<TEnum>() where TEnum : struct, Enum => [new(default, "All"), .. Enum.GetValues<TEnum>().Select(value => new EnumOption<TEnum>(value, EnumDisplay.ToDisplayName(value)))];
    private static void ConfigureDate(DateTimePicker picker, Point location, DateTime value) { picker.Format = DateTimePickerFormat.Short; picker.Width = 105; picker.Location = location; picker.Value = value; }
    private static void ConfigureButton(Button button, string text, int left, int top, int width) { button.Text = text; button.Width = width; button.Height = 30; button.Location = new Point(left, top); button.BackColor = UiStyles.SidebarActive; button.ForeColor = Color.White; button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0; }
    private sealed record BranchOption(int? BranchId, string DisplayName); private sealed record VehicleOption(int? VehicleId, string DisplayName);
    private sealed class TripDisplayRow { public int TripId { get; } public string Vehicle { get; } public string Driver { get; } public string Origin { get; } public string Destination { get; } public string Departure { get; } public string Arrival { get; } public string Shipments { get; } public string WeightCapacity { get; } public TripStatus Status { get; } public string OriginBranch { get; } public TripDisplayRow(TripListItem row) { TripId = row.TripId; Vehicle = row.VehiclePlate; Driver = row.DriverName; Origin = row.Origin; Destination = row.Destination; Departure = row.PlannedDeparture.ToString("yyyy-MM-dd HH:mm"); Arrival = row.PlannedArrival.ToString("yyyy-MM-dd HH:mm"); Shipments = row.ShipmentCount.ToString(); WeightCapacity = $"{row.TotalWeightKg:N0} / {row.VehicleCapacityKg:N0} kg"; Status = row.Status; OriginBranch = row.OriginBranch; } }
}
