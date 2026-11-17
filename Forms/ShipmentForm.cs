using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Forms;

public sealed class ShipmentForm : Form
{
    private readonly ShipmentService service;
    private readonly ShipmentListItem? existing;
    private readonly ComboBox branchComboBox = new();
    private readonly TextBox referenceTextBox = new();
    private readonly TextBox descriptionTextBox = new();
    private readonly NumericUpDown weightInput = new();
    private readonly ComboBox cargoTypeComboBox = new();
    private readonly TextBox originTextBox = new();
    private readonly TextBox destinationTextBox = new();
    private readonly Button saveButton = new();
    private readonly Button cancelButton = new();

    public ShipmentForm(ShipmentService service, ShipmentListItem? existing = null)
    {
        this.service = service;
        this.existing = existing;
        InitializeForm();
        Load += ShipmentForm_Load;
    }

    private void InitializeForm()
    {
        Text = existing is null ? "Add Shipment" : "Edit Shipment";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(540, 410);
        Font = UiStyles.DefaultFont;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
            ColumnCount = 2,
            RowCount = 8
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 0; index < 8; index++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, index == 1 ? 64 : 42));
        }

        branchComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        referenceTextBox.MaxLength = 30;
        referenceTextBox.CharacterCasing = CharacterCasing.Upper;
        descriptionTextBox.MaxLength = 200;
        weightInput.DecimalPlaces = 2;
        weightInput.Maximum = 1_000_000;
        weightInput.Minimum = 0.01M;
        weightInput.Increment = 0.01M;
        EnumDisplay.FillComboBox<CargoType>(cargoTypeComboBox);
        cargoTypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        originTextBox.MaxLength = 150;
        destinationTextBox.MaxLength = 150;

        AddField(layout, 0, "Branch", branchComboBox);
        AddField(layout, 1, "Reference No", referenceTextBox);
        AddField(layout, 2, "Description", descriptionTextBox);
        AddField(layout, 3, "Weight (kg)", weightInput);
        AddField(layout, 4, "Cargo Type", cargoTypeComboBox);
        AddField(layout, 5, "Origin", originTextBox);
        AddField(layout, 6, "Destination", destinationTextBox);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        saveButton.Text = "Save";
        saveButton.Width = 100;
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
        layout.Controls.Add(buttonPanel, 0, 7);
        layout.SetColumnSpan(buttonPanel, 2);

        Controls.Add(layout);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private async void ShipmentForm_Load(object? sender, EventArgs e)
    {
        try
        {
            var branches = await BranchFilterService.GetOptionsAsync(Module.Shipments);
            branchComboBox.DataSource = branches
                .Where(option => option.BranchId.HasValue)
                .ToList();
            branchComboBox.DisplayMember = nameof(BranchFilterOption.DisplayName);
            branchComboBox.ValueMember = nameof(BranchFilterOption.BranchId);
            branchComboBox.Enabled = Session.IsHeadOffice;

            if (existing is null)
            {
                referenceTextBox.Text = await service.GetSuggestedReferenceAsync();
                weightInput.Value = 0.01M;
                if (!Session.IsHeadOffice && Session.CurrentUser!.BranchId.HasValue)
                {
                    branchComboBox.SelectedValue = Session.CurrentUser.BranchId.Value;
                }
            }
            else
            {
                branchComboBox.SelectedValue = existing.BranchId;
                referenceTextBox.Text = existing.ReferenceNo;
                descriptionTextBox.Text = existing.Description;
                weightInput.Value = existing.WeightKg;
                cargoTypeComboBox.SelectedValue = existing.CargoType;
                originTextBox.Text = existing.Origin;
                destinationTextBox.Text = existing.Destination;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Shipment options could not be loaded.\n" + ex.Message, "Shipment Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
        }
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
        if (branchComboBox.SelectedValue is not int branchId
            || cargoTypeComboBox.SelectedValue is not CargoType cargoType)
        {
            MessageBox.Show("Select a branch and cargo type.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        saveButton.Enabled = false;
        try
        {
            var request = new ShipmentRequest(
                branchId,
                referenceTextBox.Text,
                descriptionTextBox.Text,
                weightInput.Value,
                cargoType,
                originTextBox.Text,
                destinationTextBox.Text);
            var result = existing is null
                ? await service.CreateAsync(request)
                : await service.UpdateAsync(existing.ShipmentId, request);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage, "Shipment Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (UnauthorizedAccessException ex)
        {
            MessageBox.Show(ex.Message, "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show("The shipment could not be saved.\n" + ex.Message, "Shipment Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            saveButton.Enabled = true;
        }
    }
}
