using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Forms;

public sealed class MaintenanceForm : Form
{
    private readonly MaintenanceService service = new();
    private readonly MaintenanceListItem? existing;
    private readonly MaintenanceRequest? prefill;
    private readonly ComboBox vehicleComboBox = new();
    private readonly ComboBox typeComboBox = new();
    private readonly DateTimePicker scheduledDatePicker = new();
    private readonly NumericUpDown dueOdometerInput = new();
    private readonly TextBox notesTextBox = new();
    private readonly Label vehicleInfoLabel = new();
    private readonly Button saveButton = new();
    private IReadOnlyList<MaintenanceVehicleOption> vehicles = [];

    public MaintenanceForm(MaintenanceListItem? existing = null, MaintenanceRequest? prefill = null)
    {
        this.existing = existing;
        this.prefill = prefill;
        Text = existing is null ? "Schedule Maintenance" : "Edit Maintenance";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 390);
        Font = UiStyles.DefaultFont;
        InitializeControls(this.prefill);
        Load += async (_, _) => await LoadVehiclesAsync();
    }

    private void InitializeControls(MaintenanceRequest? prefill)
    {
        var title = new Label { Text = Text, Dock = DockStyle.Top, Height = 38, Font = new Font(UiStyles.DefaultFont, FontStyle.Bold), ForeColor = UiStyles.Text };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(12), AutoSize = false };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(layout, 0, "Vehicle", vehicleComboBox);
        vehicleComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        vehicleComboBox.SelectedIndexChanged += (_, _) => UpdateVehicleInfo();
        AddRow(layout, 1, "Type", typeComboBox);
        typeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        typeComboBox.DataSource = Enum.GetValues<MaintenanceType>().Select(value => new EnumOption<MaintenanceType>(value, EnumDisplay.ToDisplayName(value))).ToList();
        typeComboBox.DisplayMember = nameof(EnumOption<MaintenanceType>.DisplayName);
        AddRow(layout, 2, "Scheduled date", scheduledDatePicker);
        scheduledDatePicker.Format = DateTimePickerFormat.Short;
        scheduledDatePicker.Value = prefill?.ScheduledDate.ToDateTime(TimeOnly.MinValue)
            ?? (existing is null ? DateTime.Today : existing.ScheduledDate.ToDateTime(TimeOnly.MinValue));
        AddRow(layout, 3, "Due odometer (optional)", dueOdometerInput);
        dueOdometerInput.Maximum = 999999999;
        dueOdometerInput.Minimum = 0;
        dueOdometerInput.Value = prefill?.DueOdometer ?? existing?.DueOdometer ?? 0;
        AddRow(layout, 4, "Vehicle information", vehicleInfoLabel);
        vehicleInfoLabel.AutoSize = true;
        vehicleInfoLabel.ForeColor = Color.DimGray;
        AddRow(layout, 5, "Notes", notesTextBox);
        notesTextBox.Multiline = true;
        notesTextBox.ScrollBars = ScrollBars.Vertical;
        notesTextBox.MaxLength = 500;
        notesTextBox.Height = 75;
        notesTextBox.Text = prefill?.Notes ?? existing?.Notes ?? string.Empty;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        ConfigureButton(saveButton, "Save");
        var cancelButton = new Button { Text = "Cancel", Width = 100, Height = 34, DialogResult = DialogResult.Cancel };
        saveButton.Click += SaveButton_Click;
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(saveButton);
        layout.Controls.Add(buttons, 1, 6);
        Controls.Add(layout);
        Controls.Add(title);
        CancelButton = cancelButton;
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, row == 5 ? 90 : 42));
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 7, 0, 0) }, 0, row);
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(control, 1, row);
    }

    private async Task LoadVehiclesAsync()
    {
        try
        {
            vehicles = await service.GetVehicleOptionsAsync();
            vehicleComboBox.DataSource = vehicles.ToList();
            vehicleComboBox.DisplayMember = nameof(MaintenanceVehicleOption.PlateNo);
            vehicleComboBox.ValueMember = nameof(MaintenanceVehicleOption.VehicleId);
            if (existing is not null)
            {
                vehicleComboBox.SelectedValue = existing.VehicleId;
                typeComboBox.SelectedValue = existing.MaintenanceType;
            }
            else if (this.prefill is not null)
            {
                vehicleComboBox.SelectedValue = this.prefill.VehicleId;
                typeComboBox.SelectedValue = this.prefill.MaintenanceType;
            }
            UpdateVehicleInfo();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Maintenance vehicles could not be loaded.\n" + ex.Message, "Maintenance", MessageBoxButtons.OK, MessageBoxIcon.Error);
            saveButton.Enabled = false;
        }
    }

    private void UpdateVehicleInfo()
    {
        if (vehicleComboBox.SelectedItem is not MaintenanceVehicleOption vehicle)
        {
            vehicleInfoLabel.Text = string.Empty;
            return;
        }
        vehicleInfoLabel.Text = $"Current odometer: {vehicle.CurrentOdometer:N0} km; Last completed service: {(vehicle.LastCompletedServiceDate?.ToString("yyyy-MM-dd") ?? "Never")}";
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        if (vehicleComboBox.SelectedItem is not MaintenanceVehicleOption vehicle
            || typeComboBox.SelectedItem is not EnumOption<MaintenanceType> type)
        {
            MessageBox.Show("Vehicle and maintenance type are required.", "Maintenance", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var request = new MaintenanceRequest(
            vehicle.VehicleId,
            type.Value,
            DateOnly.FromDateTime(scheduledDatePicker.Value.Date),
            dueOdometerInput.Value == 0 ? null : decimal.ToInt32(dueOdometerInput.Value),
            notesTextBox.Text);
        try
        {
            var result = existing is null
                ? await service.ScheduleAsync(request)
                : await service.EditAsync(existing.MaintenanceId, request);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage, "Maintenance", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "Maintenance", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Maintenance could not be saved.\n" + ex.Message, "Maintenance", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void ConfigureButton(Button button, string text)
    {
        button.Text = text;
        button.Width = 100;
        button.Height = 34;
        button.BackColor = UiStyles.SidebarActive;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
    }
}
