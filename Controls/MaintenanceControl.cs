using FinalProjectFleetLink.Forms;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Controls;

public sealed class MaintenanceControl : UserControl
{
    private readonly MaintenanceService service = new();
    private readonly MaintenanceWorkService workService = new();
    private readonly TextBox searchTextBox = new();
    private readonly ComboBox typeComboBox = new();
    private readonly ComboBox statusComboBox = new();
    private readonly ComboBox branchComboBox = new();
    private readonly DateTimePicker fromPicker = new();
    private readonly DateTimePicker toPicker = new();
    private readonly CheckBox dueOnlyCheckBox = new();
    private readonly CheckBox showClosedCheckBox = new();
    private readonly DataGridView grid = new();
    private readonly Button editButton = new();
    private readonly Button cancelButton = new();
    private readonly Button startButton = new();
    private readonly Button recordButton = new();
    private readonly Button historyButton = new();
    private bool loading;

    public MaintenanceControl()
    {
        Dock = DockStyle.Fill;
        BackColor = UiStyles.ContentBackground;
        InitializeControls();
        Load += async (_, _) => await LoadAsync();
    }

    private void InitializeControls()
    {
        var title = new Label { Text = "Maintenance", Dock = DockStyle.Top, Height = 36, Font = new Font(UiStyles.DefaultFont, FontStyle.Bold), ForeColor = UiStyles.Text };
        var filters = new Panel { Dock = DockStyle.Top, Height = 130 };
        AddLabel(filters, "Search plate", 0, 7); searchTextBox.Location = new Point(85, 2); searchTextBox.Width = 130; filters.Controls.Add(searchTextBox);
        AddLabel(filters, "Type", 235, 7); ConfigureEnumFilter(typeComboBox, 270, 2, CreateAll<MaintenanceType>()); filters.Controls.Add(typeComboBox);
        AddLabel(filters, "Status", 440, 7); ConfigureEnumFilter(statusComboBox, 490, 2, CreateAll<MaintenanceStatus>()); filters.Controls.Add(statusComboBox);
        AddLabel(filters, "Branch", 670, 7); branchComboBox.DropDownStyle = ComboBoxStyle.DropDownList; branchComboBox.Width = 150; branchComboBox.Location = new Point(720, 2); filters.Controls.Add(branchComboBox);
        AddLabel(filters, "From", 0, 43); ConfigureDate(fromPicker, new Point(40, 38), DateTime.Today.AddMonths(-1)); filters.Controls.Add(fromPicker);
        AddLabel(filters, "To", 155, 43); ConfigureDate(toPicker, new Point(180, 38), DateTime.Today); filters.Controls.Add(toPicker);
        dueOnlyCheckBox.Text = "Overdue / due soon only"; dueOnlyCheckBox.AutoSize = true; dueOnlyCheckBox.Location = new Point(320, 42); filters.Controls.Add(dueOnlyCheckBox);
        showClosedCheckBox.Text = "Show completed and cancelled"; showClosedCheckBox.AutoSize = true; showClosedCheckBox.Location = new Point(505, 42); filters.Controls.Add(showClosedCheckBox);
        var apply = new Button { Text = "Apply", Width = 85, Height = 30, Location = new Point(0, 78) }; apply.Click += async (_, _) => await LoadAsync(); filters.Controls.Add(apply);
        var schedule = new Button(); ConfigureButton(schedule, "Schedule", 100, 78, 95); editButton.Text = "Edit"; ConfigureButton(editButton, "Edit", 205, 78, 85); ConfigureButton(cancelButton, "Cancel", 300, 78, 85); ConfigureButton(startButton, "Start Work", 395, 78, 105); ConfigureButton(recordButton, "Record Service", 505, 78, 120); ConfigureButton(historyButton, "Vehicle History", 635, 78, 120);
        schedule.Click += Schedule_Click; editButton.Click += Edit_Click; cancelButton.Click += Cancel_Click; startButton.Click += Start_Click; recordButton.Click += Record_Click; historyButton.Click += History_Click;
        filters.Controls.AddRange([schedule, editButton, cancelButton, startButton, recordButton, historyButton]);
        ConfigureGrid();
        Controls.Add(grid); Controls.Add(filters); Controls.Add(title);
        searchTextBox.TextChanged += async (_, _) => { if (!loading) await LoadAsync(); };
        grid.SelectionChanged += (_, _) => UpdateButtons();
    }

