using System.Text;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Controls;

public sealed class ReportsControl : UserControl
{
    private sealed record ReportOption(ReportType Type, string DisplayName);

    private readonly ReportsService reportsService = new();
    private readonly ComboBox reportComboBox = new();
    private readonly ComboBox branchComboBox = new();
    private readonly DateTimePicker startDatePicker = new();
    private readonly DateTimePicker endDatePicker = new();
    private readonly ComboBox expiryWindowComboBox = new();
    private readonly ComboBox maintenanceVehicleComboBox = new();
    private readonly ComboBox maintenanceTypeComboBox = new();
    private readonly ComboBox maintenanceStatusComboBox = new();
    private readonly MaintenanceService maintenanceService = new();
    private readonly Label startDateLabel = new();
    private readonly Label endDateLabel = new();
    private readonly Button runButton = new();
    private readonly Button exportButton = new();
    private readonly DataGridView reportGrid = new();
    private ReportResult? currentReport;
    private bool loading;

    public ReportsControl()
    {
        Dock = DockStyle.Fill;
        BackColor = UiStyles.ContentBackground;
        InitializeControls();
        Load += ReportsControl_Load;
    }

    private void InitializeControls()
    {
        var titleLabel = new Label
        {
            Text = "Reports",
            Dock = DockStyle.Top,
            Height = 36,
            Font = new Font(UiStyles.DefaultFont, FontStyle.Bold),
            ForeColor = UiStyles.Text
        };
        var filterPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 122
        };
        var reportLabel = CreateFilterLabel("Report", 0);
        reportComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        reportComboBox.Width = 190;
        reportComboBox.Location = new Point(50, 0);
        reportComboBox.SelectedIndexChanged += (_, _) => UpdateDateFilterState();

        var branchLabel = CreateFilterLabel("Branch", 250);
        branchComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        branchComboBox.Width = 180;
        branchComboBox.Location = new Point(300, 0);

        startDateLabel.Text = "From";
        startDateLabel.AutoSize = true;
        startDateLabel.Location = new Point(495, 5);
        startDatePicker.Format = DateTimePickerFormat.Short;
        startDatePicker.Width = 105;
        startDatePicker.Location = new Point(530, 0);
        startDatePicker.Value = DateTime.Today.AddDays(-30);

        endDateLabel.Text = "To";
        endDateLabel.AutoSize = true;
        endDateLabel.Location = new Point(650, 5);
        endDatePicker.Format = DateTimePickerFormat.Short;
        endDatePicker.Width = 105;
        endDatePicker.Location = new Point(675, 0);
        endDatePicker.Value = DateTime.Today;

        expiryWindowComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        expiryWindowComboBox.Width = 105;
        expiryWindowComboBox.Location = new Point(795, 0);
        expiryWindowComboBox.DataSource = new[] { 30, 60, 90 };

        maintenanceVehicleComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        maintenanceVehicleComboBox.Width = 150;
        maintenanceVehicleComboBox.Location = new Point(0, 40);
        maintenanceTypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        maintenanceTypeComboBox.Width = 130;
        maintenanceTypeComboBox.Location = new Point(180, 40);
        maintenanceStatusComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        maintenanceStatusComboBox.Width = 130;
        maintenanceStatusComboBox.Location = new Point(340, 40);

        runButton.Text = "Run report";
        runButton.Width = 105;
        runButton.Height = 30;
        runButton.Location = new Point(0, 42);
        runButton.BackColor = UiStyles.SidebarActive;
        runButton.ForeColor = Color.White;
        runButton.FlatStyle = FlatStyle.Flat;
        runButton.FlatAppearance.BorderSize = 0;
        runButton.Click += async (_, _) => await RunReportAsync();

        exportButton.Text = "Export to CSV";
        exportButton.Width = 120;
        exportButton.Height = 30;
        exportButton.Location = new Point(115, 42);
        exportButton.FlatStyle = FlatStyle.Flat;
        exportButton.Enabled = false;
        exportButton.Click += ExportButton_Click;

        filterPanel.Controls.AddRange([
            reportLabel, reportComboBox, branchLabel, branchComboBox,
            startDateLabel, startDatePicker, endDateLabel, endDatePicker,
            new Label { Text = "Days", AutoSize = true, Location = new Point(755, 5) }, expiryWindowComboBox,
            new Label { Text = "Vehicle", AutoSize = true, Location = new Point(0, 80) }, maintenanceVehicleComboBox,
            new Label { Text = "Type", AutoSize = true, Location = new Point(180, 80) }, maintenanceTypeComboBox,
            new Label { Text = "Status", AutoSize = true, Location = new Point(340, 80) }, maintenanceStatusComboBox,
            runButton, exportButton
        ]);

