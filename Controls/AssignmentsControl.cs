using FinalProjectFleetLink.Forms;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Services;
using FinalProjectFleetLink.Models;

namespace FinalProjectFleetLink.Controls;

public sealed class AssignmentsControl : UserControl
{
    private readonly AssignmentService service = new();
    private readonly ComboBox branchComboBox = new();
    private readonly DateTimePicker historyFromPicker = new();
    private readonly DateTimePicker historyToPicker = new();
    private readonly DataGridView currentGrid = CreateGrid();
    private readonly DataGridView historyGrid = CreateGrid();
    private readonly TabControl tabs = new();
    private readonly Button assignButton = new();
    private readonly Button reassignButton = new();
    private readonly Button endButton = new();
    private readonly Button refreshButton = new();
    private bool loading;

    public AssignmentsControl()
    {
        Dock = DockStyle.Fill;
        BackColor = UiStyles.ContentBackground;
        InitializeControls();
        Load += AssignmentsControl_Load;
    }

    private void InitializeControls()
    {
        var title = new Label
        {
            Text = "Driver Assignments",
            Dock = DockStyle.Top,
            Height = 36,
            Font = new Font(UiStyles.DefaultFont, FontStyle.Bold),
            ForeColor = UiStyles.Text
        };
        var filterPanel = new Panel { Dock = DockStyle.Top, Height = 75 };
        branchComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        branchComboBox.Width = 180;
        branchComboBox.Location = new Point(55, 2);
        branchComboBox.SelectedIndexChanged += async (_, _) => await LoadCurrentAsync();
        filterPanel.Controls.Add(new Label { Text = "Branch", AutoSize = true, Location = new Point(0, 7) });
        filterPanel.Controls.Add(branchComboBox);

        ConfigureButton(assignButton, "Assign", 0);
        ConfigureButton(reassignButton, "Reassign", 110);
        ConfigureButton(endButton, "End Assignment", 220);
        ConfigureButton(refreshButton, "Refresh", 350);
        assignButton.Click += AssignButton_Click;
        reassignButton.Click += ReassignButton_Click;
        endButton.Click += EndButton_Click;
        refreshButton.Click += async (_, _) => await LoadAllAsync();
        filterPanel.Controls.AddRange([assignButton, reassignButton, endButton, refreshButton]);

        var historyFilters = new Panel { Dock = DockStyle.Top, Height = 42 };
        historyFromPicker.Format = DateTimePickerFormat.Short;
        historyFromPicker.Value = DateTime.Today.AddMonths(-1);
        historyFromPicker.Width = 105;
        historyFromPicker.Location = new Point(35, 4);
        historyToPicker.Format = DateTimePickerFormat.Short;
        historyToPicker.Value = DateTime.Today;
        historyToPicker.Width = 105;
        historyToPicker.Location = new Point(205, 4);
        var applyHistoryButton = new Button { Text = "Apply", Width = 75, Height = 28, Location = new Point(320, 2) };
        applyHistoryButton.Click += async (_, _) => await LoadHistoryAsync();
        historyFilters.Controls.AddRange([
            new Label { Text = "From", AutoSize = true, Location = new Point(0, 9) }, historyFromPicker,
            new Label { Text = "To", AutoSize = true, Location = new Point(175, 9) }, historyToPicker,
            applyHistoryButton
        ]);

        ConfigureCurrentGrid();
        ConfigureHistoryGrid();
        var currentPage = new TabPage("Current assignments");
        currentPage.Controls.Add(currentGrid);
        var historyPage = new TabPage("History");
        historyPage.Controls.Add(historyGrid);
        historyPage.Controls.Add(historyFilters);
        tabs.Dock = DockStyle.Fill;
        tabs.TabPages.Add(currentPage);
        tabs.TabPages.Add(historyPage);

        Controls.Add(tabs);
        Controls.Add(filterPanel);
        Controls.Add(title);
    }

    private static void ConfigureButton(Button button, string text, int left)
    {
        button.Text = text;
        button.Width = text == "End Assignment" ? 120 : 100;
        button.Height = 30;
        button.Location = new Point(left, 38);
        button.BackColor = UiStyles.SidebarActive;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
    }

