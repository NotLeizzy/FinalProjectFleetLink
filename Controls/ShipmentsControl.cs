using FinalProjectFleetLink.Forms;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Controls;

public sealed class ShipmentsControl : UserControl
{
    private readonly ShipmentService service = new();
    private readonly TextBox searchTextBox = new();
    private readonly ComboBox statusComboBox = new();
    private readonly ComboBox cargoTypeComboBox = new();
    private readonly ComboBox branchComboBox = new();
    private readonly DateTimePicker fromPicker = new();
    private readonly DateTimePicker toPicker = new();
    private readonly DataGridView grid = new();
    private readonly Button addButton = new();
    private readonly Button editButton = new();
    private readonly Button cancelShipmentButton = new();
    private readonly Button applyButton = new();
    private bool loading;

    public ShipmentsControl()
    {
        Dock = DockStyle.Fill;
        BackColor = UiStyles.ContentBackground;
        InitializeControls();
        Load += ShipmentsControl_Load;
    }

    private void InitializeControls()
    {
        var title = new Label
        {
            Text = "Shipments",
            Dock = DockStyle.Top,
            Height = 36,
            Font = new Font(UiStyles.DefaultFont, FontStyle.Bold),
            ForeColor = UiStyles.Text
        };
        var filters = new Panel { Dock = DockStyle.Top, Height = 118 };
        AddLabel(filters, "Search", 0, 7);
        searchTextBox.Width = 180;
        searchTextBox.Location = new Point(55, 2);
        filters.Controls.Add(searchTextBox);
        AddLabel(filters, "Status", 250, 7);
        statusComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        statusComboBox.Width = 130;
        statusComboBox.Location = new Point(300, 2);
        statusComboBox.DataSource = CreateAllOptionList<ShipmentStatus>();
        statusComboBox.DisplayMember = nameof(EnumFilterOption<ShipmentStatus>.DisplayName);
        filters.Controls.Add(statusComboBox);
        AddLabel(filters, "Cargo", 455, 7);
        cargoTypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        cargoTypeComboBox.Width = 130;
        cargoTypeComboBox.Location = new Point(500, 2);
        cargoTypeComboBox.DataSource = CreateAllOptionList<CargoType>();
        cargoTypeComboBox.DisplayMember = nameof(EnumFilterOption<CargoType>.DisplayName);
        filters.Controls.Add(cargoTypeComboBox);
        AddLabel(filters, "Branch", 655, 7);
        branchComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        branchComboBox.Width = 150;
        branchComboBox.Location = new Point(705, 2);
        filters.Controls.Add(branchComboBox);
        AddLabel(filters, "Created from", 0, 48);
        ConfigureDatePicker(fromPicker, new Point(90, 43), DateTime.Today.AddMonths(-1));
        filters.Controls.Add(fromPicker);
        AddLabel(filters, "to", 220, 48);
        ConfigureDatePicker(toPicker, new Point(245, 43), DateTime.Today);
        filters.Controls.Add(toPicker);
        ConfigureButton(applyButton, "Apply", 375, 42, 90);
        filters.Controls.Add(applyButton);
        ConfigureButton(addButton, "Add", 0, 82, 90);
        ConfigureButton(editButton, "Edit", 100, 82, 90);
        ConfigureButton(cancelShipmentButton, "Cancel Shipment", 200, 82, 135);
        filters.Controls.AddRange([addButton, editButton, cancelShipmentButton]);
        applyButton.Click += async (_, _) => await LoadShipmentsAsync();
        addButton.Click += AddButton_Click;
        editButton.Click += EditButton_Click;
        cancelShipmentButton.Click += CancelShipmentButton_Click;

        ConfigureGrid();
        Controls.Add(grid);
        Controls.Add(filters);
        Controls.Add(title);
    }

    private static void AddLabel(Control parent, string text, int left, int top) => parent.Controls.Add(new Label { Text = text, AutoSize = true, Location = new Point(left, top) });

    private static void ConfigureDatePicker(DateTimePicker picker, Point location, DateTime value)
    {
        picker.Format = DateTimePickerFormat.Short;
        picker.Width = 105;
        picker.Location = location;
        picker.Value = value;
    }

    private static void ConfigureButton(Button button, string text, int left, int top, int width)
    {
        button.Text = text;
        button.Width = width;
        button.Height = 30;
        button.Location = new Point(left, top);
        button.BackColor = UiStyles.SidebarActive;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
    }