        ConfigureGrid();
        Controls.Add(reportGrid);
        Controls.Add(filterPanel);
        Controls.Add(titleLabel);
    }

    private static Label CreateFilterLabel(string text, int left) => new()
    {
        Text = text,
        AutoSize = true,
        Location = new Point(left, 5)
    };

    private void ConfigureGrid()
    {
        reportGrid.Dock = DockStyle.Fill;
        reportGrid.ReadOnly = true;
        reportGrid.AllowUserToAddRows = false;
        reportGrid.AllowUserToDeleteRows = false;
        reportGrid.AllowUserToResizeRows = false;
        reportGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        reportGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        reportGrid.MultiSelect = false;
        reportGrid.RowHeadersVisible = false;
        reportGrid.BackgroundColor = Color.White;
        reportGrid.BorderStyle = BorderStyle.None;
    }

    private async void ReportsControl_Load(object? sender, EventArgs e)
    {
        try
        {
            loading = true;
            reportComboBox.DataSource = new List<ReportOption>
            {
                new(ReportType.VehicleList, "Vehicle list"),
                new(ReportType.DriverList, "Driver list"),
                new(ReportType.TripsSummary, "Trips summary"),
                new(ReportType.FuelUsage, "Fuel usage"),
                new(ReportType.MaintenanceHistory, "Maintenance history"),
                new(ReportType.MaintenanceCostPerVehicle, "Maintenance cost per vehicle"),
                new(ReportType.OverdueUpcomingMaintenance, "Overdue and upcoming maintenance"),
                new(ReportType.VehiclesWithoutRecentMaintenance, "Vehicles without maintenance in 6 months"),
                new(ReportType.UserAccounts, "User accounts"),
                new(ReportType.CurrentAssignments, "Current assignments"),
                new(ReportType.AssignmentHistory, "Assignment history"),
                new(ReportType.ExpiringDocuments, "Expiring documents"),
                new(ReportType.FuelConsumptionSummary, "Fuel consumption summary")
            };
            reportComboBox.DisplayMember = nameof(ReportOption.DisplayName);
            reportComboBox.ValueMember = nameof(ReportOption.Type);

            var branches = await BranchFilterService.GetOptionsAsync(Module.Reports);
            branchComboBox.DataSource = branches.ToList();
            branchComboBox.DisplayMember = nameof(BranchFilterOption.DisplayName);
            branchComboBox.ValueMember = nameof(BranchFilterOption.BranchId);
            var vehicles = await maintenanceService.GetVehicleOptionsAsync();
            maintenanceVehicleComboBox.DataSource = vehicles.Select(item => new MaintenanceVehicleOptionFilter(item.VehicleId, item.PlateNo)).Prepend(new MaintenanceVehicleOptionFilter(null, "All vehicles")).ToList();
            maintenanceVehicleComboBox.DisplayMember = nameof(MaintenanceVehicleOptionFilter.DisplayName);
            maintenanceVehicleComboBox.ValueMember = nameof(MaintenanceVehicleOptionFilter.VehicleId);
            maintenanceTypeComboBox.DataSource = Enum.GetValues<MaintenanceType>().Select(item => new EnumFilterOption<MaintenanceType>(item, EnumDisplay.ToDisplayName(item))).Prepend(new EnumFilterOption<MaintenanceType>(null, "All types")).ToList();
            maintenanceTypeComboBox.DisplayMember = nameof(EnumFilterOption<MaintenanceType>.DisplayName);
            maintenanceStatusComboBox.DataSource = Enum.GetValues<MaintenanceStatus>().Select(item => new EnumFilterOption<MaintenanceStatus>(item, EnumDisplay.ToDisplayName(item))).Prepend(new EnumFilterOption<MaintenanceStatus>(null, "All statuses")).ToList();
            maintenanceStatusComboBox.DisplayMember = nameof(EnumFilterOption<MaintenanceStatus>.DisplayName);
            UpdateDateFilterState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Report filters could not be loaded.\n" + ex.Message,
                "Reports Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            loading = false;
        }
    }

    private void UpdateDateFilterState()
    {
        if (reportComboBox.SelectedItem is not ReportOption option)
        {
            return;
        }

        var dateFilterEnabled = option.Type is ReportType.TripsSummary
            or ReportType.FuelUsage
            or ReportType.MaintenanceHistory
            or ReportType.AssignmentHistory
            or ReportType.FuelConsumptionSummary;
        var expiryFilterEnabled = option.Type == ReportType.ExpiringDocuments;
        var maintenanceFilterEnabled = option.Type == ReportType.MaintenanceHistory;
        startDateLabel.Enabled = dateFilterEnabled;
        endDateLabel.Enabled = dateFilterEnabled;
        startDatePicker.Enabled = dateFilterEnabled;
        endDatePicker.Enabled = dateFilterEnabled;
        expiryWindowComboBox.Enabled = expiryFilterEnabled;
        maintenanceVehicleComboBox.Enabled = maintenanceFilterEnabled;
        maintenanceTypeComboBox.Enabled = maintenanceFilterEnabled;
        maintenanceStatusComboBox.Enabled = maintenanceFilterEnabled;
        if (expiryFilterEnabled)
        {
            endDatePicker.Value = DateTime.Today.AddDays(30);
        }
    }

    private async Task RunReportAsync()
    {
        if (loading || reportComboBox.SelectedItem is not ReportOption report
            || branchComboBox.SelectedItem is not BranchFilterOption branch)
        {
            return;
        }

        runButton.Enabled = false;
        try
        {
            var usesDateRange = report.Type is ReportType.TripsSummary
                or ReportType.FuelUsage
                or ReportType.MaintenanceHistory
                or ReportType.MaintenanceCostPerVehicle
                or ReportType.AssignmentHistory
                or ReportType.FuelConsumptionSummary;
            var usesExpiryWindow = report.Type == ReportType.ExpiringDocuments;
            currentReport = await reportsService.GetReportAsync(
                report.Type,
                branch.BranchId,
                usesDateRange ? startDatePicker.Value.Date : null,
                usesExpiryWindow
                    ? DateTime.Today.AddDays((int)(expiryWindowComboBox.SelectedItem ?? 30))
                    : usesDateRange ? endDatePicker.Value.Date : null,
                report.Type == ReportType.MaintenanceHistory && maintenanceVehicleComboBox.SelectedItem is MaintenanceVehicleOptionFilter vehicle ? vehicle.VehicleId : null,
                report.Type == ReportType.MaintenanceHistory && maintenanceTypeComboBox.SelectedItem is EnumFilterOption<MaintenanceType> type ? type.Value : null,
                report.Type == ReportType.MaintenanceHistory && maintenanceStatusComboBox.SelectedItem is EnumFilterOption<MaintenanceStatus> status ? status.Value : null);
            BindReport(currentReport);
            exportButton.Enabled = true;
        }
        catch (Exception ex)
        {
            currentReport = null;
            exportButton.Enabled = false;
            MessageBox.Show(
                "The report could not be loaded.\n" + ex.Message,
                "Report Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            runButton.Enabled = true;
        }
    }

    private sealed record MaintenanceVehicleOptionFilter(int? VehicleId, string DisplayName);
    private sealed record EnumFilterOption<T>(T? Value, string DisplayName) where T : struct, Enum;

    private void BindReport(ReportResult report)
    {
        reportGrid.Columns.Clear();
        foreach (var column in report.Columns)
        {
            reportGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = column,
                Name = column,
                FillWeight = Math.Max(50, column.Length * 8)
            });
        }

        foreach (var row in report.Rows)
        {
            reportGrid.Rows.Add(row.ToArray());
        }
    }

    private void ExportButton_Click(object? sender, EventArgs e)
    {
        if (currentReport is null)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt = "csv",
            AddExtension = true,
            FileName = currentReport.Title.Replace(' ', '_') + ".csv"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            using var writer = new StreamWriter(dialog.FileName, false, utf8WithBom);
            writer.WriteLine(string.Join(",", currentReport.Columns.Select(EscapeCsvValue)));
            foreach (var row in currentReport.Rows)
            {
                writer.WriteLine(string.Join(",", row.Select(EscapeCsvValue)));
            }

            MessageBox.Show(
                "The report was exported successfully.",
                "Export Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "The report could not be exported.\n" + ex.Message,
                "Export Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string EscapeCsvValue(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\r') || value.Contains('\n'))
        {
            return '"' + value.Replace("\"", "\"\"") + '"';
        }
        return value;
    }
}
