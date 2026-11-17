using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Forms;

public sealed class TripForm : Form
{
    private readonly TripService service;
    private readonly LookupService lookupService = new();
    private readonly TripListItem? existing;
    private readonly ComboBox originBranchComboBox = new();
    private readonly ComboBox vehicleComboBox = new();
    private readonly ComboBox driverComboBox = new();
    private readonly TextBox originTextBox = new();
    private readonly TextBox destinationTextBox = new();
    private readonly ComboBox destinationBranchComboBox = new();
    private readonly DateTimePicker departurePicker = new();
    private readonly DateTimePicker arrivalPicker = new();
    private readonly NumericUpDown distanceInput = new();
    private readonly Button saveButton = new();
    private readonly Button cancelButton = new();
    private IReadOnlyList<VehicleLookupItem> vehicles = [];
    private IReadOnlyList<DriverLookupItem> drivers = [];
    private bool loadingVehicle;

    public TripForm(TripService service, TripListItem? existing = null)
    {
        this.service = service;
        this.existing = existing;
        InitializeForm();
        Load += TripForm_Load;
    }

    private void InitializeForm()
    {
        Text = existing is null ? "New Trip" : "Edit Trip";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(600, 510);
        Font = UiStyles.DefaultFont;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 2, RowCount = 9 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 9; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

        ConfigureCombo(originBranchComboBox); ConfigureCombo(vehicleComboBox); ConfigureCombo(driverComboBox); ConfigureCombo(destinationBranchComboBox);
        originBranchComboBox.SelectedIndexChanged += async (_, _) => await LoadBranchResourcesAsync();
        vehicleComboBox.SelectedIndexChanged += async (_, _) => await VehicleChangedAsync();
        ConfigureDatePicker(departurePicker, DateTime.Now.AddMinutes(5));
        ConfigureDatePicker(arrivalPicker, DateTime.Now.AddHours(1));
        distanceInput.DecimalPlaces = 2; distanceInput.Maximum = 1_000_000; distanceInput.Increment = 0.1M;
        originTextBox.MaxLength = 150; destinationTextBox.MaxLength = 150;

        AddField(layout, 0, "Origin Branch", originBranchComboBox);
        AddField(layout, 1, "Vehicle", vehicleComboBox);
        AddField(layout, 2, "Driver", driverComboBox);
        AddField(layout, 3, "Origin", originTextBox);
        AddField(layout, 4, "Destination", destinationTextBox);
        AddField(layout, 5, "Destination Branch", destinationBranchComboBox);
        AddField(layout, 6, "Planned Departure", departurePicker);
        AddField(layout, 7, "Planned Arrival", arrivalPicker);
        AddField(layout, 8, "Distance (km)", distanceInput);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        saveButton.Text = "Save"; saveButton.Width = 100; saveButton.Height = 34; saveButton.BackColor = UiStyles.SidebarActive; saveButton.ForeColor = Color.White; saveButton.FlatStyle = FlatStyle.Flat; saveButton.FlatAppearance.BorderSize = 0; saveButton.Click += SaveButton_Click;
        cancelButton.Text = "Cancel"; cancelButton.Width = 100; cancelButton.Height = 34; cancelButton.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(saveButton); buttons.Controls.Add(cancelButton);
        layout.Controls.Add(buttons, 0, 9); layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout); AcceptButton = saveButton; CancelButton = cancelButton;
    }

    private async void TripForm_Load(object? sender, EventArgs e)
    {
        try
        {
            var branchOptions = await lookupService.GetActiveBranchesAsync();
            originBranchComboBox.DataSource = branchOptions.ToList(); originBranchComboBox.DisplayMember = nameof(BranchLookupItem.BranchName); originBranchComboBox.ValueMember = nameof(BranchLookupItem.BranchId); originBranchComboBox.Enabled = Session.IsHeadOffice;
            destinationBranchComboBox.DataSource = new List<BranchLookupOption> { new(null, "None / outside the branch network") }.Concat(branchOptions.Select(item => new BranchLookupOption(item.BranchId, item.BranchName))).ToList();
            destinationBranchComboBox.DisplayMember = nameof(BranchLookupOption.DisplayName); destinationBranchComboBox.ValueMember = nameof(BranchLookupOption.BranchId);
            if (existing is null)
            {
                if (!Session.IsHeadOffice) originBranchComboBox.SelectedValue = Session.CurrentUser!.BranchId!.Value;
                else if (originBranchComboBox.Items.Count > 0) originBranchComboBox.SelectedIndex = 0;
                await LoadBranchResourcesAsync();
            }
            else
            {
                originBranchComboBox.SelectedValue = GetExistingBranchId();
                originTextBox.Text = existing.Origin; destinationTextBox.Text = existing.Destination;
                departurePicker.Value = existing.PlannedDeparture; arrivalPicker.Value = existing.PlannedArrival;
                distanceInput.Value = 0;
                await LoadBranchResourcesAsync();
                vehicleComboBox.SelectedItem = vehicles.FirstOrDefault(item => item.PlateNo == existing.VehiclePlate);
                driverComboBox.SelectedItem = drivers.FirstOrDefault(item => item.FullName == existing.DriverName);
            }
        }
        catch (Exception ex) { MessageBox.Show("Trip options could not be loaded.\n" + ex.Message, "Trip Error", MessageBoxButtons.OK, MessageBoxIcon.Error); DialogResult = DialogResult.Cancel; }
    }

    private int GetExistingBranchId() => existing is not null && originBranchComboBox.DataSource is IEnumerable<BranchLookupItem> branches
        ? branches.FirstOrDefault(item => item.BranchName == existing.OriginBranch)?.BranchId ?? Session.CurrentUser!.BranchId ?? 0
        : Session.CurrentUser!.BranchId ?? 0;

    private async Task LoadBranchResourcesAsync()
    {
        if (originBranchComboBox.SelectedValue is not int branchId) return;
        loadingVehicle = true;
        vehicles = await lookupService.GetAvailableVehiclesAsync(branchId);
        drivers = await lookupService.GetActiveDriversAsync(branchId);
        vehicleComboBox.DataSource = vehicles.ToList(); vehicleComboBox.DisplayMember = nameof(VehicleLookupItem.PlateNo); vehicleComboBox.ValueMember = nameof(VehicleLookupItem.VehicleId);
        driverComboBox.DataSource = drivers.ToList(); driverComboBox.DisplayMember = nameof(DriverLookupItem.FullName); driverComboBox.ValueMember = nameof(DriverLookupItem.DriverId);
        loadingVehicle = false;
        await VehicleChangedAsync();
    }

    private async Task VehicleChangedAsync()
    {
        if (loadingVehicle || vehicleComboBox.SelectedItem is not VehicleLookupItem vehicle) return;
        var current = await lookupService.GetCurrentDriverForVehicleAsync(vehicle.VehicleId);
        if (current is not null) driverComboBox.SelectedValue = current.DriverId;
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        if (originBranchComboBox.SelectedValue is not int branchId || vehicleComboBox.SelectedValue is not int vehicleId || driverComboBox.SelectedValue is not int driverId) { MessageBox.Show("Select an origin branch, vehicle, and driver.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        saveButton.Enabled = false;
        try
        {
            var destinationBranchId = destinationBranchComboBox.SelectedValue is int selectedBranch ? selectedBranch : (int?)null;
            var request = new TripRequest(branchId, vehicleId, driverId, originTextBox.Text, destinationTextBox.Text, destinationBranchId, departurePicker.Value, arrivalPicker.Value, distanceInput.Value == 0 ? null : distanceInput.Value);
            var result = existing is null ? await service.CreateAsync(request) : await service.UpdateAsync(existing.TripId, request);
            if (result.Warning && result.Success && MessageBox.Show("The vehicle is not Available now. Continue?", "Vehicle Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            if (!result.Success) { MessageBox.Show(result.ErrorMessage, "Trip Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            DialogResult = DialogResult.OK;
        }
        catch (ArgumentException ex) { MessageBox.Show(ex.Message, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        catch (Exception ex) { MessageBox.Show("The trip could not be saved.\n" + ex.Message, "Trip Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { saveButton.Enabled = true; }
    }

    private static void ConfigureCombo(ComboBox combo) { combo.DropDownStyle = ComboBoxStyle.DropDownList; combo.Dock = DockStyle.Fill; }
    private static void ConfigureDatePicker(DateTimePicker picker, DateTime value) { picker.Format = DateTimePickerFormat.Custom; picker.CustomFormat = "yyyy-MM-dd HH:mm"; picker.Value = value; picker.Dock = DockStyle.Fill; }
    private static void AddField(TableLayoutPanel layout, int row, string label, Control control) { layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 0, 0) }, 0, row); layout.Controls.Add(control, 1, row); }
    private sealed record BranchLookupOption(int? BranchId, string DisplayName);
}
