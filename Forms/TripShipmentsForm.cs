using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Forms;

public sealed class TripShipmentsForm : Form
{
    private readonly TripShipmentService service = new();
    private readonly int tripId;
    private readonly TextBox searchTextBox = new();
    private readonly ComboBox cargoTypeComboBox = new();
    private readonly ListBox assignedList = new();
    private readonly ListBox availableList = new();
    private readonly Label summaryLabel = new();
    private readonly Label cargoRuleLabel = new();
    private readonly Button addButton = new();
    private readonly Button removeButton = new();
    private IReadOnlyList<TripShipmentItem> assigned = [];
    private IReadOnlyList<TripShipmentItem> available = [];
    private bool loading;

    public TripShipmentsForm(int tripId)
    {
        this.tripId = tripId;
        Text = $"Manage Shipments - Trip {tripId}";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(950, 570);
        Font = UiStyles.DefaultFont;
        InitializeControls();
        Load += TripShipmentsForm_Load;
    }

    private void InitializeControls()
    {
        var filterPanel = new Panel { Dock = DockStyle.Top, Height = 48 };
        filterPanel.Controls.Add(new Label { Text = "Search", AutoSize = true, Location = new Point(0, 9) });
        searchTextBox.Width = 220; searchTextBox.Location = new Point(55, 4); searchTextBox.TextChanged += async (_, _) => await LoadViewAsync(); filterPanel.Controls.Add(searchTextBox);
        filterPanel.Controls.Add(new Label { Text = "Cargo", AutoSize = true, Location = new Point(300, 9) });
        cargoTypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList; cargoTypeComboBox.Width = 150; cargoTypeComboBox.Location = new Point(350, 4); cargoTypeComboBox.DataSource = CreateCargoOptions(); cargoTypeComboBox.DisplayMember = nameof(CargoOption.DisplayName); cargoTypeComboBox.SelectedIndexChanged += async (_, _) => await LoadViewAsync(); filterPanel.Controls.Add(cargoTypeComboBox);

        var assignedLabel = new Label { Text = "Shipments on this trip", Dock = DockStyle.Top, Height = 28, Font = new Font(UiStyles.DefaultFont, FontStyle.Bold) };
        var availableLabel = new Label { Text = "Pending shipments available", Dock = DockStyle.Top, Height = 28, Font = new Font(UiStyles.DefaultFont, FontStyle.Bold) };
        ConfigureList(assignedList); ConfigureList(availableList);
        var assignedPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 8, 0) }; assignedPanel.Controls.Add(assignedList); assignedPanel.Controls.Add(assignedLabel);
        var availablePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 0, 0) }; availablePanel.Controls.Add(availableList); availablePanel.Controls.Add(availableLabel);
        var lists = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(16, 8, 16, 8) }; lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46)); lists.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115)); lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10, 100, 10, 0) };
        ConfigureButton(addButton, "Add >>"); ConfigureButton(removeButton, "<< Remove"); addButton.Click += AddButton_Click; removeButton.Click += RemoveButton_Click; buttonPanel.Controls.Add(addButton); buttonPanel.Controls.Add(removeButton);
        lists.Controls.Add(assignedPanel, 0, 0); lists.Controls.Add(buttonPanel, 1, 0); lists.Controls.Add(availablePanel, 2, 0);

        summaryLabel.Dock = DockStyle.Top; summaryLabel.Height = 30; summaryLabel.Padding = new Padding(16, 5, 0, 0); summaryLabel.BackColor = Color.White;
        cargoRuleLabel.Dock = DockStyle.Top; cargoRuleLabel.Height = 30; cargoRuleLabel.Padding = new Padding(16, 5, 0, 0); cargoRuleLabel.BackColor = Color.White;
        Controls.Add(lists); Controls.Add(cargoRuleLabel); Controls.Add(summaryLabel); Controls.Add(filterPanel);
    }

    private static void ConfigureList(ListBox list) { list.Dock = DockStyle.Fill; list.DisplayMember = nameof(TripShipmentItem.ReferenceNo); list.SelectionMode = SelectionMode.One; }
    private static void ConfigureButton(Button button, string text) { button.Text = text; button.Width = 95; button.Height = 34; button.BackColor = UiStyles.SidebarActive; button.ForeColor = Color.White; button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0; }

    private async void TripShipmentsForm_Load(object? sender, EventArgs e)
    {
        cargoTypeComboBox.SelectedIndex = 0;
        await LoadViewAsync();
    }

    private async Task LoadViewAsync()
    {
        if (loading) return;
        loading = true;
        try
        {
            var cargo = cargoTypeComboBox.SelectedItem is CargoOption option ? option.Value : null;
            var view = await service.GetViewAsync(tripId, searchTextBox.Text, cargo);
            assigned = view.AssignedShipments; available = view.AvailableShipments;
            assignedList.DataSource = assigned.ToList(); availableList.DataSource = available.ToList();
            summaryLabel.Text = $"Trip {view.Trip.TripId} | Vehicle: {view.Trip.VehiclePlate} | Total weight: {view.Summary.TotalWeightKg:N2} kg | Capacity: {view.Summary.CapacityKg:N2} kg | Remaining: {view.Summary.RemainingCapacityKg:N2} kg";
            summaryLabel.ForeColor = view.Summary.OverCapacity ? Color.DarkRed : UiStyles.Text;
            cargoRuleLabel.Text = view.Summary.CargoTypesCompatible ? "Cargo types are compatible with the vehicle." : view.Summary.CargoRuleMessage ?? string.Empty;
            cargoRuleLabel.ForeColor = view.Summary.CargoTypesCompatible ? Color.DarkGreen : Color.DarkRed;
            addButton.Enabled = view.Trip.Status == TripStatus.Scheduled;
            removeButton.Enabled = view.Trip.Status == TripStatus.Scheduled;
        }
        catch (Exception ex) { MessageBox.Show("Trip shipments could not be loaded.\n" + ex.Message, "Trip Shipments Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { loading = false; }
    }

    private async void AddButton_Click(object? sender, EventArgs e)
    {
        if (availableList.SelectedItem is not TripShipmentItem item) return;
        var result = await service.AddAsync(tripId, item.ShipmentId);
        if (!result.Success) MessageBox.Show(result.ErrorMessage, "Add Shipment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        await LoadViewAsync();
    }

    private async void RemoveButton_Click(object? sender, EventArgs e)
    {
        if (assignedList.SelectedItem is not TripShipmentItem item) return;
        var result = await service.RemoveAsync(tripId, item.ShipmentId);
        if (!result.Success) MessageBox.Show(result.ErrorMessage, "Remove Shipment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        await LoadViewAsync();
    }

    private static List<CargoOption> CreateCargoOptions() => new List<CargoOption> { new(null, "All") }
        .Concat(Enum.GetValues<CargoType>().Select(value => new CargoOption(value, EnumDisplay.ToDisplayName(value))))
        .ToList();
    private sealed record CargoOption(CargoType? Value, string DisplayName);
}
