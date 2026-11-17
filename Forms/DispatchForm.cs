using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Forms;

public sealed class DispatchForm : Form
{
    private readonly DispatchService service = new();
    private readonly int tripId;
    private readonly DataGridView checksGrid = new();
    private readonly Button recheckButton = new();
    private readonly Button dispatchButton = new();
    private readonly Button closeButton = new();
    private readonly Label summaryLabel = new();
    private IReadOnlyList<CheckResult> checks = [];

    public DispatchForm(int tripId)
    {
        this.tripId = tripId;
        Text = $"Dispatch Trip {tripId}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(850, 430);
        Font = UiStyles.DefaultFont;
        InitializeControls();
        Load += async (_, _) => await LoadChecksAsync();
    }

    private void InitializeControls()
    {
        var title = new Label
        {
            Text = $"Dispatch checklist - Trip {tripId}",
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font(UiStyles.DefaultFont, FontStyle.Bold),
            ForeColor = UiStyles.Text
        };
        ConfigureGrid();
        summaryLabel.Dock = DockStyle.Top;
        summaryLabel.Height = 36;
        summaryLabel.Padding = new Padding(0, 8, 0, 0);
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 45,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        ConfigureButton(dispatchButton, "Dispatch");
        ConfigureButton(recheckButton, "Re-check");
        closeButton.Text = "Close";
        closeButton.Width = 90;
        closeButton.Height = 34;
        closeButton.DialogResult = DialogResult.Cancel;
        dispatchButton.Click += DispatchButton_Click;
        recheckButton.Click += async (_, _) => await LoadChecksAsync();
        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(dispatchButton);
        buttons.Controls.Add(recheckButton);
        Controls.Add(checksGrid);
        Controls.Add(summaryLabel);
        Controls.Add(buttons);
        Controls.Add(title);
        CancelButton = closeButton;
    }

    private void ConfigureGrid()
    {
        checksGrid.Dock = DockStyle.Fill;
        checksGrid.ReadOnly = true;
        checksGrid.AllowUserToAddRows = false;
        checksGrid.AllowUserToDeleteRows = false;
        checksGrid.AllowUserToResizeRows = false;
        checksGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        checksGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        checksGrid.MultiSelect = false;
        checksGrid.RowHeadersVisible = false;
        checksGrid.BackgroundColor = Color.White;
        checksGrid.BorderStyle = BorderStyle.None;
        checksGrid.AutoGenerateColumns = false;
        checksGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Result", HeaderText = "Result", DataPropertyName = nameof(CheckDisplayRow.Result), FillWeight = 12 });
        checksGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Code", HeaderText = "Code", DataPropertyName = nameof(CheckDisplayRow.Code), FillWeight = 12 });
        checksGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Check", DataPropertyName = nameof(CheckDisplayRow.Name), FillWeight = 22 });
        checksGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Message", HeaderText = "Message", DataPropertyName = nameof(CheckDisplayRow.Message), FillWeight = 54 });
        checksGrid.CellFormatting += ChecksGrid_CellFormatting;
    }

    private async Task LoadChecksAsync()
    {
        recheckButton.Enabled = false;
        dispatchButton.Enabled = false;
        try
        {
            checks = await service.RunChecksAsync(tripId);
            checksGrid.DataSource = checks.Select(check => new CheckDisplayRow(check)).ToList();
            var passed = checks.Count(check => check.Passed);
            var failed = checks.Count - passed;
            summaryLabel.Text = failed == 0
                ? $"All {passed} checks passed. Dispatch is available."
                : $"{failed} check(s) failed. Resolve the failures before dispatching.";
            summaryLabel.ForeColor = failed == 0 ? Color.DarkGreen : Color.DarkRed;
            dispatchButton.Enabled = checks.Count > 0 && failed == 0;
        }
        catch (Exception ex)
        {
            summaryLabel.Text = ex.Message;
            summaryLabel.ForeColor = Color.DarkRed;
        }
        finally
        {
            recheckButton.Enabled = true;
        }
    }

    private async void DispatchButton_Click(object? sender, EventArgs e)
    {
        dispatchButton.Enabled = false;
        try
        {
            var result = await service.DispatchAsync(tripId);
            checks = result.Checks;
            if (!result.Success)
            {
                checksGrid.DataSource = checks.Select(check => new CheckDisplayRow(check)).ToList();
                MessageBox.Show(result.ErrorMessage ?? "Dispatch failed.", "Dispatch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                await LoadChecksAsync();
                return;
            }
            MessageBox.Show("The trip was dispatched successfully.", "Dispatch", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            MessageBox.Show("The trip could not be dispatched.\n" + ex.Message, "Dispatch", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            dispatchButton.Enabled = checks.Count > 0 && checks.All(check => check.Passed);
        }
    }

    private void ChecksGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || checksGrid.Rows[e.RowIndex].DataBoundItem is not CheckDisplayRow row)
        {
            return;
        }
        e.CellStyle.ForeColor = row.Passed ? Color.DarkGreen : Color.DarkRed;
        e.CellStyle.BackColor = row.Passed ? Color.Honeydew : Color.MistyRose;
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

    private sealed class CheckDisplayRow
    {
        public string Result { get; }
        public string Code { get; }
        public string Name { get; }
        public string Message { get; }
        public bool Passed { get; }

        public CheckDisplayRow(CheckResult check)
        {
            Passed = check.Passed;
            Result = check.Passed ? "✓" : "✗";
            Code = check.Code;
            Name = check.Name;
            Message = check.Message;
        }
    }
}