    private static DataGridView CreateGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RowHeadersVisible = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None
    };

    private void ConfigureCurrentGrid()
    {
        currentGrid.AutoGenerateColumns = false;
        currentGrid.Columns.AddRange(
            TextColumn("AssignmentId", "Id", nameof(AssignmentListItem.AssignmentId)),
            TextColumn("PlateNo", "Vehicle plate", nameof(AssignmentListItem.PlateNo)),
            TextColumn("VehicleType", "Vehicle type", nameof(AssignmentListItem.VehicleType)),
            TextColumn("DriverName", "Driver", nameof(AssignmentListItem.DriverName)),
            TextColumn("LicenseExpiry", "License expiry", nameof(AssignmentListItem.LicenseExpiry)),
            TextColumn("StartDate", "Start Date", nameof(AssignmentListItem.StartDate)),
            TextColumn("BranchName", "Branch", nameof(AssignmentListItem.BranchName)));
        currentGrid.Columns[0].Visible = false;
    }

    private void ConfigureHistoryGrid()
    {
        historyGrid.AutoGenerateColumns = false;
        historyGrid.Columns.AddRange(
            TextColumn("PlateNo", "Vehicle plate", nameof(AssignmentListItem.PlateNo)),
            TextColumn("VehicleType", "Vehicle type", nameof(AssignmentListItem.VehicleType)),
            TextColumn("DriverName", "Driver", nameof(AssignmentListItem.DriverName)),
            TextColumn("StartDate", "Start Date", nameof(AssignmentListItem.StartDate)),
            TextColumn("EndDate", "End Date", nameof(AssignmentListItem.EndDate)),
            TextColumn("BranchName", "Branch", nameof(AssignmentListItem.BranchName)));
    }

    private static DataGridViewTextBoxColumn TextColumn(string name, string header, string property) => new()
    {
        Name = name,
        HeaderText = header,
        DataPropertyName = property
    };

    private async void AssignmentsControl_Load(object? sender, EventArgs e)
    {
        try
        {
            loading = true;
            var branches = await BranchFilterService.GetOptionsAsync(Module.Assignments);
            branchComboBox.DataSource = branches.ToList();
            branchComboBox.DisplayMember = nameof(BranchFilterOption.DisplayName);
            branchComboBox.ValueMember = nameof(BranchFilterOption.BranchId);
            await LoadAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Assignments could not be loaded.\n" + ex.Message, "Assignments Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            loading = false;
        }
    }

    private async Task LoadAllAsync()
    {
        await LoadCurrentAsync();
        await LoadHistoryAsync();
    }

    private async Task LoadCurrentAsync()
    {
        if (loading) return;
        currentGrid.DataSource = (await service.GetCurrentAssignmentsAsync(GetBranchId())).ToList();
    }

    private async Task LoadHistoryAsync()
    {
        if (loading) return;
        historyGrid.DataSource = (await service.GetHistoryAsync(
            GetBranchId(),
            DateOnly.FromDateTime(historyFromPicker.Value),
            DateOnly.FromDateTime(historyToPicker.Value))).ToList();
    }

    private int? GetBranchId() => branchComboBox.SelectedItem is BranchFilterOption option ? option.BranchId : null;

    private async void AssignButton_Click(object? sender, EventArgs e)
    {
        using var form = new AssignmentForm(service, false);
        if (form.ShowDialog(this) == DialogResult.OK) await LoadAllAsync();
    }

    private async void ReassignButton_Click(object? sender, EventArgs e)
    {
        if (currentGrid.CurrentRow?.DataBoundItem is not AssignmentListItem assignment) return;
        using var form = new AssignmentForm(service, true, assignment.VehicleId);
        if (form.ShowDialog(this) == DialogResult.OK) await LoadAllAsync();
    }

    private async void EndButton_Click(object? sender, EventArgs e)
    {
        if (currentGrid.CurrentRow?.DataBoundItem is not AssignmentListItem assignment) return;
        using var picker = new DatePickerForm("End Assignment", assignment.StartDate);
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var result = await service.EndAssignmentAsync(assignment.AssignmentId, picker.SelectedDate);
            if (!result.Success) MessageBox.Show(result.ErrorMessage, "End Assignment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else await LoadAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "End Assignment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private sealed class DatePickerForm : Form
    {
        private readonly DateTimePicker picker = new();
        public DateOnly SelectedDate => DateOnly.FromDateTime(picker.Value);
        public DatePickerForm(string title, DateOnly minimum)
        {
            Text = title; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(280, 125); MinimizeBox = false; MaximizeBox = false;
            picker.Format = DateTimePickerFormat.Short; picker.MinDate = minimum.ToDateTime(TimeOnly.MinValue); picker.MaxDate = DateTime.Today; picker.Value = DateTime.Today; picker.Location = new Point(20, 15);
            var ok = new Button { Text = "End", DialogResult = DialogResult.OK, Location = new Point(105, 60), Width = 70 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(180, 60), Width = 70 };
            Controls.AddRange([picker, ok, cancel]); AcceptButton = ok; CancelButton = cancel;
        }
    }
}