    private async Task LoadAsync()
    {
        if (loading) return;
        loading = true;
        try
        {
            var branch = branchComboBox.SelectedItem as BranchFilterOption;
            MaintenanceType? type = typeComboBox.SelectedItem is EnumOption<MaintenanceType> typeOption && typeComboBox.SelectedIndex > 0 ? typeOption.Value : null;
            MaintenanceStatus? status = statusComboBox.SelectedItem is EnumOption<MaintenanceStatus> statusOption && statusComboBox.SelectedIndex > 0 ? statusOption.Value : null;
            var rows = await service.GetMaintenanceAsync(searchTextBox.Text, type, status, branch?.BranchId, DateOnly.FromDateTime(fromPicker.Value), DateOnly.FromDateTime(toPicker.Value), dueOnlyCheckBox.Checked, showClosedCheckBox.Checked);
            grid.DataSource = rows.ToList();
            UpdateButtons();
        }
        catch (Exception ex) { MessageBox.Show("Maintenance could not be loaded.\n" + ex.Message, "Maintenance", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { loading = false; }
    }

    private async void Schedule_Click(object? sender, EventArgs e) { using var form = new MaintenanceForm(); if (form.ShowDialog(this) == DialogResult.OK) await LoadAsync(); }
    private async void Edit_Click(object? sender, EventArgs e) { if (Selected() is not { Status: MaintenanceStatus.Scheduled } item) return; using var form = new MaintenanceForm(item); if (form.ShowDialog(this) == DialogResult.OK) await LoadAsync(); }
    private async void Cancel_Click(object? sender, EventArgs e)
    {
        if (Selected() is not { Status: MaintenanceStatus.Scheduled or MaintenanceStatus.InProgress } item) return;
        var reason = Prompt("Cancellation reason", "Enter the cancellation reason:");
        if (string.IsNullOrWhiteSpace(reason)) return;
        if (MessageBox.Show("Cancel this maintenance record?", "Confirm cancellation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var result = await service.CancelAsync(item.MaintenanceId, reason); ShowResult(result); if (result.Success) await LoadAsync();
    }
    private async void Start_Click(object? sender, EventArgs e)
    {
        if (Selected() is not { Status: MaintenanceStatus.Scheduled } item) return;
        var result = await workService.StartWorkAsync(item.MaintenanceId);
        if (!result.Success)
        {
            MessageBox.Show(result.ErrorMessage, "Maintenance", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (result.ScheduledTripsWarning is { Count: > 0 } trips)
        {
            var message = $"This vehicle has Scheduled trips in the next {MaintenanceDueSettings.DueSoonDays} days. Tell the Dispatcher:\n\n"
                + string.Join("\n", trips.Select(trip => $"Trip {trip.TripId}: {trip.PlannedDeparture:yyyy-MM-dd HH:mm} - {trip.PlannedArrival:yyyy-MM-dd HH:mm}, {trip.Origin} to {trip.Destination}"));
            MessageBox.Show(message, "Scheduled trips warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        await LoadAsync();
    }
    private async void Record_Click(object? sender, EventArgs e)
    {
        if (Selected() is not { Status: MaintenanceStatus.InProgress } item) return;
        using var form = new RecordServiceForm(item);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        var result = await workService.RecordServiceAsync(item.MaintenanceId, form.Request);
        if (!result.Success)
        {
            ShowResult(result);
            return;
        }
        if (item.MaintenanceType == MaintenanceType.Preventive
            && MessageBox.Show("Schedule the next preventive service?", "Preventive maintenance", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            var completedDate = form.Request.CompletedDate;
            var odometer = form.Request.OdometerAtService;
            var nextRequest = new MaintenanceRequest(
                item.VehicleId,
                MaintenanceType.Preventive,
                completedDate.AddMonths(MaintenanceDueSettings.NextPreventiveServiceMonths),
                odometer + MaintenanceDueSettings.NextPreventiveServiceOdometerKm,
                null);
            using var nextForm = new MaintenanceForm(prefill: nextRequest);
            nextForm.ShowDialog(this);
        }
        await LoadAsync();
    }

    private void History_Click(object? sender, EventArgs e)
    {
        if (Selected() is not MaintenanceListItem item)
        {
            return;
        }
        using var form = new VehicleHistoryForm(item.VehicleId);
        form.ShowDialog(this);
    }

    private MaintenanceListItem? Selected() => grid.CurrentRow?.DataBoundItem as MaintenanceListItem;
    private void UpdateButtons()
    {
        var item = Selected();
        editButton.Enabled = item?.Status == MaintenanceStatus.Scheduled;
        cancelButton.Enabled = item?.Status is MaintenanceStatus.Scheduled or MaintenanceStatus.InProgress;
        startButton.Enabled = item?.Status == MaintenanceStatus.Scheduled && Session.CurrentUser?.Role == Role.MaintenanceOfficer;
        recordButton.Enabled = item?.Status == MaintenanceStatus.InProgress && Session.CurrentUser?.Role == Role.MaintenanceOfficer;
        historyButton.Enabled = item is not null;
    }

    private void ConfigureGrid()
    {
        grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.MultiSelect = false; grid.RowHeadersVisible = false; grid.AutoGenerateColumns = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; grid.BackgroundColor = Color.White;
        AddColumn(nameof(MaintenanceListItem.PlateNo), "Vehicle"); AddColumn(nameof(MaintenanceListItem.MaintenanceType), "Type"); AddColumn(nameof(MaintenanceListItem.ScheduledDate), "Scheduled Date"); AddColumn(nameof(MaintenanceListItem.DueOdometer), "Due Odometer"); AddColumn(nameof(MaintenanceListItem.CurrentOdometer), "Current Odometer"); AddColumn(nameof(MaintenanceListItem.Status), "Status"); AddColumn(nameof(MaintenanceListItem.DueState), "Due state"); AddColumn(nameof(MaintenanceListItem.CompletedDate), "Completed Date"); AddColumn(nameof(MaintenanceListItem.Cost), "Cost"); AddColumn(nameof(MaintenanceListItem.BranchName), "Branch");
        grid.CellFormatting += (_, e) => { if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].DataBoundItem is MaintenanceListItem item) e.CellStyle.BackColor = MaintenanceDueHelper.GetRowColor(item.DueState); };
    }
    private void AddColumn(string property, string header) => grid.Columns.Add(new DataGridViewTextBoxColumn { Name = property, HeaderText = header, DataPropertyName = property });
    private static void AddLabel(Control parent, string text, int left, int top) => parent.Controls.Add(new Label { Text = text, AutoSize = true, Location = new Point(left, top) });
    private static void ConfigureEnumFilter<T>(ComboBox combo, int left, int top, List<EnumOption<T>> options) where T : struct, Enum { combo.DropDownStyle = ComboBoxStyle.DropDownList; combo.Width = 145; combo.Location = new Point(left, top); combo.DataSource = options; combo.DisplayMember = nameof(EnumOption<T>.DisplayName); }
    private static List<EnumOption<T>> CreateAll<T>() where T : struct, Enum => [new(default, "All"), .. Enum.GetValues<T>().Select(value => new EnumOption<T>(value, EnumDisplay.ToDisplayName(value)))];
    private static void ConfigureDate(DateTimePicker picker, Point point, DateTime value) { picker.Format = DateTimePickerFormat.Short; picker.Location = point; picker.Width = 105; picker.Value = value; }
    private static void ConfigureButton(Button button, string text, int left, int top, int width) { button.Text = text; button.Location = new Point(left, top); button.Width = width; button.Height = 30; button.BackColor = UiStyles.SidebarActive; button.ForeColor = Color.White; button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0; }
    private static void ShowResult(MaintenanceActionResult result) { if (!result.Success) MessageBox.Show(result.ErrorMessage, "Maintenance", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    private static void ShowResult(RecordServiceResult result) { if (!result.Success) MessageBox.Show(result.ErrorMessage, "Maintenance", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    private static string? Prompt(string title, string label)
    {
        using var form = new Form { Text = title, ClientSize = new Size(420, 145), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Font = UiStyles.DefaultFont };
        var text = new TextBox { Multiline = true, MaxLength = 500, Location = new Point(12, 35), Width = 390, Height = 55 }; form.Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(12, 10) }); form.Controls.Add(text); var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(230, 105), Width = 80 }; var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(320, 105), Width = 80 }; form.Controls.Add(ok); form.Controls.Add(cancel); form.AcceptButton = ok; form.CancelButton = cancel; return form.ShowDialog() == DialogResult.OK ? text.Text : null;
    }

}