    private void ConfigureGrid()
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
        grid.CellFormatting += Grid_CellFormatting;
    }

    private async void ShipmentsControl_Load(object? sender, EventArgs e)
    {
        try
        {
            loading = true;
            var branches = await BranchFilterService.GetOptionsAsync(Module.Shipments);
            branchComboBox.DataSource = branches.ToList();
            branchComboBox.DisplayMember = nameof(BranchFilterOption.DisplayName);
            branchComboBox.ValueMember = nameof(BranchFilterOption.BranchId);
            await LoadShipmentsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Shipment filters could not be loaded.\n" + ex.Message, "Shipment Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            loading = false;
        }
    }

    private async Task LoadShipmentsAsync()
    {
        if (loading || branchComboBox.SelectedItem is not BranchFilterOption branch)
        {
            return;
        }
        applyButton.Enabled = false;
        try
        {
            var status = statusComboBox.SelectedItem is EnumFilterOption<ShipmentStatus> statusOption
                ? statusOption.Value
                : null;
            var cargo = cargoTypeComboBox.SelectedItem is EnumFilterOption<CargoType> cargoOption
                ? cargoOption.Value
                : null;
            var rows = await service.GetShipmentsAsync(
                searchTextBox.Text,
                status,
                cargo,
                branch.BranchId,
                fromPicker.Value.Date,
                toPicker.Value.Date);
            grid.DataSource = rows.Select(row => new ShipmentDisplayRow(row)).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Shipments could not be loaded.\n" + ex.Message, "Shipment Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            applyButton.Enabled = true;
        }
    }

    private async void AddButton_Click(object? sender, EventArgs e)
    {
        using var form = new ShipmentForm(service);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadShipmentsAsync();
        }
    }

    private async void EditButton_Click(object? sender, EventArgs e)
    {
        if (grid.CurrentRow?.DataBoundItem is not ShipmentDisplayRow row)
        {
            return;
        }
        if (row.Status != ShipmentStatus.Pending)
        {
            MessageBox.Show("Only Pending shipments can be edited.", "Shipment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var shipment = await service.GetByIdAsync(row.ShipmentId);
        if (shipment is null)
        {
            return;
        }
        using var form = new ShipmentForm(service, shipment);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadShipmentsAsync();
        }
    }

    private async void CancelShipmentButton_Click(object? sender, EventArgs e)
    {
        if (grid.CurrentRow?.DataBoundItem is not ShipmentDisplayRow row)
        {
            return;
        }
        if (row.Status != ShipmentStatus.Pending)
        {
            MessageBox.Show("Only Pending shipments can be cancelled.", "Shipment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (MessageBox.Show("Cancel the selected shipment?", "Confirm Cancellation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }
        var result = await service.CancelAsync(row.ShipmentId);
        if (!result.Success)
        {
            MessageBox.Show(result.ErrorMessage, "Shipment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        await LoadShipmentsAsync();
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || grid.Columns[e.ColumnIndex].Name != nameof(ShipmentDisplayRow.Status))
        {
            return;
        }
        if (grid.Rows[e.RowIndex].DataBoundItem is ShipmentDisplayRow row)
        {
            e.CellStyle.BackColor = row.Status switch
            {
                ShipmentStatus.Pending => Color.LightYellow,
                ShipmentStatus.Assigned => Color.LightBlue,
                ShipmentStatus.InTransit => Color.LightSalmon,
                ShipmentStatus.Delivered => Color.LightGreen,
                ShipmentStatus.Cancelled => Color.LightGray,
                _ => Color.White
            };
        }
    }

    private static List<EnumFilterOption<TEnum>> CreateAllOptionList<TEnum>()
        where TEnum : struct, Enum
    {
        var options = new List<EnumFilterOption<TEnum>> { new(null, "All") };
        options.AddRange(Enum.GetValues<TEnum>().Select(value => new EnumFilterOption<TEnum>(value, EnumDisplay.ToDisplayName(value))));
        return options;
    }

    private sealed record EnumFilterOption<TEnum>(TEnum? Value, string DisplayName)
        where TEnum : struct, Enum;

    private sealed class ShipmentDisplayRow
    {
        public int ShipmentId { get; }
        public string ReferenceNo { get; }
        public string Description { get; }
        public string Weight { get; }
        public string CargoType { get; }
        public string Origin { get; }
        public string Destination { get; }
        public ShipmentStatus Status { get; }
        public string Trip { get; }
        public string Branch { get; }
        public string Created { get; }

        public ShipmentDisplayRow(ShipmentListItem row)
        {
            ShipmentId = row.ShipmentId;
            ReferenceNo = row.ReferenceNo;
            Description = row.Description;
            Weight = row.WeightKg.ToString("0.##");
            CargoType = EnumDisplay.ToDisplayName(row.CargoType);
            Origin = row.Origin;
            Destination = row.Destination;
            Status = row.Status;
            Trip = row.TripId?.ToString() ?? "-";
            Branch = row.BranchName;
            Created = row.CreatedAt.ToString("yyyy-MM-dd HH:mm");
        }
    }
}
