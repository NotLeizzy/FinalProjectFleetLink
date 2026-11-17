using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Services;
using FinalProjectFleetLink.Models;

namespace FinalProjectFleetLink.Forms;

public sealed class AssignmentForm : Form
{
    private readonly AssignmentService service;
    private readonly bool reassign;
    private readonly ComboBox vehicleComboBox = new();
    private readonly ComboBox driverComboBox = new();
    private readonly DateTimePicker startDatePicker = new();
    private readonly Label vehicleInfoLabel = new();
    private readonly Label driverInfoLabel = new();
    private readonly Button saveButton = new();
    private readonly Button cancelButton = new();
    private readonly int? initialVehicleId;
    private IReadOnlyList<VehicleAssignmentOption> vehicleOptions = [];
    private IReadOnlyList<DriverAssignmentOption> driverOptions = [];

    public AssignmentForm(AssignmentService service, bool reassign, int? vehicleId = null)
    {
        this.service = service;
        this.reassign = reassign;
        initialVehicleId = vehicleId;
        InitializeForm();
        Load += AssignmentForm_Load;
    }

    private void InitializeForm()
    {
        Text = reassign ? "Reassign Driver" : "Assign Driver";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(540, 300);
        Font = UiStyles.DefaultFont;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
            ColumnCount = 2,
            RowCount = 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 0; index < 5; index++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, index == 3 ? 55 : 44));
        }

        vehicleComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        vehicleComboBox.SelectedIndexChanged += VehicleComboBox_SelectedIndexChanged;
        driverComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        startDatePicker.Format = DateTimePickerFormat.Short;
        startDatePicker.Value = DateTime.Today;

        AddField(layout, 0, "Vehicle", vehicleComboBox);
        AddField(layout, 1, "Driver", driverComboBox);
        AddField(layout, 2, "Start Date", startDatePicker);
        vehicleInfoLabel.AutoSize = true;
        vehicleInfoLabel.ForeColor = Color.DarkOrange;
        driverInfoLabel.AutoSize = true;
        driverInfoLabel.ForeColor = Color.DarkOrange;
        layout.Controls.Add(vehicleInfoLabel, 1, 3);
        layout.Controls.Add(driverInfoLabel, 1, 4);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 45,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        saveButton.Text = reassign ? "Reassign" : "Assign";
        saveButton.Width = 105;
        saveButton.Height = 34;
        saveButton.BackColor = UiStyles.SidebarActive;
        saveButton.ForeColor = Color.White;
        saveButton.FlatStyle = FlatStyle.Flat;
        saveButton.FlatAppearance.BorderSize = 0;
        saveButton.Click += SaveButton_Click;
        cancelButton.Text = "Cancel";
        cancelButton.Width = 100;
        cancelButton.Height = 34;
        cancelButton.DialogResult = DialogResult.Cancel;
        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(cancelButton);
        layout.Controls.Add(buttonPanel, 0, 5);
        layout.SetColumnSpan(buttonPanel, 2);

        Controls.Add(layout);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private async void AssignmentForm_Load(object? sender, EventArgs e)
    {
        try
        {
            var branchId = Session.CurrentUser!.IsHeadOffice ? null : Session.CurrentUser.BranchId;
            vehicleOptions = await service.GetVehicleOptionsAsync(branchId);
            var visibleVehicles = vehicleOptions
                .Where(option => !reassign || option.VehicleId == initialVehicleId)
                .Select(option => reassign && option.VehicleId == initialVehicleId
                    ? option with { IsAvailable = true, UnavailableReason = null }
                    : option)
                .ToList();
            vehicleComboBox.DataSource = visibleVehicles;
            vehicleComboBox.DisplayMember = nameof(VehicleAssignmentOption.PlateNo);
            vehicleComboBox.ValueMember = nameof(VehicleAssignmentOption.VehicleId);
            await RefreshDriversAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Assignment options could not be loaded.\n" + ex.Message, "Assignment Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
        }
    }

    private async void VehicleComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        await RefreshDriversAsync();
    }

    private async Task RefreshDriversAsync()
    {
        if (vehicleComboBox.SelectedItem is not VehicleAssignmentOption vehicle)
        {
            driverComboBox.DataSource = null;
            return;
        }

        driverOptions = await service.GetDriverOptionsAsync(vehicle.BranchId);
        driverComboBox.DataSource = driverOptions.ToList();
        driverComboBox.DisplayMember = nameof(DriverAssignmentOption.FullName);
        driverComboBox.ValueMember = nameof(DriverAssignmentOption.DriverId);
        vehicleInfoLabel.Text = vehicle.IsAvailable
            ? $"Branch: {vehicle.BranchName}"
            : $"Unavailable: {vehicle.UnavailableReason}";
        driverInfoLabel.Text = string.Empty;
    }

    private static void AddField(TableLayoutPanel layout, int row, string labelText, Control control)
    {
        layout.Controls.Add(new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 0, 0)
        }, 0, row);
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(control, 1, row);
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        if (vehicleComboBox.SelectedItem is not VehicleAssignmentOption vehicle
            || driverComboBox.SelectedItem is not DriverAssignmentOption driver)
        {
            MessageBox.Show("Select a vehicle and a driver.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!vehicle.IsAvailable || !driver.IsAvailable)
        {
            MessageBox.Show("The selected vehicle or driver is unavailable.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        saveButton.Enabled = false;
        try
        {
            var request = new AssignmentRequest(vehicle.VehicleId, driver.DriverId, DateOnly.FromDateTime(startDatePicker.Value));
            var result = reassign
                ? await service.ReassignAsync(request, false)
                : await service.AssignAsync(request, false);
            if (result.RequiresConfirmation)
            {
                var warningText = string.Join(Environment.NewLine, result.Warnings ?? []);
                var confirm = MessageBox.Show(
                    warningText + Environment.NewLine + Environment.NewLine + "Continue anyway?",
                    "Expiry Warning",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes)
                {
                    return;
                }
                result = reassign
                    ? await service.ReassignAsync(request, true)
                    : await service.AssignAsync(request, true);
            }
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage ?? "The assignment could not be saved.", "Assignment Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Assignment Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show("The assignment could not be saved.\n" + ex.Message, "Assignment Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            saveButton.Enabled = true;
        }
    }
}
