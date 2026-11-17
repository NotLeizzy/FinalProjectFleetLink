using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Forms;

public sealed class RecordServiceForm : Form
{
    private readonly DateTimePicker completedDatePicker = new();
    private readonly NumericUpDown odometerInput = new();
    private readonly NumericUpDown costInput = new();
    private readonly TextBox notesTextBox = new();
    private readonly MaintenanceListItem item;

    public ServiceRecordRequest Request => new(
        DateOnly.FromDateTime(completedDatePicker.Value.Date),
        decimal.ToInt32(odometerInput.Value),
        costInput.Value,
        notesTextBox.Text);

    public bool ConfirmedBackDatedOdometer { get; private set; }

    public RecordServiceForm(MaintenanceListItem item)
    {
        this.item = item;
        Text = "Record Service";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(480, 300);
        Font = UiStyles.DefaultFont;
        InitializeControls();
    }

    private void InitializeControls()
    {
        var title = new Label
        {
            Text = $"Record service - {item.PlateNo}",
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font(UiStyles.DefaultFont, FontStyle.Bold),
            ForeColor = UiStyles.Text
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(layout, 0, "Completed date", completedDatePicker);
        completedDatePicker.Format = DateTimePickerFormat.Short;
        completedDatePicker.Value = DateTime.Today;
        completedDatePicker.MaxDate = DateTime.Today;
        AddRow(layout, 1, "Odometer at service", odometerInput);
        odometerInput.Minimum = 0;
        odometerInput.Maximum = 999999999;
        odometerInput.Value = item.CurrentOdometer;
        AddRow(layout, 2, "Cost (optional)", costInput);
        costInput.DecimalPlaces = 2;
        costInput.Minimum = 0;
        costInput.Maximum = 999999999;
        costInput.Increment = 0.01M;
        AddRow(layout, 3, "Notes (optional)", notesTextBox);
        notesTextBox.Multiline = true;
        notesTextBox.ScrollBars = ScrollBars.Vertical;
        notesTextBox.MaxLength = 500;
        notesTextBox.Height = 75;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var saveButton = new Button { Text = "Save", Width = 95, Height = 34 };
        var cancelButton = new Button { Text = "Cancel", Width = 95, Height = 34, DialogResult = DialogResult.Cancel };
        saveButton.Click += SaveButton_Click;
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(saveButton);
        layout.Controls.Add(buttons, 1, 4);
        Controls.Add(layout);
        Controls.Add(title);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, row == 3 ? 90 : 48));
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 7, 0, 0) }, 0, row);
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(control, 1, row);
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        if (completedDatePicker.Value.Date > DateTime.Today)
        {
            MessageBox.Show("Completed date cannot be in the future.", "Record Service", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (odometerInput.Value < 0)
        {
            MessageBox.Show("Odometer at service is required and must be 0 or greater.", "Record Service", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (notesTextBox.Text.Length > 500)
        {
            MessageBox.Show("Notes cannot exceed 500 characters.", "Record Service", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (odometerInput.Value < item.CurrentOdometer)
        {
            var answer = MessageBox.Show(
                $"The odometer at service is below the vehicle's current odometer ({item.CurrentOdometer:N0}). This record looks back-dated. Continue?",
                "Confirm back-dated odometer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
            {
                return;
            }
            ConfirmedBackDatedOdometer = true;
        }
        DialogResult = DialogResult.OK;
    }
}
